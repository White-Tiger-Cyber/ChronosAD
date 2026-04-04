using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ChronosAD.Models;
using ChronosAD.ViewModels;

namespace ChronosAD.Views;

public partial class ManagementWindow : Window
{
    private readonly ManagementViewModel _vm;

    public ManagementWindow(ManagementViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        Render();

        // Silently archive records older than 1 year on manager startup
        Loaded += async (_, _) =>
        {
            await Task.Run(() => _vm.SilentArchive());
            _vm.Refresh();
            Render();
        };
    }

    private void Render()
    {
        GridEmployees.ItemsSource = _vm.Employees;
        GridAllMessages.ItemsSource = _vm.AllMessages;
        DpPayPeriodStart.SelectedDate = _vm.Config.PayPeriodStartDate;
        TxtHolidays.Text = string.Join("\n", _vm.Config.HolidayDates.Select(d => d.ToString("MM-dd-yyyy")));
        TxtConfigWarning.Visibility = _vm.IsConfigSaved ? Visibility.Collapsed : Visibility.Visible;
        LstPayPeriods.ItemsSource = _vm.PayPeriodList;
        CboHistoryEmployee.ItemsSource = _vm.Employees;
    }

    private void GridEmployees_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _vm.SelectedEmployee = GridEmployees.SelectedItem as EmployeeStatus;
        GridEmployeePunches.ItemsSource = _vm.SelectedPunches;
        if (_vm.SelectedEmployee != null)
        {
            TxtSelectedEmployee.Text = $"Punches — {_vm.SelectedEmployee.User.FullName}";
            TxtPeriodSummary.Text = _vm.SelectedEmployeePeriodSummary;
        }
        else
        {
            TxtSelectedEmployee.Text = "Select an employee to view punches";
            TxtPeriodSummary.Text = "";
        }
    }

    private void BtnAddPunch_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedEmployee == null) return;
        var dlg = new AddPunchDialog(_vm.SelectedEmployee.User.FullName);
        if (dlg.ShowDialog() == true)
        {
            _vm.AddPunch(dlg.ClockIn, dlg.ClockOut, dlg.Note);
            GridEmployees_SelectionChanged(sender, null!);
        }
    }

    private void BtnEditPunch_Click(object sender, RoutedEventArgs e)
    {
        if (GridEmployeePunches.SelectedItem is not Punch punch) return;
        var dlg = new PunchEditDialog(punch);
        if (dlg.ShowDialog() == true)
        {
            if (dlg.IsDelete)
                _vm.DeletePunch(punch);
            else
                _vm.SavePunchEdit(punch);
            GridEmployees_SelectionChanged(sender, null!);
        }
    }

    private void BtnFreeze_Click(object sender, RoutedEventArgs e)
    {
        if (GridEmployees.SelectedItem is not EmployeeStatus emp) return;
        _vm.FreezeUser(emp.User.SID);
        Render();
    }

    private void BtnPromote_Click(object sender, RoutedEventArgs e)
    {
        if (GridEmployees.SelectedItem is not EmployeeStatus emp) return;
        _vm.PromoteUser(emp.User.SID);
        Render();
    }

    private void BtnDeleteUser_Click(object sender, RoutedEventArgs e)
    {
        if (GridEmployees.SelectedItem is not EmployeeStatus emp) return;
        var result = MessageBox.Show($"Permanently delete {emp.User.FullName}?\nThis cannot be undone.",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _vm.DeleteUser(emp.User.SID);
            Render();
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        _vm.Refresh();
        Render();
    }

    private void BtnAutoClockOut_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("This will clock out ALL currently active employees.\nProceed?",
            "Auto Clock-Out", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _vm.RunAutoClockOut();
            Render();
        }
    }

    private void BtnArchive_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Archive all punch records older than 12 months?\nThey will be moved to History_Archive.",
            "Archive Data", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _vm.ArchiveData();
            MessageBox.Show("Archive complete.", "ChronosAD", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "CSV Files (*.csv)|*.csv", FileName = $"ChronosAD_Export_{DateTime.Today:yyyyMMdd}.csv" };
        if (dlg.ShowDialog() == true)
        {
            _vm.ExportToCSV(dlg.FileName);
            MessageBox.Show($"Exported to:\n{dlg.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnDeleteMessage_Click(object sender, RoutedEventArgs e)
    {
        if (GridAllMessages.SelectedItem is not Message msg) return;
        var result = MessageBox.Show(
            $"Permanently delete this message from {msg.UserName}?\nThis cannot be undone.",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _vm.DeleteMessage(msg.MessageID);
            GridAllMessages.ItemsSource = _vm.AllMessages;
        }
    }

    private void BtnReply_Click(object sender, RoutedEventArgs e)
    {
        if (GridAllMessages.SelectedItem is not Message msg) return;
        var text = TxtManagerResponse.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        _vm.RespondToMessage(msg.MessageID, text);
        TxtManagerResponse.Clear();
        _vm.Refresh();
        Render();
    }

    private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
    {
        if (DpPayPeriodStart.SelectedDate == null) { TxtConfigStatus.Text = "Please select a start date."; return; }
        var config = new Config { PayPeriodStartDate = DpPayPeriodStart.SelectedDate.Value };
        foreach (var line in TxtHolidays.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (DateTime.TryParse(line.Trim(), out var dt))
                config.HolidayDates.Add(dt);
        }
        _vm.SaveConfig(config);
        _vm.Refresh();
        Render();
        TxtConfigStatus.Text = "Configuration saved.";
    }

    // ── Punch History Tab ─────────────────────────────────────────────────────

    private void CboHistoryEmployee_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _vm.SelectedHistoryEmployee = CboHistoryEmployee.SelectedItem as EmployeeStatus;
        UpdateHistoryDisplay();
    }

    private void LstPayPeriods_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _vm.SelectedHistoryPeriod = LstPayPeriods.SelectedItem as PayPeriodEntry;
        UpdateHistoryDisplay();
    }

    private void UpdateHistoryDisplay()
    {
        GridHistoryPunches.ItemsSource = _vm.HistoryPunches;
        TxtHistorySummary.Text = _vm.HistoryPeriodSummary;

        if (_vm.SelectedHistoryEmployee != null && _vm.SelectedHistoryPeriod != null)
            TxtHistoryHeader.Text = $"{_vm.SelectedHistoryEmployee.User.FullName} — {_vm.SelectedHistoryPeriod.Display}";
        else if (_vm.SelectedHistoryEmployee != null)
            TxtHistoryHeader.Text = $"{_vm.SelectedHistoryEmployee.User.FullName} — Select a pay period";
        else
            TxtHistoryHeader.Text = "Select an employee and pay period";
    }
}
