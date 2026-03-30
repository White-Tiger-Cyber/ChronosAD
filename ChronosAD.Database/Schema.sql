-- ChronosAD Database Schema
-- Run on SQL Server Express hosted on Windows Server 2025

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ChronosAD')
    CREATE DATABASE ChronosAD;
GO

USE ChronosAD;
GO

-- Users Table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
CREATE TABLE Users (
    SID          NVARCHAR(256) PRIMARY KEY,
    FirstName    NVARCHAR(100) NOT NULL,
    LastName     NVARCHAR(100) NOT NULL,
    StartDate    DATE          NOT NULL DEFAULT GETDATE(),
    IsManager    BIT           NOT NULL DEFAULT 0,
    IsFrozen     BIT           NOT NULL DEFAULT 0
);
GO

-- Punches Table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Punches' AND xtype='U')
CREATE TABLE Punches (
    PunchID          INT IDENTITY(1,1) PRIMARY KEY,
    UserSID          NVARCHAR(256) NOT NULL REFERENCES Users(SID) ON DELETE CASCADE,
    ClockInTime      DATETIME      NOT NULL DEFAULT GETDATE(),
    ClockOutTime     DATETIME      NULL,
    Duration         FLOAT         NULL,
    IsAutoLogout     BIT           NOT NULL DEFAULT 0,
    InNote           NVARCHAR(MAX) NULL,
    OutNote          NVARCHAR(MAX) NULL,
    OriginalClockIn  NVARCHAR(50)  NULL,
    OriginalClockOut NVARCHAR(50)  NULL,
    EditedBy         NVARCHAR(256) NULL,
    EditedAt         DATETIME      NULL
);
GO

-- Messages Table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Messages' AND xtype='U')
CREATE TABLE Messages (
    MessageID       INT IDENTITY(1,1) PRIMARY KEY,
    UserSID         NVARCHAR(256) NOT NULL REFERENCES Users(SID) ON DELETE CASCADE,
    Timestamp       DATETIME      NOT NULL DEFAULT GETDATE(),
    EmployeeMessage NVARCHAR(MAX) NOT NULL,
    ManagerResponse NVARCHAR(MAX) NULL,
    IsRead          BIT           NOT NULL DEFAULT 0
);
GO

-- Config Table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Config' AND xtype='U')
BEGIN
    CREATE TABLE Config (
        PayPeriodStartDate DATE          NOT NULL,
        HolidayDates       NVARCHAR(MAX) NULL
    );
    INSERT INTO Config (PayPeriodStartDate) VALUES (CAST(GETDATE() AS DATE));
END
GO

-- History Archive Table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='History_Archive' AND xtype='U')
CREATE TABLE History_Archive (
    PunchID          INT           PRIMARY KEY,
    UserSID          NVARCHAR(256) NOT NULL,
    ClockInTime      DATETIME      NOT NULL,
    ClockOutTime     DATETIME      NULL,
    Duration         FLOAT         NULL,
    IsAutoLogout     BIT           NOT NULL DEFAULT 0,
    InNote           NVARCHAR(MAX) NULL,
    OutNote          NVARCHAR(MAX) NULL,
    OriginalClockIn  NVARCHAR(50)  NULL,
    OriginalClockOut NVARCHAR(50)  NULL,
    EditedBy         NVARCHAR(256) NULL,
    EditedAt         DATETIME      NULL
);
GO

-- SQL Server Agent Job for Midnight Auto Clock-Out
-- (Requires SQL Server Agent; adjust for Express using Task Scheduler + stored procedure)
CREATE OR ALTER PROCEDURE sp_MidnightAutoClockOut
AS
BEGIN
    UPDATE Punches
    SET
        ClockOutTime = CAST(CAST(GETDATE() AS DATE) AS DATETIME),
        Duration = DATEDIFF(SECOND, ClockInTime, CAST(CAST(GETDATE() AS DATE) AS DATETIME)) / 3600.0,
        IsAutoLogout = 1
    WHERE ClockOutTime IS NULL;
END;
GO
