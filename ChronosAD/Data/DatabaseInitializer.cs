using Microsoft.Data.SqlClient;

namespace ChronosAD.Data;

public static class DatabaseInitializer
{
    public static void Initialize(string connectionString)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();

        var sql = @"
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
CREATE TABLE Users (
    SID NVARCHAR(256) PRIMARY KEY,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    StartDate DATE NOT NULL DEFAULT GETDATE(),
    IsManager BIT NOT NULL DEFAULT 0,
    IsFrozen BIT NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Punches' AND xtype='U')
CREATE TABLE Punches (
    PunchID INT IDENTITY(1,1) PRIMARY KEY,
    UserSID NVARCHAR(256) NOT NULL REFERENCES Users(SID) ON DELETE CASCADE,
    ClockInTime DATETIME NOT NULL DEFAULT GETDATE(),
    ClockOutTime DATETIME NULL,
    Duration FLOAT NULL,
    IsAutoLogout BIT NOT NULL DEFAULT 0,
    InNote NVARCHAR(MAX) NULL,
    OutNote NVARCHAR(MAX) NULL,
    OriginalClockIn NVARCHAR(50) NULL,
    OriginalClockOut NVARCHAR(50) NULL,
    EditedBy NVARCHAR(256) NULL,
    EditedAt DATETIME NULL
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Messages' AND xtype='U')
CREATE TABLE Messages (
    MessageID INT IDENTITY(1,1) PRIMARY KEY,
    UserSID NVARCHAR(256) NOT NULL REFERENCES Users(SID) ON DELETE CASCADE,
    Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
    EmployeeMessage NVARCHAR(MAX) NOT NULL,
    ManagerResponse NVARCHAR(MAX) NULL,
    IsRead BIT NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Config' AND xtype='U')
BEGIN
    CREATE TABLE Config (
        PayPeriodStartDate DATE NOT NULL,
        HolidayDates NVARCHAR(MAX) NULL
    );
    INSERT INTO Config (PayPeriodStartDate) VALUES (CAST(GETDATE() AS DATE));
END

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='History_Archive' AND xtype='U')
CREATE TABLE History_Archive (
    PunchID INT PRIMARY KEY,
    UserSID NVARCHAR(256) NOT NULL,
    ClockInTime DATETIME NOT NULL,
    ClockOutTime DATETIME NULL,
    Duration FLOAT NULL,
    IsAutoLogout BIT NOT NULL DEFAULT 0,
    InNote NVARCHAR(MAX) NULL,
    OutNote NVARCHAR(MAX) NULL,
    OriginalClockIn NVARCHAR(50) NULL,
    OriginalClockOut NVARCHAR(50) NULL,
    EditedBy NVARCHAR(256) NULL,
    EditedAt DATETIME NULL
);
";
        using var cmd = new SqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }
}
