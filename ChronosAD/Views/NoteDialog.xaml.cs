using System.Windows;

namespace ChronosAD.Views;

public partial class NoteDialog : Window
{
    public string NoteText { get; private set; } = string.Empty;

    public NoteDialog() => InitializeComponent();

    private void BtnAttach_Click(object sender, RoutedEventArgs e)
    {
        NoteText = TxtNote.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
