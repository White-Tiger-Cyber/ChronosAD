using System.Windows;

namespace ChronosAD.Views;

public partial class AddPunchDialog : Window
{
    public DateTime ClockIn { get; private set; }
    public DateTime? ClockOut { get; private set; }
    public string? Note { get; private set; }

    public AddPunchDialog(string employeeName)
    {
        InitializeComponent();
        TxtEmployeeName.Text = $"Adding punch for: {employeeName}";
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!DateTime.TryParse(TxtClockIn.Text, out var clockIn))
        {
            MessageBox.Show("Invalid Clock In date/time.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DateTime? clockOut = null;
        if (!string.IsNullOrWhiteSpace(TxtClockOut.Text))
        {
            if (!DateTime.TryParse(TxtClockOut.Text, out var co))
            {
                MessageBox.Show("Invalid Clock Out date/time.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (co <= clockIn)
            {
                MessageBox.Show("Clock Out must be after Clock In.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            clockOut = co;
        }

        ClockIn = clockIn;
        ClockOut = clockOut;
        Note = string.IsNullOrWhiteSpace(TxtNote.Text) ? null : TxtNote.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
