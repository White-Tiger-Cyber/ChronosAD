using ChronosAD.Data;
using ChronosAD.Models;
using ChronosAD.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace ChronosAD.ViewModels;

public class EmployeeStatus
{
    public User User { get; set; } = null!;
    public bool IsClockedIn { get; set; }
    public double TotalHours { get; set; }
    public string TotalHoursDisplay => $"{(int)TotalHours}h {(int)((TotalHours % 1) * 60)}m";
    public string StatusDisplay => IsClockedIn ? "In" : "Out";
    public string IsManagerDisplay => User.IsManager ? "Yes" : "No";
}

public record PayPeriodEntry(DateTime Start, DateTime End)
{
    public string Display => $"{Start:MMM d} – {End:MMM d, yyyy}";
}

public class ManagementViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly PunchService _punchService;
    private readonly MessageService _messageService;
    private readonly string _currentManagerSid;

    private ObservableCollection<EmployeeStatus> _employees = new();
    private EmployeeStatus? _selectedEmployee;
    private List<Punch> _selectedPunches = new();
    private List<Message> _allMessages = new();
    private Config _config = new();
    private DateTime _periodStart;
    private DateTime _periodEnd;
    private PayPeriodEntry? _selectedHistoryPeriod;
    private EmployeeStatus? _selectedHistoryEmployee;
    private List<Punch> _historyPunches = new();
    private string _historyPeriodSummary = "";

    public ObservableCollection<EmployeeStatus> Employees { get => _employees; set => Set(ref _employees, value); }
    public EmployeeStatus? SelectedEmployee { get => _selectedEmployee; set { Set(ref _selectedEmployee, value); LoadSelectedEmployeePunches(); } }
    public List<Punch> SelectedPunches { get => _selectedPunches; set => Set(ref _selectedPunches, value); }
    public List<Message> AllMessages { get => _allMessages; set => Set(ref _allMessages, value); }
    public Config Config { get => _config; set => Set(ref _config, value); }
    public DateTime PeriodStart => _periodStart;
    public DateTime PeriodEnd => _periodEnd;
    public bool IsConfigSaved { get; private set; }
    public List<PayPeriodEntry> PayPeriodList { get; private set; } = new();
    public PayPeriodEntry? SelectedHistoryPeriod { get => _selectedHistoryPeriod; set { Set(ref _selectedHistoryPeriod, value); LoadHistoryPunches(); } }
    public EmployeeStatus? SelectedHistoryEmployee { get => _selectedHistoryEmployee; set { Set(ref _selectedHistoryEmployee, value); LoadHistoryPunches(); } }
    public List<Punch> HistoryPunches { get => _historyPunches; set => Set(ref _historyPunches, value); }
    public string HistoryPeriodSummary { get => _historyPeriodSummary; set => Set(ref _historyPeriodSummary, value); }
    public string SelectedEmployeePeriodSummary =>
        SelectedEmployee == null ? "" :
        $"Current Period: {SelectedEmployee.TotalHoursDisplay}  ({_periodStart:MMM d} – {_periodEnd:MMM d})";

    public ManagementViewModel(DatabaseService db, PunchService punchService, MessageService messageService, string managerSid)
    {
        _db = db;
        _punchService = punchService;
        _messageService = messageService;
        _currentManagerSid = managerSid;
        Refresh();
    }

    public void Refresh()
    {
        (_periodStart, _periodEnd) = _punchService.GetCurrentPayPeriod();
        Config = _db.GetConfig();
        IsConfigSaved = _db.IsConfigSaved();
        PayPeriodList = IsConfigSaved ? BuildPayPeriodList() : new();

        var users = _db.GetAllUsers();
        var statuses = new ObservableCollection<EmployeeStatus>();
        foreach (var user in users)
        {
            var punches = _db.GetPunchesForPeriod(user.SID, _periodStart, _periodEnd);
            statuses.Add(new EmployeeStatus
            {
                User = user,
                IsClockedIn = _db.IsUserClockedIn(user.SID),
                TotalHours = _punchService.CalculateTotalHours(punches)
            });
        }
        Employees = statuses;
        AllMessages = _messageService.GetAll();
    }

    private void LoadSelectedEmployeePunches()
    {
        if (_selectedEmployee == null) { SelectedPunches = new(); return; }
        SelectedPunches = _db.GetPunchesForPeriod(_selectedEmployee.User.SID, _periodStart, _periodEnd);
    }

    public void SavePunchEdit(Punch punch) => _db.UpdatePunch(punch, _currentManagerSid);
    public void DeletePunch(Punch punch) { _db.DeletePunch(punch.PunchID); LoadSelectedEmployeePunches(); }
    public void AddPunch(DateTime clockIn, DateTime? clockOut, string? note)
    {
        if (SelectedEmployee == null) return;
        _db.AddPunch(SelectedEmployee.User.SID, clockIn, clockOut, note, _currentManagerSid);
        LoadSelectedEmployeePunches();
    }
    public void SaveConfig(Config config) => _db.SaveConfig(config);
    public void RespondToMessage(int messageId, string response) => _messageService.Respond(messageId, response);
    public void DeleteMessage(int messageId) { _db.DeleteMessage(messageId); AllMessages = _messageService.GetAll(); }
    public void FreezeUser(string sid) { var u = _db.GetUserBySID(sid); if (u != null) { u.IsFrozen = !u.IsFrozen; _db.UpdateUser(u); Refresh(); } }
    public void DeleteUser(string sid) { _db.DeleteUser(sid); Refresh(); }
    public void PromoteUser(string sid) { var u = _db.GetUserBySID(sid); if (u != null) { u.IsManager = !u.IsManager; _db.UpdateUser(u); Refresh(); } }
    public void RunAutoClockOut() { _db.AutoClockOutAllActive(); Refresh(); }
    public void ArchiveData() { _db.ArchiveOldPunches(); Refresh(); }

    public void ExportToCSV(string filePath)
    {
        var from = _periodStart.AddYears(-1);
        var punches = _db.ExportPunches(from, _periodEnd);
        var nameMap = _db.GetAllUsers().ToDictionary(u => u.SID, u => u.FullName);
        var lines = new List<string> { "PunchID,EmployeeName,ClockIn,ClockOut,Duration,IsAutoLogout,InNote,OutNote" };
        foreach (var p in punches)
        {
            var name = nameMap.TryGetValue(p.UserSID, out var n) ? n : p.UserSID;
            lines.Add($"{p.PunchID},{name},{p.ClockInTime:yyyy-MM-dd HH:mm},{p.ClockOutDisplay},{p.Duration:F2},{p.IsAutoLogout},{p.InNote},{p.OutNote}");
        }
        File.WriteAllLines(filePath, lines);
    }

    public void SilentArchive() => _db.ArchiveOldPunches();

    private List<PayPeriodEntry> BuildPayPeriodList()
    {
        var config = _db.GetConfig();
        var list = new List<PayPeriodEntry>();
        var start = config.PayPeriodStartDate.Date;
        while (start < DateTime.Today)
        {
            var end = start.AddDays(14);
            list.Add(new PayPeriodEntry(start, end));
            start = end;
        }
        list.Reverse(); // newest first
        return list;
    }

    private void LoadHistoryPunches()
    {
        if (_selectedHistoryEmployee == null || _selectedHistoryPeriod == null)
        {
            HistoryPunches = new();
            HistoryPeriodSummary = "";
            return;
        }
        HistoryPunches = _db.GetPunchesForPeriodAllSources(
            _selectedHistoryEmployee.User.SID,
            _selectedHistoryPeriod.Start,
            _selectedHistoryPeriod.End);
        var total = _punchService.CalculateTotalHours(HistoryPunches);
        HistoryPeriodSummary = $"Total: {(int)total}h {(int)((total % 1) * 60)}m  ·  {_selectedHistoryPeriod.Display}";
    }
}
