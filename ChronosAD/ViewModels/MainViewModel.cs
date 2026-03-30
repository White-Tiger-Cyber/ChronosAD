using ChronosAD.Data;
using ChronosAD.Models;
using ChronosAD.Services;

namespace ChronosAD.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly PunchService _punchService;
    private readonly MessageService _messageService;

    public User CurrentUser { get; }

    private bool _isClockedIn;
    private string _statusText = string.Empty;
    private string _totalHoursText = string.Empty;
    private List<Punch> _punches = new();
    private List<Message> _messages = new();
    private string _latestMessageDisplay = string.Empty;
    private DateTime _periodStart;
    private DateTime _periodEnd;

    public bool IsClockedIn { get => _isClockedIn; set => Set(ref _isClockedIn, value); }
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
    public string TotalHoursText { get => _totalHoursText; set => Set(ref _totalHoursText, value); }
    public List<Punch> Punches { get => _punches; set => Set(ref _punches, value); }
    public List<Message> Messages { get => _messages; set => Set(ref _messages, value); }
    public string LatestMessageDisplay { get => _latestMessageDisplay; set => Set(ref _latestMessageDisplay, value); }
    public DateTime PeriodStart => _periodStart;
    public DateTime PeriodEnd => _periodEnd;

    public MainViewModel(DatabaseService db, PunchService punchService, MessageService messageService, User user)
    {
        _db = db;
        _punchService = punchService;
        _messageService = messageService;
        CurrentUser = user;
        Refresh();
    }

    public void Refresh()
    {
        (_periodStart, _periodEnd) = _punchService.GetCurrentPayPeriod();
        IsClockedIn = _db.IsUserClockedIn(CurrentUser.SID);
        StatusText = IsClockedIn ? "Clocked In" : "Clocked Out";

        Punches = _db.GetPunchesForPeriod(CurrentUser.SID, _periodStart, _periodEnd);
        var total = _punchService.CalculateTotalHours(Punches);
        TotalHoursText = $"{(int)total}h {(int)((total % 1) * 60)}m";

        Messages = _messageService.GetForUser(CurrentUser.SID);
        var latest = Messages.FirstOrDefault();
        if (latest != null)
        {
            LatestMessageDisplay = $"[{latest.Timestamp:MM/dd HH:mm}] You: {latest.EmployeeMessage}";
            if (latest.ManagerResponse != null)
                LatestMessageDisplay += $"\nManager: {latest.ManagerResponse}";
        }
        else LatestMessageDisplay = "No messages.";
    }

    public bool TogglePunch(string? note, out string error)
    {
        bool result;
        if (!IsClockedIn)
            result = _punchService.TryClockIn(CurrentUser.SID, note, out error);
        else
            result = _punchService.TryClockOut(CurrentUser.SID, note, out error);

        if (result) Refresh();
        return result;
    }

    public void SendMessage(string msg) => _messageService.Send(CurrentUser.SID, msg);
}
