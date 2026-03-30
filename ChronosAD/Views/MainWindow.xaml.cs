using System.Windows;
using System.Windows.Threading;
using ChronosAD.Data;
using ChronosAD.Services;
using ChronosAD.ViewModels;

namespace ChronosAD.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DatabaseService _db;
    private readonly PunchService _punchSvc;
    private readonly MessageService _msgSvc;
    private readonly DispatcherTimer _clockTimer;
    private string? _pendingNote;

    public MainWindow(MainViewModel vm, DatabaseService db, PunchService punchSvc, MessageService msgSvc)
    {
        InitializeComponent();
        _vm = vm;
        _db = db;
        _punchSvc = punchSvc;
        _msgSvc = msgSvc;
        DataContext = vm;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => TxtCurrentTime.Text = DateTime.Now.ToString("hh:mm:ss tt");
        _clockTimer.Start();

        if (vm.CurrentUser.IsManager)
            BtnManagement.Visibility = Visibility.Visible;

        Render();
    }

    private void Render()
    {
        TxtWelcome.Text = $"Welcome, {_vm.CurrentUser.FullName}";
        TxtStatus.Text = _vm.StatusText;
        TxtTotalHours.Text = _vm.TotalHoursText;
        TxtPeriodRange.Text = $"{_vm.PeriodStart:MM/dd/yy} \u2013 {_vm.PeriodEnd.AddDays(-1):MM/dd/yy}";
        TxtMessagePreview.Text = _vm.LatestMessageDisplay;
        GridPunches.ItemsSource = _vm.Punches;

        if (_vm.IsClockedIn)
        {
            BtnPunch.Content = "Clock Out";
            BtnPunch.Background = (System.Windows.Media.SolidColorBrush)FindResource("ClockOutBrush");
        }
        else
        {
            BtnPunch.Content = "Clock In";
            BtnPunch.Background = (System.Windows.Media.SolidColorBrush)FindResource("ClockInBrush");
        }
    }

    private void BtnPunch_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.TogglePunch(_pendingNote, out var error))
            MessageBox.Show(error, "ChronosAD", MessageBoxButton.OK, MessageBoxImage.Warning);
        _pendingNote = null;
        Render();
    }

    private void BtnNote_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new NoteDialog();
        if (dlg.ShowDialog() == true)
            _pendingNote = dlg.NoteText;
    }

    private void BtnMessage_Click(object sender, RoutedEventArgs e)
    {
        var msgWin = new MessageWindow(_vm.CurrentUser.SID, _msgSvc);
        msgWin.ShowDialog();
        _vm.Refresh();
        Render();
    }

    private void BtnManagement_Click(object sender, RoutedEventArgs e)
    {
        var mgmtVm = new ManagementViewModel(_db, _punchSvc, _msgSvc, _vm.CurrentUser.SID);
        var mgmtWin = new ManagementWindow(mgmtVm);
        mgmtWin.ShowDialog();
        _vm.Refresh();
        Render();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        _vm.Refresh();
        Render();
    }
}
