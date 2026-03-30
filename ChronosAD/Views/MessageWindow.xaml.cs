using System.Windows;
using ChronosAD.Services;

namespace ChronosAD.Views;

public partial class MessageWindow : Window
{
    private readonly string _sid;
    private readonly MessageService _msgSvc;

    public MessageWindow(string sid, MessageService msgSvc)
    {
        InitializeComponent();
        _sid = sid;
        _msgSvc = msgSvc;
        LoadMessages();
    }

    private void LoadMessages() => GridMessages.ItemsSource = _msgSvc.GetForUser(_sid);

    private void BtnSend_Click(object sender, RoutedEventArgs e)
    {
        var text = TxtNewMessage.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        _msgSvc.Send(_sid, text);
        TxtNewMessage.Clear();
        LoadMessages();
    }
}
