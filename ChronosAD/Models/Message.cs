namespace ChronosAD.Models;

public class Message
{
    public int MessageID { get; set; }
    public string UserSID { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public DateTime Timestamp { get; set; }
    public string EmployeeMessage { get; set; } = string.Empty;
    public string? ManagerResponse { get; set; }
    public bool IsRead { get; set; }
}
