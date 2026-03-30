# ChronosAD — Build & Deploy Guide

## Overview

| Where | What |
|-------|------|
| **Dev machine** | Build once with `dotnet publish`, producing the `publish/` folder |
| **Client server** | Install SQL Server Express, create the database, set permissions |
| **Each workstation** | Copy `publish/` folder, create shortcut |

---

## Part A — Build (on your dev machine, before going on-site)

### A1. Prerequisites

.NET 10 SDK must be installed (already done if you followed setup):
```powershell
dotnet --list-sdks
# Expected: 10.0.xxx listed
```

### A2. Build

From an elevated PowerShell or command prompt:
```powershell
cd D:\Documents\WhiteTiger\ChronosAD

dotnet publish ChronosAD\ChronosAD.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output .\publish
```

Output folder: `D:\Documents\WhiteTiger\ChronosAD\publish\`

Verify these two files are present before going on-site:
```powershell
dir .\publish\ChronosAD.exe
dir .\publish\appsettings.json
```

> **Why self-contained?** No .NET runtime needs to be installed on workstations — everything is bundled.

---

## Part B — Server Setup (on client's server via RDP)

Run all commands in an **elevated PowerShell** session.

### B1. Install SQL Server Express 2022

```powershell
winget install Microsoft.SQLServer.2022.Express
```

The installer will run automatically and select Basic configuration. When complete you will see `Installation has completed successfully!` with the instance name `SQLEXPRESS` confirmed.

### B2. Verify SQL Server is running

```powershell
Get-Service -Name "MSSQL`$SQLEXPRESS"
# Status should be: Running

# If stopped:
Start-Service "MSSQL`$SQLEXPRESS"

# Set to auto-start on boot:
Set-Service -Name "MSSQL`$SQLEXPRESS" -StartupType Automatic
```

### B3. Start SQL Server Browser

SQL Server Browser resolves the named instance (`SATURN\SQLEXPRESS`) for remote workstations. It is stopped and disabled by default — this must be enabled or workstations will fail to connect even if port 1433 is open.

```powershell
Set-Service "SQLBrowser" -StartupType Automatic
Start-Service "SQLBrowser"
```

### B4. Enable TCP/IP on port 1433

Workstations connect over TCP — this is off by default and must be turned on.

1. Open **SQL Server Configuration Manager** (search in Start)
2. Expand **SQL Server Network Configuration** → **Protocols for SQLEXPRESS**
3. Right-click **TCP/IP** → **Enable**
4. Right-click **TCP/IP** → **Properties** → **IP Addresses** tab → scroll to **IPAll**
5. Set **TCP Port** to `1433`, clear **TCP Dynamic Ports** (leave blank)
6. Click OK

Restart the service to apply:
```powershell
Restart-Service "MSSQL`$SQLEXPRESS"
```

### B5. Open firewall ports

Two ports are required — TCP 1433 for SQL Server, and UDP 1434 for SQL Server Browser (instance name resolution). Both must be open or workstations will fail to connect.

```powershell
New-NetFirewallRule -DisplayName "SQL Server Express 1433" `
    -Direction Inbound -Protocol TCP -LocalPort 1433 -Action Allow

New-NetFirewallRule -DisplayName "SQL Server Browser 1434" `
    -Direction Inbound -Protocol UDP -LocalPort 1434 -Action Allow
```

### B6. Verify sqlcmd is available

`sqlcmd` is bundled with SQL Server Express 2022 — no separate install is needed. Verify it is on PATH:

```powershell
sqlcmd -?
# Should print usage help
```

> **Note:** All `sqlcmd` commands in this guide use `tcp:localhost,1433` rather than `.\SQLEXPRESS`. By default sqlcmd uses named pipes for local connections, but named pipes is disabled on this instance. Forcing TCP avoids timeout errors.

### B7. Create the database and schema

```powershell
# Create the database
sqlcmd -S "tcp:localhost,1433" -E -Q "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name='ChronosAD') CREATE DATABASE ChronosAD;"

# Verify
sqlcmd -S "tcp:localhost,1433" -E -Q "SELECT name FROM sys.databases WHERE name='ChronosAD';"
# Expected output: ChronosAD
```

Copy `ChronosAD.Database\Schema.sql` from your dev machine to the server (e.g. via RDP file transfer), then run it:
```powershell
sqlcmd -S "tcp:localhost,1433" -E -d ChronosAD -i "C:\ChronosAD\Schema.sql"
```

> The app's `DatabaseInitializer` will also create tables on first run if they don't exist, so this step is a belt-and-suspenders check.

### B8. Grant SQL permissions to domain users

Replace `DOMAIN` with the actual AD domain name (e.g. `RLTNAV`).

```powershell
sqlcmd -S "tcp:localhost,1433" -E -d ChronosAD -Q "
CREATE LOGIN [DOMAIN\Domain Users] FROM WINDOWS;
CREATE USER [DOMAIN\Domain Users] FOR LOGIN [DOMAIN\Domain Users];
ALTER ROLE db_datareader ADD MEMBER [DOMAIN\Domain Users];
ALTER ROLE db_datawriter ADD MEMBER [DOMAIN\Domain Users];
GRANT EXECUTE TO [DOMAIN\Domain Users];
"
```

### B9. Note the server hostname

```powershell
hostname
# e.g. SATURN
```

You'll need this in Part C.

### B10. Set up midnight auto clock-out (optional but recommended)

The stored procedure `sp_MidnightAutoClockOut` (created by Schema.sql) clocks out anyone still active at midnight. Schedule it with Task Scheduler:

```powershell
$action  = New-ScheduledTaskAction -Execute "sqlcmd" `
    -Argument '-S "tcp:localhost,1433" -E -d ChronosAD -Q "EXEC sp_MidnightAutoClockOut"'
$trigger = New-ScheduledTaskTrigger -Daily -At "11:59PM"
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -RunLevel Highest
Register-ScheduledTask -TaskName "ChronosAD Midnight Clock-Out" `
    -Action $action -Trigger $trigger -Principal $principal
