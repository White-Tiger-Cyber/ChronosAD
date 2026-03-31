using System.Windows;
using ChronosAD.Models;

namespace ChronosAD.Views;

public partial class PunchEditDialog : Window
{
    private readonly Punch _punch;
    public bool IsDelete { get; private set; } = false;

    public PunchEditDialog(Punch punch)
    {
        InitializeComponent();
        _punch = punch;
        TxtClockIn.Text = punch.ClockInTime.ToString("MM/dd/yyyy HH:mm");
        TxtClockOut.Text = punch.ClockOutTime?.ToString("MM/dd/yyyy HH:mm") ?? string.Empty;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!DateTime.TryParse(TxtClockIn.Text, out var clockIn))
        {
            MessageBox.Show("Invalid Clock In date/time.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _punch.ClockInTime = clockIn;

        if (string.IsNullOrWhiteSpace(TxtClockOut.Text))
            _punch.ClockOutTime = null;
        else if (DateTime.TryParse(TxtClockOut.Text, out var clockOut))
            _punch.ClockOutTime = clockOut;
        else
        {
            MessageBox.Show("Invalid Clock Out date/time.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            $"Permanently delete this punch record?\n\nClock In: {_punch.ClockInTime:MM/dd/yyyy HH:mm}\nThis cannot be undone.",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            IsDelete = true;
            DialogResult = true;
            Close();
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
