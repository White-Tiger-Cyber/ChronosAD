namespace ChronosAD.Models;

public class Punch
{
    public int PunchID { get; set; }
    public string UserSID { get; set; } = string.Empty;
    public DateTime ClockInTime { get; set; }
    public DateTime? ClockOutTime { get; set; }
    public double? Duration { get; set; }
    public bool IsAutoLogout { get; set; }
    public string? InNote { get; set; }
    public string? OutNote { get; set; }
    public string? OriginalClockIn { get; set; }
    public string? OriginalClockOut { get; set; }
    public string? EditedBy { get; set; }
    public DateTime? EditedAt { get; set; }

    public string DurationDisplay => Duration.HasValue
        ? $"{(int)Duration.Value}h {(int)((Duration.Value % 1) * 60)}m"
        : "Active";

    public string ClockOutDisplay => ClockOutTime.HasValue
        ? ClockOutTime.Value.ToString("MM/dd/yyyy hh:mm tt")
        : "—";
}