```

---

## Part C — Configure appsettings.json (on your dev machine)

Before copying to workstations, update `appsettings.json` in the `publish/` folder with the server hostname you noted in B9:

```json
{
  "ConnectionString": "Server=SATURN\\SQLEXPRESS;Database=ChronosAD;Integrated Security=True;TrustServerCertificate=True;"
}
```

> **Important:** The double backslash `\\` is required in JSON. Replace `SATURN` with your actual server hostname.
>
> For future clients: just change this one file — no rebuild needed.

---

## Part D — Workstation Deployment

### D1. Copy the publish folder

On each workstation, copy the entire `publish/` folder to:
```
C:\Program Files\ChronosAD\
```

All files must stay together in the same folder — do not move `ChronosAD.exe` separately from its DLLs and `appsettings.json`.

### D2. Create a desktop shortcut

```powershell
$shell    = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut("$env:PUBLIC\Desktop\ChronosAD.lnk")
$shortcut.TargetPath  = "C:\Program Files\ChronosAD\ChronosAD.exe"
$shortcut.Description = "ChronosAD Timeclock"
$shortcut.Save()
```

This places the shortcut on the desktop for all users on that machine.

### D3. First run

Each user who hasn't registered before will see the **Registration** window when they first launch the app. They enter their first and last name — their Windows SID is captured automatically.

---

## Part E — First Manager Setup

After the first user registers, promote them to manager. Run this on the server:

```powershell
# Promote the first registered user
sqlcmd -S "tcp:localhost,1433" -E -d ChronosAD -Q "
UPDATE Users SET IsManager = 1 WHERE SID = (SELECT TOP 1 SID FROM Users ORDER BY StartDate ASC);
"

# Verify
sqlcmd -S "tcp:localhost,1433" -E -d ChronosAD -Q "
SELECT SID, FirstName, LastName, IsManager FROM Users;
"
```

That manager can then promote others from within the Management Console.

---

## Quick Reference

```powershell
# Verify SQL Server is running (server)
Get-Service "MSSQL`$SQLEXPRESS"

# Create database (server)
sqlcmd -S "tcp:localhost,1433" -E -Q "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name='ChronosAD') CREATE DATABASE ChronosAD;"

# Build (dev machine)
cd D:\Documents\WhiteTiger\ChronosAD
dotnet publish ChronosAD\ChronosAD.csproj --configuration Release --runtime win-x64 --self-contained true --output .\publish

# Promote first manager (server, after first user registers)
sqlcmd -S "tcp:localhost,1433" -E -d ChronosAD -Q "UPDATE Users SET IsManager=1 WHERE SID=(SELECT TOP 1 SID FROM Users ORDER BY StartDate ASC);"
ex.
sqlcmd -S "tcp:localhost,1433" -E -d ChronosAD -Q "UPDATE Users SET IsManager = 1 WHERE FirstName = 'Amanda' AND LastName = 'Baker';"
```

---

## Troubleshooting

| Symptom | Check |
|---------|-------|
| "appsettings.json not found" on startup | The file must be in the same folder as `ChronosAD.exe` — don't move the exe out of the publish folder |
| "Database connection failed" on startup | SQL Server service running; ports 1433 (TCP) and 1434 (UDP) open in firewall; SQL Server Browser service running; `appsettings.json` has correct server hostname |
| Workstation gets "server not found or not accessible" even though port 1433 is open | UDP 1434 is likely blocked — SQL Server Browser needs this port to resolve the named instance. Add the B5 UDP firewall rule on the server |
| `sqlcmd` times out with named pipe error on the server | Use `tcp:localhost,1433` instead of `.\SQLEXPRESS` — named pipes is disabled by default |
| "Could not determine Windows identity" | App is running as a local account — must be launched as a domain user |
| App crashes silently | Check Event Viewer → Windows Logs → Application for .NET runtime errors |
| Users can't write to the database | Re-run the `ALTER ROLE db_datawriter` and `GRANT EXECUTE` commands from B8 |
| Can't reach server on port 1433 | TCP/IP not enabled (B4) or firewall rule missing (B5) — check both on the server |
| Wrong instance name error | If SQL installed as default instance use `Server=HOSTNAME` instead of `Server=HOSTNAME\SQLEXPRESS` |
| Build fails: SDK not found | Run `dotnet --list-sdks` — need version 10.x; install with `winget install Microsoft.DotNet.SDK.10` |


