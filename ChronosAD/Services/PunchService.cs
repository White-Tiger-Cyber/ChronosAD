using ChronosAD.Data;
using ChronosAD.Models;

namespace ChronosAD.Services;

public class PunchService
{
    private readonly DatabaseService _db;

    public PunchService(DatabaseService db) => _db = db;

    public (DateTime PeriodStart, DateTime PeriodEnd) GetCurrentPayPeriod()
    {
        var config = _db.GetConfig();
        var today = DateTime.Today;
        var start = config.PayPeriodStartDate;

        // Walk forward in 14-day increments until we pass today
        while (start.AddDays(14) <= today)
            start = start.AddDays(14);

        return (start, start.AddDays(14));
    }

    public double CalculateTotalHours(List<Punch> punches)
        => punches.Where(p => p.Duration.HasValue).Sum(p => Math.Max(0, p.Duration!.Value));

    public bool TryClockIn(string sid, string? note, out string error)
    {
        error = string.Empty;
        var user = _db.GetUserBySID(sid);
        if (user == null) { error = "User not registered."; return false; }
        if (user.IsFrozen) { error = "Your account is frozen. Contact your manager."; return false; }
        if (_db.IsUserClockedIn(sid)) { error = "You are already clocked in on another workstation."; return false; }
        _db.ClockIn(sid, note);
        return true;
    }

    public bool TryClockOut(string sid, string? note, out string error)
    {
        error = string.Empty;
        var session = _db.GetActiveSession(sid);
        if (session == null) { error = "No active session found."; return false; }
        _db.ClockOut(session.PunchID, note);
        return true;
    }
}
