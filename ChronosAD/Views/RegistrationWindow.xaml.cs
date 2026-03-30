using System.Windows;
using ChronosAD.ViewModels;

namespace ChronosAD.Views;

public partial class RegistrationWindow : Window
{
    private readonly RegistrationViewModel _vm;

    public RegistrationWindow(RegistrationViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void BtnRegister_Click(object sender, RoutedEventArgs e)
    {
        _vm.FirstName = TxtFirstName.Text;
        _vm.LastName = TxtLastName.Text;
        if (_vm.Register())
        {
            BtnRegister.IsEnabled = false;
            TxtError.Foreground = System.Windows.Media.Brushes.LightGreen;
            TxtError.Text = $"Welcome, {_vm.FirstName}! Taking you to the clock...";
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            timer.Tick += (_, _) => { timer.Stop(); DialogResult = true; Close(); };
            timer.Start();
        }
        else TxtError.Text = _vm.Error;
    }
}
