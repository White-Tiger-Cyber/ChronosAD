namespace ChronosAD.Models;

public class User
{
    public string SID { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public DateTime StartDate { get; set; }
    public bool IsManager { get; set; }
    public bool IsFrozen { get; set; }
    public bool IsClockedIn { get; set; }
    public double TotalHoursThisPeriod { get; set; }
}
