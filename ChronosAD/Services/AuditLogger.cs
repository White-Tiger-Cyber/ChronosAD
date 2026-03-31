using System.IO;

namespace ChronosAD.Services;

/// <summary>
/// Thread-safe flat-file audit logger. Rotates to a .bak file when the
/// log exceeds MaxBytes, keeping at most ~20 MB on disk at any time.
/// </summary>
public class AuditLogger
{
    private readonly string _logPath;
    private readonly object _lock = new();
    private const long MaxBytes = 10L * 1024 * 1024; // 10 MB

    public AuditLogger(string logPath)
    {
        _logPath = logPath;
        var dir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public void Log(string action, string details)
    {
        lock (_lock)
        {
            RotateIfNeeded();
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{action}] {details}{Environment.NewLine}";
            File.AppendAllText(_logPath, line);
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_logPath)) return;
        if (new FileInfo(_logPath).Length < MaxBytes) return;

        var backup = _logPath + ".bak";
        if (File.Exists(backup)) File.Delete(backup);
        File.Move(_logPath, backup);
    }
}
