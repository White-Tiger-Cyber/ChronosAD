namespace ChronosAD.Models;

public class Config
{
    public DateTime PayPeriodStartDate { get; set; }
    public List<DateTime> HolidayDates { get; set; } = new();
}
