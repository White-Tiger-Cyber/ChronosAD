# ChronosAD

A custom employee timeclock system built for Active Directory environments. Employees clock in and out from their domain workstations with no passwords required — the app identifies them automatically through Windows Authentication. Managers get a real-time dashboard with punch editing, audit trails, payroll export, and messaging.

Built with C#, WPF/.NET 8, and SQL Server Express 2022. Designed for deployment on Windows Server 2025.

---

## Features

**Employee**
- Clock in / clock out with optional notes
- View total hours for the current 14-day pay period
- Full punch history timesheet
- Send messages to managers and receive replies

**Manager**
- Real-time dashboard showing all employees' status and hours
- Edit punch records with automatic audit trail (original values preserved)
- Freeze / unfreeze accounts, promote / demote managers, delete users
- Respond to employee messages
- Configure pay period start date and holidays
- Auto clock-out all active sessions
- Export punch data to CSV for payroll processing
- Archive records older than one year

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| UI | WPF (.NET 8, Windows) |
| Language | C# 12 |
| Database | SQL Server Express 2022 |
| Auth | Windows Authentication (Active Directory) |
| Architecture | MVVM |

---

## Project Structure

```
ChronosAD/
├── ChronosAD/              # Main WPF application
│   ├── Data/               # DatabaseService, DatabaseInitializer
│   ├── Models/             # User, Punch, Message, Config
│   ├── Services/           # AuthService, PunchService, MessageService
│   ├── ViewModels/         # MVVM ViewModels
│   └── Views/              # XAML windows and dialogs
├── ChronosAD.Database/     # Schema.sql — database schema + stored procedures
├── ChronosAD.Tests/        # Console test suite (60 tests)
└── BUILD_AND_DEPLOY.md     # Full deployment guide
```

---

## Deployment

See [BUILD_AND_DEPLOY.md](BUILD_AND_DEPLOY.md) for complete step-by-step instructions covering:
- Building a self-contained publish on your dev machine
- SQL Server Express setup on the client server
- Firewall and permissions configuration
- Workstation deployment
- First manager setup

**Quick build:**
```powershell
dotnet publish ChronosAD\ChronosAD.csproj --configuration Release --runtime win-x64 --self-contained true --output .\publish
```

---

## Tests

The test suite covers all CRUD operations, business logic, audit trails, and ViewModels — 60 tests, 0 failures.

```powershell
dotnet run --project ChronosAD.Tests\ChronosAD.Tests.csproj
```

> Requires a local SQL Server instance. See the connection string in `ChronosAD.Tests/Program.cs`.

---

## License

Private client project. All rights reserved.
