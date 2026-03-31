using Microsoft.Data.SqlClient;
using ChronosAD.Models;
using ChronosAD.Services;

namespace ChronosAD.Data;

public class DatabaseService
{
    private readonly string _connectionString;
    private readonly AuditLogger? _log;

    public DatabaseService(string connectionString, AuditLogger? logger = null)
    {
        _connectionString = connectionString;
        _log = logger;
    }

    private SqlConnection GetConnection() => new(_connectionString);

    // ── User Methods ──────────────────────────────────────────────────────────

    public User? GetUserBySID(string sid)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            "SELECT SID, FirstName, LastName, StartDate, IsManager, IsFrozen FROM Users WHERE SID = @SID", conn);
        cmd.Parameters.AddWithValue("@SID", sid);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new User
        {
            SID = reader.GetString(0),
            FirstName = reader.GetString(1),
            LastName = reader.GetString(2),
            StartDate = reader.GetDateTime(3),
            IsManager = reader.GetBoolean(4),
            IsFrozen = reader.GetBoolean(5)
        };
    }

    public void RegisterUser(User user)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            @"INSERT INTO Users (SID, FirstName, LastName, StartDate, IsManager, IsFrozen)
              VALUES (@SID, @FirstName, @LastName, @StartDate, 0, 0)", conn);
        cmd.Parameters.AddWithValue("@SID", user.SID);
        cmd.Parameters.AddWithValue("@FirstName", user.FirstName);
        cmd.Parameters.AddWithValue("@LastName", user.LastName);
        cmd.Parameters.AddWithValue("@StartDate", user.StartDate);
        cmd.ExecuteNonQuery();
        _log?.Log("USER_ADD", $"SID={user.SID} Name={user.FirstName} {user.LastName}");
    }

    public List<User> GetAllUsers()
    {
        var users = new List<User>();
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            "SELECT SID, FirstName, LastName, StartDate, IsManager, IsFrozen FROM Users ORDER BY LastName, FirstName", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            users.Add(new User
            {
                SID = reader.GetString(0),
                FirstName = reader.GetString(1),
                LastName = reader.GetString(2),
                StartDate = reader.GetDateTime(3),
                IsManager = reader.GetBoolean(4),
                IsFrozen = reader.GetBoolean(5)
            });
        }
        return users;
    }

    public void UpdateUser(User user)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            @"UPDATE Users SET FirstName=@FirstName, LastName=@LastName,
              StartDate=@StartDate, IsManager=@IsManager, IsFrozen=@IsFrozen
              WHERE SID=@SID", conn);
        cmd.Parameters.AddWithValue("@SID", user.SID);
        cmd.Parameters.AddWithValue("@FirstName", user.FirstName);
        cmd.Parameters.AddWithValue("@LastName", user.LastName);
        cmd.Parameters.AddWithValue("@StartDate", user.StartDate);
        cmd.Parameters.AddWithValue("@IsManager", user.IsManager);
        cmd.Parameters.AddWithValue("@IsFrozen", user.IsFrozen);
        cmd.ExecuteNonQuery();
        _log?.Log("USER_UPDATE", $"SID={user.SID} Name={user.FirstName} {user.LastName} IsManager={user.IsManager} IsFrozen={user.IsFrozen}");
    }

    public void DeleteUser(string sid)
    {
        using var conn = GetConnection();
        conn.Open();
        var user = GetUserBySID(sid);
        using var cmd = new SqlCommand("DELETE FROM Users WHERE SID=@SID", conn);
        cmd.Parameters.AddWithValue("@SID", sid);
        cmd.ExecuteNonQuery();
        _log?.Log("USER_DELETE", $"SID={sid} Name={user?.FirstName} {user?.LastName}");
    }

    // ── Punch Methods ─────────────────────────────────────────────────────────

    public bool IsUserClockedIn(string sid)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            "SELECT COUNT(1) FROM Punches WHERE UserSID=@SID AND ClockOutTime IS NULL", conn);
        cmd.Parameters.AddWithValue("@SID", sid);
        return (int)cmd.ExecuteScalar()! > 0;
    }

    public Punch? GetActiveSession(string sid)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            "SELECT PunchID, UserSID, ClockInTime, InNote FROM Punches WHERE UserSID=@SID AND ClockOutTime IS NULL", conn);
        cmd.Parameters.AddWithValue("@SID", sid);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new Punch
        {
            PunchID = reader.GetInt32(0),
            UserSID = reader.GetString(1),
            ClockInTime = reader.GetDateTime(2),
            InNote = reader.IsDBNull(3) ? null : reader.GetString(3)
        };
    }

    public int ClockIn(string sid, string? note = null)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            @"INSERT INTO Punches (UserSID, ClockInTime, InNote)
              OUTPUT INSERTED.PunchID
              VALUES (@SID, GETDATE(), @Note)", conn);
        cmd.Parameters.AddWithValue("@SID", sid);
        cmd.Parameters.AddWithValue("@Note", (object?)note ?? DBNull.Value);
        var punchId = (int)cmd.ExecuteScalar()!;
        _log?.Log("PUNCH_CLOCKIN", $"PunchID={punchId} UserSID={sid} Note={note ?? "(none)"}");
        return punchId;
    }

    public void ClockOut(int punchId, string? note = null, bool isAutoLogout = false)
    {
        using var conn = GetConnection();
        conn.Open();
        string? userSid = null;
        if (_log != null)
        {
            using var sel = new SqlCommand("SELECT UserSID FROM Punches WHERE PunchID=@id", conn);
            sel.Parameters.AddWithValue("@id", punchId);
            userSid = sel.ExecuteScalar() as string;
        }
        using var cmd = new SqlCommand(
            @"UPDATE Punches SET
                ClockOutTime = GETDATE(),
                Duration = DATEDIFF(SECOND, ClockInTime, GETDATE()) / 3600.0,
                OutNote = @Note,
                IsAutoLogout = @IsAuto
              WHERE PunchID = @PunchID", conn);
        cmd.Parameters.AddWithValue("@PunchID", punchId);
        cmd.Parameters.AddWithValue("@Note", (object?)note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsAuto", isAutoLogout);
        cmd.ExecuteNonQuery();
        _log?.Log("PUNCH_CLOCKOUT", $"PunchID={punchId} UserSID={userSid} Note={note ?? "(none)"} IsAuto={isAutoLogout}");
    }

    public List<Punch> GetPunchesForPeriod(string sid, DateTime periodStart, DateTime periodEnd)
    {
        var punches = new List<Punch>();
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            @"SELECT PunchID, UserSID, ClockInTime, ClockOutTime, Duration,
                     IsAutoLogout, InNote, OutNote, OriginalClockIn, OriginalClockOut, EditedBy, EditedAt
              FROM Punches
              WHERE UserSID=@SID AND ClockInTime >= @Start AND ClockInTime < @End
              ORDER BY ClockInTime DESC", conn);
        cmd.Parameters.AddWithValue("@SID", sid);
        cmd.Parameters.AddWithValue("@Start", periodStart);
        cmd.Parameters.AddWithValue("@End", periodEnd);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            punches.Add(new Punch
            {
                PunchID = reader.GetInt32(0),
                UserSID = reader.GetString(1),
                ClockInTime = reader.GetDateTime(2),
                ClockOutTime = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                Duration = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                IsAutoLogout = reader.GetBoolean(5),
                InNote = reader.IsDBNull(6) ? null : reader.GetString(6),
                OutNote = reader.IsDBNull(7) ? null : reader.GetString(7),
                OriginalClockIn = reader.IsDBNull(8) ? null : reader.GetString(8),
                OriginalClockOut = reader.IsDBNull(9) ? null : reader.GetString(9),
                EditedBy = reader.IsDBNull(10) ? null : reader.GetString(10),
                EditedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11)
            });
        }
        return punches;
    }

    public List<Punch> GetAllPunchesForPeriod(DateTime periodStart, DateTime periodEnd)
    {
        var punches = new List<Punch>();
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            @"SELECT PunchID, UserSID, ClockInTime, ClockOutTime, Duration,
                     IsAutoLogout, InNote, OutNote, OriginalClockIn, OriginalClockOut, EditedBy, EditedAt
              FROM Punches
              WHERE ClockInTime >= @Start AND ClockInTime < @End
              ORDER BY UserSID, ClockInTime DESC", conn);
        cmd.Parameters.AddWithValue("@Start", periodStart);
        cmd.Parameters.AddWithValue("@End", periodEnd);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            punches.Add(new Punch
            {
                PunchID = reader.GetInt32(0),
                UserSID = reader.GetString(1),
                ClockInTime = reader.GetDateTime(2),
                ClockOutTime = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                Duration = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                IsAutoLogout = reader.GetBoolean(5),
                InNote = reader.IsDBNull(6) ? null : reader.GetString(6),
                OutNote = reader.IsDBNull(7) ? null : reader.GetString(7),
                OriginalClockIn = reader.IsDBNull(8) ? null : reader.GetString(8),
                OriginalClockOut = reader.IsDBNull(9) ? null : reader.GetString(9),
                EditedBy = reader.IsDBNull(10) ? null : reader.GetString(10),
                EditedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11)
            });
        }
        return punches;
    }

    public void UpdatePunch(Punch punch, string editorSid)
    {
        using var conn = GetConnection();
        conn.Open();
        // Preserve original values on first edit
        using var checkCmd = new SqlCommand(
            "SELECT OriginalClockIn FROM Punches WHERE PunchID=@ID", conn);
        checkCmd.Parameters.AddWithValue("@ID", punch.PunchID);
        var existingOriginal = checkCmd.ExecuteScalar();

        using var cmd = new SqlCommand(
            @"UPDATE Punches SET
                ClockInTime = @ClockIn,
                ClockOutTime = @ClockOut,
                Duration = CASE WHEN @ClockOut IS NOT NULL
                           THEN DATEDIFF(SECOND, @ClockIn, @ClockOut) / 3600.0
                           ELSE NULL END,
                OriginalClockIn = CASE WHEN OriginalClockIn IS NULL THEN CONVERT(VARCHAR, ClockInTime, 120) ELSE OriginalClockIn END,
                OriginalClockOut = CASE WHEN OriginalClockOut IS NULL THEN CONVERT(VARCHAR, ClockOutTime, 120) ELSE OriginalClockOut END,
                EditedBy = @Editor,
                EditedAt = GETDATE()
              WHERE PunchID = @PunchID", conn);
        cmd.Parameters.AddWithValue("@PunchID", punch.PunchID);
        cmd.Parameters.AddWithValue("@ClockIn", punch.ClockInTime);
        cmd.Parameters.AddWithValue("@ClockOut", (object?)punch.ClockOutTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Editor", editorSid);
        cmd.ExecuteNonQuery();
        _log?.Log("PUNCH_EDIT", $"PunchID={punch.PunchID} UserSID={punch.UserSID} EditedBy={editorSid} NewClockIn={punch.ClockInTime:yyyy-MM-dd HH:mm} NewClockOut={punch.ClockOutTime?.ToString("yyyy-MM-dd HH:mm") ?? "(active)"}");
    }

    public void DeletePunch(int punchId)
    {
        // Capture details before deletion so they appear in the audit log
        using var conn = GetConnection();
        conn.Open();
        string? userSid = null; DateTime? clockIn = null; DateTime? clockOut = null;
        using (var sel = new SqlCommand("SELECT UserSID, ClockInTime, ClockOutTime FROM Punches WHERE PunchID=@id", conn))
        {
            sel.Parameters.AddWithValue("@id", punchId);
            using var r = sel.ExecuteReader();
            if (r.Read()) { userSid = r.GetString(0); clockIn = r.GetDateTime(1); clockOut = r.IsDBNull(2) ? null : r.GetDateTime(2); }
        }
        using var cmd = new SqlCommand("DELETE FROM Punches WHERE PunchID = @id", conn);
        cmd.Parameters.AddWithValue("@id", punchId);
        cmd.ExecuteNonQuery();
        _log?.Log("PUNCH_DELETE", $"PunchID={punchId} UserSID={userSid} ClockIn={clockIn?.ToString("yyyy-MM-dd HH:mm") ?? "?"} ClockOut={clockOut?.ToString("yyyy-MM-dd HH:mm") ?? "(active)"}");
    }

    public void AutoClockOutAllActive()
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            @"UPDATE Punches SET
                ClockOutTime = CAST(CAST(GETDATE() AS DATE) AS DATETIME),
                Duration = DATEDIFF(SECOND, ClockInTime, CAST(CAST(GETDATE() AS DATE) AS DATETIME)) / 3600.0,
                IsAutoLogout = 1
              WHERE ClockOutTime IS NULL", conn);
        cmd.ExecuteNonQuery();
        _log?.Log("AUTO_CLOCKOUT", "All active sessions clocked out");
    }

    // ── Message Methods ───────────────────────────────────────────────────────

    public void SendMessage(string sid, string message)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            @"INSERT INTO Messages (UserSID, Timestamp, EmployeeMessage, IsRead)
              VALUES (@SID, GETDATE(), @Message, 0)", conn);
        cmd.Parameters.AddWithValue("@SID", sid);
        cmd.Parameters.AddWithValue("@Message", message);
        cmd.ExecuteNonQuery();
        _log?.Log("MSG_SEND", $"UserSID={sid}");
    }

    public List<Message> GetMessagesForUser(string sid)
    {
        var messages = new List<Message>();
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            @"SELECT MessageID, UserSID, Timestamp, EmployeeMessage, ManagerResponse, IsRead
              FROM Messages WHERE UserSID=@SID ORDER BY Timestamp DESC", conn);
        cmd.Parameters.AddWithValue("@SID", sid);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            messages.Add(new Message
            {
                MessageID = reader.GetInt32(0),
                UserSID = reader.GetString(1),
                Timestamp = reader.GetDateTime(2),
                EmployeeMessage = reader.GetString(3),
                ManagerResponse = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsRead = reader.GetBoolean(5)
            });
        }
        return messages;
    }

    public List<Message> GetAllMessages()
    {
        var messages = new List<Message>();
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            @"SELECT m.MessageID, m.UserSID, m.Timestamp, m.EmployeeMessage, m.ManagerResponse, m.IsRead,
                     u.FirstName + ' ' + u.LastName AS UserName
              FROM Messages m
              JOIN Users u ON m.UserSID = u.SID
              ORDER BY m.Timestamp DESC", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            messages.Add(new Message
            {
                MessageID = reader.GetInt32(0),
                UserSID = reader.GetString(1),
                Timestamp = reader.GetDateTime(2),
                EmployeeMessage = reader.GetString(3),
                ManagerResponse = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsRead = reader.GetBoolean(5),
                UserName = reader.GetString(6)
            });
        }
        return messages;
    }

    public void RespondToMessage(int messageId, string response)
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            "UPDATE Messages SET ManagerResponse=@Response, IsRead=1 WHERE MessageID=@ID", conn);
        cmd.Parameters.AddWithValue("@Response", response);
        cmd.Parameters.AddWithValue("@ID", messageId);
        cmd.ExecuteNonQuery();
        _log?.Log("MSG_RESPOND", $"MessageID={messageId}");
    }

    // ── Config Methods ────────────────────────────────────────────────────────

    public Config GetConfig()
    {
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand("SELECT PayPeriodStartDate, HolidayDates FROM Config", conn);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return new Config { PayPeriodStartDate = DateTime.Today };
        var config = new Config { PayPeriodStartDate = reader.GetDateTime(0) };
        if (!reader.IsDBNull(1))
        {
            var holidayStr = reader.GetString(1);
            config.HolidayDates = holidayStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => DateTime.Parse(d.Trim()))
                .ToList();
        }
        return config;
    }

    public void SaveConfig(Config config)
    {
        using var conn = GetConnection();
        conn.Open();
        var holidayStr = string.Join(",", config.HolidayDates.Select(d => d.ToString("yyyy-MM-dd")));
        using var cmd = new SqlCommand(
            "UPDATE Config SET PayPeriodStartDate=@Start, HolidayDates=@Holidays", conn);
        cmd.Parameters.AddWithValue("@Start", config.PayPeriodStartDate);
        cmd.Parameters.AddWithValue("@Holidays", holidayStr);
        cmd.ExecuteNonQuery();
        _log?.Log("CONFIG_UPDATE", $"PayPeriodStart={config.PayPeriodStartDate:yyyy-MM-dd} HolidayCount={config.HolidayDates.Count}");
    }

    // ── Archive Methods ───────────────────────────────────────────────────────

    public void ArchiveOldPunches()
    {
        using var conn = GetConnection();
        conn.Open();
        var cutoff = DateTime.Today.AddYears(-1);
        using var insertCmd = new SqlCommand(
            @"INSERT INTO History_Archive
              SELECT * FROM Punches WHERE ClockInTime < @Cutoff", conn);
        insertCmd.Parameters.AddWithValue("@Cutoff", cutoff);
        insertCmd.ExecuteNonQuery();

        using var deleteCmd = new SqlCommand(
            "DELETE FROM Punches WHERE ClockInTime < @Cutoff", conn);
        deleteCmd.Parameters.AddWithValue("@Cutoff", cutoff);
        deleteCmd.ExecuteNonQuery();
        _log?.Log("ARCHIVE", $"Records older than {cutoff:yyyy-MM-dd} moved to History_Archive");
    }

    public List<Punch> ExportPunches(DateTime from, DateTime to)
    {
        var punches = new List<Punch>();
        using var conn = GetConnection();
        conn.Open();
        using var cmd = new SqlCommand(
            @"SELECT PunchID, UserSID, ClockInTime, ClockOutTime, Duration,
                     IsAutoLogout, InNote, OutNote
              FROM Punches
              WHERE ClockInTime >= @From AND ClockInTime < @To
              UNION ALL
              SELECT PunchID, UserSID, ClockInTime, ClockOutTime, Duration,
                     IsAutoLogout, InNote, OutNote
              FROM History_Archive
              WHERE ClockInTime >= @From AND ClockInTime < @To
              ORDER BY ClockInTime", conn);
        cmd.Parameters.AddWithValue("@From", from);
        cmd.Parameters.AddWithValue("@To", to);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            punches.Add(new Punch
            {
                PunchID = reader.GetInt32(0),
                UserSID = reader.GetString(1),
                ClockInTime = reader.GetDateTime(2),
                ClockOutTime = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                Duration = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                IsAutoLogout = reader.GetBoolean(5),
                InNote = reader.IsDBNull(6) ? null : reader.GetString(6),
                OutNote = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }
        return punches;
    }
}
