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

    public ObservableCollection<EmployeeStatus> Employees { get => _employees; set => Set(ref _employees, value); }
    public EmployeeStatus? SelectedEmployee { get => _selectedEmployee; set { Set(ref _selectedEmployee, value); LoadSelectedEmployeePunches(); } }
    public List<Punch> SelectedPunches { get => _selectedPunches; set => Set(ref _selectedPunches, value); }
    public List<Message> AllMessages { get => _allMessages; set => Set(ref _allMessages, value); }
    public Config Config { get => _config; set => Set(ref _config, value); }
    public DateTime PeriodStart => _periodStart;
    public DateTime PeriodEnd => _periodEnd;

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
    public void SaveConfig(Config config) => _db.SaveConfig(config);
    public void RespondToMessage(int messageId, string response) => _messageService.Respond(messageId, response);
    public void FreezeUser(string sid) { var u = _db.GetUserBySID(sid); if (u != null) { u.IsFrozen = !u.IsFrozen; _db.UpdateUser(u); Refresh(); } }
    public void DeleteUser(string sid) { _db.DeleteUser(sid); Refresh(); }
    public void PromoteUser(string sid) { var u = _db.GetUserBySID(sid); if (u != null) { u.IsManager = !u.IsManager; _db.UpdateUser(u); Refresh(); } }
    public void RunAutoClockOut() { _db.AutoClockOutAllActive(); Refresh(); }
    public void ArchiveData() { _db.ArchiveOldPunches(); Refresh(); }

    public void ExportToCSV(string filePath)
    {
        var from = _periodStart.AddYears(-1);
        var punches = _db.ExportPunches(from, _periodEnd);
        var lines = new List<string> { "PunchID,UserSID,ClockIn,ClockOut,Duration,IsAutoLogout,InNote,OutNote" };
        foreach (var p in punches)
            lines.Add($"{p.PunchID},{p.UserSID},{p.ClockInTime:yyyy-MM-dd HH:mm},{p.ClockOutDisplay},{p.Duration:F2},{p.IsAutoLogout},{p.InNote},{p.OutNote}");
        File.WriteAllLines(filePath, lines);
    }
}
