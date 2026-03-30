using System.Windows;
using ChronosAD.Models;

namespace ChronosAD.Views;

public partial class PunchEditDialog : Window
{
    private readonly Punch _punch;

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

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
