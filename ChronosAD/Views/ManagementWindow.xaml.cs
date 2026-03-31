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
    }

    private void Render()
    {
        GridEmployees.ItemsSource = _vm.Employees;
        GridAllMessages.ItemsSource = _vm.AllMessages;
        DpPayPeriodStart.SelectedDate = _vm.Config.PayPeriodStartDate;
        TxtHolidays.Text = string.Join("\n", _vm.Config.HolidayDates.Select(d => d.ToString("yyyy-MM-dd")));
    }

    private void GridEmployees_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _vm.SelectedEmployee = GridEmployees.SelectedItem as EmployeeStatus;
        GridEmployeePunches.ItemsSource = _vm.SelectedPunches;
        if (_vm.SelectedEmployee != null)
            TxtSelectedEmployee.Text = $"Punches — {_vm.SelectedEmployee.User.FullName}";
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
        TxtConfigStatus.Text = "Configuration saved.";
    }
}
