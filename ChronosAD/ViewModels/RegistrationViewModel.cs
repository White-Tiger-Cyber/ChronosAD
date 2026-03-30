using ChronosAD.Data;
using ChronosAD.Models;

namespace ChronosAD.ViewModels;

public class RegistrationViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly string _sid;

    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _error = string.Empty;

    public string FirstName { get => _firstName; set => Set(ref _firstName, value); }
    public string LastName { get => _lastName; set => Set(ref _lastName, value); }
    public string Error { get => _error; set => Set(ref _error, value); }

    public RegistrationViewModel(DatabaseService db, string sid)
    {
        _db = db;
        _sid = sid;
    }

    public bool Register()
    {
        if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
        {
            Error = "First and last name are required.";
            return false;
        }
        _db.RegisterUser(new User
        {
            SID = _sid,
            FirstName = FirstName.Trim(),
            LastName = LastName.Trim(),
            StartDate = DateTime.Today
        });
        return true;
    }
}
