using System.IO;
using System.Text.Json;
using System.Windows;
using ChronosAD.Data;
using ChronosAD.Services;
using ChronosAD.ViewModels;
using ChronosAD.Views;

namespace ChronosAD;

public partial class App : Application
{
    // Reads connection string and log path from appsettings.json next to the executable.
    // To deploy to a different client, edit appsettings.json — no recompile needed.
    public static string ConnectionString { get; } = LoadConnectionString();
    public static string LogPath { get; } = LoadLogPath();

    private static JsonDocument LoadConfig()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException(
                "appsettings.json not found next to the executable. " +
                "Edit appsettings.json with your SQL Server name before running.", configPath);
        return JsonDocument.Parse(File.ReadAllText(configPath));
    }

    private static string LoadConnectionString()
    {
        using var doc = LoadConfig();
        return doc.RootElement.GetProperty("ConnectionString").GetString()
            ?? throw new InvalidOperationException("ConnectionString is missing or null in appsettings.json");
    }

    private static string LoadLogPath()
    {
        using var doc = LoadConfig();
        return doc.RootElement.TryGetProperty("LogPath", out var lp)
            ? lp.GetString() ?? @"C:\ProgramData\ChronosAD\audit.log"
            : @"C:\ProgramData\ChronosAD\audit.log";
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            DatabaseInitializer.Initialize(ConnectionString);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Database connection failed:\n{ex.Message}\n\nEnsure SQL Server Express is running and the connection string is correct.",
                "ChronosAD — Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var logger = new AuditLogger(LogPath);
        var db = new DatabaseService(ConnectionString, logger);
        var auth = new AuthService();
        string sid;
        try { sid = auth.GetCurrentUserSID(); }
        catch
        {
            MessageBox.Show("Could not determine Windows identity. ChronosAD requires Windows Authentication.",
                "ChronosAD — Auth Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // Auto clock-out check (midnight reset applied on startup if needed)
        var user = db.GetUserBySID(sid);

        if (user == null)
        {
            var regVm = new RegistrationViewModel(db, sid);
            var regWindow = new RegistrationWindow(regVm);
            if (regWindow.ShowDialog() != true) { Shutdown(); return; }
            user = db.GetUserBySID(sid)!;
        }

        var punchSvc = new PunchService(db);
        var msgSvc = new MessageService(db);
        var mainVm = new MainViewModel(db, punchSvc, msgSvc, user);
        var mainWindow = new MainWindow(mainVm, db, punchSvc, msgSvc);
        mainWindow.Show();
    }
}
