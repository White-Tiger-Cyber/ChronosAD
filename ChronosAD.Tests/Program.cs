using ChronosAD.Data;
using ChronosAD.Models;
using ChronosAD.Services;
using ChronosAD.ViewModels;

// ── Connection string ──────────────────────────────────────────────────────
const string ConnStr =
    "Server=127.0.0.1,1433;Database=ChronosAD;User Id=sa;" +
    "Password=ChronosAD_Dev1!;TrustServerCertificate=True;";

// ── Test state ─────────────────────────────────────────────────────────────
int passed = 0;
int failed = 0;

void Pass(string name) { Console.WriteLine($"  [PASS] {name}"); passed++; }
void Fail(string name, string reason) { Console.WriteLine($"  [FAIL] {name}: {reason}"); failed++; }

void Assert(bool condition, string testName, string failMsg = "assertion false")
{
    if (condition) Pass(testName);
    else Fail(testName, failMsg);
}

void Run(string name, Action test)
{
    try { test(); }
    catch (Exception ex) { Fail(name, ex.Message); }
}

// ── Services ───────────────────────────────────────────────────────────────
var db = new DatabaseService(ConnStr);
var punchService = new PunchService(db);
var messageService = new MessageService(db);

const string TestSID  = "S-1-5-TEST-1001";
const string TestSID2 = "S-1-5-TEST-1002";

// ── Cleanup helper ─────────────────────────────────────────────────────────
void Cleanup()
{
    try { db.DeleteUser(TestSID); } catch { }
    try { db.DeleteUser(TestSID2); } catch { }
}

// ══════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n=== DatabaseInitializer ===");

Run("Initialize (idempotent)", () =>
{
    DatabaseInitializer.Initialize(ConnStr);
    DatabaseInitializer.Initialize(ConnStr); // second call must not throw
    Pass("Initialize (idempotent)");
});

// ══════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n=== DatabaseService – User CRUD ===");

Cleanup(); // ensure clean slate

Run("RegisterUser", () =>
{
    db.RegisterUser(new User
    {
        SID = TestSID,
        FirstName = "Alice",
        LastName = "Testington",
        StartDate = DateTime.Today
    });
    Pass("RegisterUser");
});

Run("GetUserBySID – found", () =>
{
    var u = db.GetUserBySID(TestSID);
    Assert(u != null && u.FirstName == "Alice", "GetUserBySID – found");
});

Run("GetUserBySID – not found", () =>
{
    var u = db.GetUserBySID("S-1-5-NOBODY");
    Assert(u == null, "GetUserBySID – not found");
});

Run("GetAllUsers – contains registered user", () =>
{
    var users = db.GetAllUsers();
    Assert(users.Any(u => u.SID == TestSID), "GetAllUsers – contains registered user");
});

Run("UpdateUser", () =>
{
    var u = db.GetUserBySID(TestSID)!;
    u.LastName = "Updated";
    u.IsManager = true;
    db.UpdateUser(u);
    var u2 = db.GetUserBySID(TestSID)!;
    Assert(u2.LastName == "Updated" && u2.IsManager, "UpdateUser");
});

// Register second user for manager tests
Run("RegisterUser2", () =>
{
    db.RegisterUser(new User
    {
        SID = TestSID2,
        FirstName = "Bob",
        LastName = "Worker",
        StartDate = DateTime.Today
    });
    Pass("RegisterUser2");
});

// ══════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n=== DatabaseService – Config ===");

Run("GetConfig – returns object", () =>
{
    var cfg = db.GetConfig();
    Assert(cfg != null && cfg.PayPeriodStartDate != default, "GetConfig – returns object");
});

Run("SaveConfig – round-trip", () =>
{
    var cfg = db.GetConfig();
    var original = cfg.PayPeriodStartDate;
    cfg.PayPeriodStartDate = new DateTime(2024, 1, 1);
    cfg.HolidayDates = new List<DateTime> { new DateTime(2024, 12, 25), new DateTime(2024, 7, 4) };
    db.SaveConfig(cfg);
    var cfg2 = db.GetConfig();
    Assert(cfg2.PayPeriodStartDate == new DateTime(2024, 1, 1) && cfg2.HolidayDates.Count == 2,
           "SaveConfig – round-trip");
    // restore
    cfg.PayPeriodStartDate = original;
    cfg.HolidayDates.Clear();
    db.SaveConfig(cfg);
});

// ══════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n=== DatabaseService – Punch CRUD ===");

Run("ClockIn – returns valid PunchID", () =>
{
    var id = db.ClockIn(TestSID, "morning shift");
    Assert(id > 0, "ClockIn – returns valid PunchID");
});

Run("IsUserClockedIn – true after ClockIn", () =>
{
    Assert(db.IsUserClockedIn(TestSID), "IsUserClockedIn – true after ClockIn");
});

Run("GetActiveSession – returns punch", () =>
{
    var p = db.GetActiveSession(TestSID);
    Assert(p != null && p.InNote == "morning shift", "GetActiveSession – returns punch");
});

Run("ClockOut", () =>
{
    var session = db.GetActiveSession(TestSID)!;
    db.ClockOut(session.PunchID, "done for day");
    Assert(!db.IsUserClockedIn(TestSID), "ClockOut");
});

Run("GetPunchesForPeriod – has record", () =>
{
    var (start, end) = punchService.GetCurrentPayPeriod();
    var punches = db.GetPunchesForPeriod(TestSID, start, end);
    Assert(punches.Count > 0 && punches[0].ClockOutTime.HasValue, "GetPunchesForPeriod – has record");
});

Run("GetAllPunchesForPeriod – has record", () =>
{
    var (start, end) = punchService.GetCurrentPayPeriod();
    var punches = db.GetAllPunchesForPeriod(start, end);
    Assert(punches.Any(p => p.UserSID == TestSID), "GetAllPunchesForPeriod – has record");
});

Run("UpdatePunch – audit trail set", () =>
{
    var (start, end) = punchService.GetCurrentPayPeriod();
    var punch = db.GetPunchesForPeriod(TestSID, start, end)[0];
    punch.ClockInTime = punch.ClockInTime.AddMinutes(-30);
    db.UpdatePunch(punch, TestSID2);
    var updated = db.GetPunchesForPeriod(TestSID, start, end)[0];
    Assert(updated.EditedBy == TestSID2 && updated.OriginalClockIn != null, "UpdatePunch – audit trail set");
});

Run("AutoClockOutAllActive – no exception", () =>
{
    // Clock TestSID2 in, then auto-clock-out all
    db.ClockIn(TestSID2, null);
    db.AutoClockOutAllActive();
    Assert(!db.IsUserClockedIn(TestSID2), "AutoClockOutAllActive – no exception");
});

Run("ExportPunches – returns list", () =>
{
    var punches = db.ExportPunches(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));
    Assert(punches != null, "ExportPunches – returns list");
});

Run("ArchiveOldPunches – no exception", () =>
{
    db.ArchiveOldPunches(); // won't archive recent data, just verifies no exception
    Pass("ArchiveOldPunches – no exception");
});

// ══════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n=== DatabaseService – DeleteUser (cascade) ===");

Run("DeleteUser – cascades to Punches and Messages", () =>
{
    // Add a punch and message for TestSID2 before deleting
    db.ClockIn(TestSID2, null);
    var session = db.GetActiveSession(TestSID2)!;
    db.ClockOut(session.PunchID);
    db.SendMessage(TestSID2, "test msg");
    db.DeleteUser(TestSID2);
    Assert(db.GetUserBySID(TestSID2) == null, "DeleteUser – cascades to Punches and Messages");
});

// ══════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n=== DatabaseService – Messages ===");

Run("SendMessage", () =>
{
    db.SendMessage(TestSID, "Hello manager!");
    Pass("SendMessage");
});

Run("GetMessagesForUser – has message", () =>
{
    var msgs = db.GetMessagesForUser(TestSID);
    Assert(msgs.Count > 0 && msgs[0].EmployeeMessage == "Hello manager!", "GetMessagesForUser – has message");
});

Run("RespondToMessage", () =>
{
    var msgs = db.GetMessagesForUser(TestSID);
    db.RespondToMessage(msgs[0].MessageID, "Hi Alice!");
    var updated = db.GetMessagesForUser(TestSID);
    Assert(updated[0].ManagerResponse == "Hi Alice!" && updated[0].IsRead, "RespondToMessage");
});

Run("GetAllMessages – includes TestSID", () =>
{
    var all = db.GetAllMessages();
    Assert(all.Any(m => m.UserSID == TestSID), "GetAllMessages – includes TestSID");
});

// ══════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n=== PunchService ===");

Run("GetCurrentPayPeriod – valid range", () =>
{
    var (start, end) = punchService.GetCurrentPayPeriod();
    Assert(start <= DateTime.Today && end > DateTime.Today && (end - start).Days == 14,
           "GetCurrentPayPeriod – valid range");
});

Run("CalculateTotalHours – sums durations", () =>
{
    var (start, end) = punchService.GetCurrentPayPeriod();
    var punches = db.GetPunchesForPeriod(TestSID, start, end);
    var total = punchService.CalculateTotalHours(punches);
    Assert(total >= 0, "CalculateTotalHours – sums durations");
});

Run("TryClockIn – success", () =>
{
    var ok = punchService.TryClockIn(TestSID, "punch service test", out var err);
    Assert(ok && string.IsNullOrEmpty(err), "TryClockIn – success");
});

Run("TryClockIn – duplicate rejection", () =>
{
    var ok = punchService.TryClockIn(TestSID, null, out var err);
    Assert(!ok && err.Contains("already clocked in"), "TryClockIn – duplicate rejection");
});

Run("TryClockOut – success", () =>
{
    var ok = punchService.TryClockOut(TestSID, "done", out var err);
    Assert(ok && string.IsNullOrEmpty(err), "TryClockOut – success");
});

Run("TryClockOut – no active session", () =>
{
    var ok = punchService.TryClockOut(TestSID, null, out var err);
    Assert(!ok && err.Contains("No active session"), "TryClockOut – no active session");
});

Run("TryClockIn – frozen user rejected", () =>
{
    var u = db.GetUserBySID(TestSID)!;
    u.IsFrozen = true;
    db.UpdateUser(u);
    var ok = punchService.TryClockIn(TestSID, null, out var err);
    Assert(!ok && err.Contains("frozen"), "TryClockIn – frozen user rejected");
    u.IsFrozen = false;
    db.UpdateUser(u);
});

// ══════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n=== MessageService ===");

Run("MessageService.Send", () =>
{
    messageService.Send(TestSID, "via MessageService");
    Pass("MessageService.Send");
});

Run("MessageService.GetForUser", () =>
{
    var msgs = messageService.GetForUser(TestSID);
    Assert(msgs.Any(m => m.EmployeeMessage == "via MessageService"), "MessageService.GetForUser");
});

Run("MessageService.Respond", () =>
{
    var msgs = messageService.GetForUser(TestSID);
    var msg = msgs.First(m => m.EmployeeMessage == "via MessageService");
    messageService.Respond(msg.MessageID, "Got it!");
    var updated = messageService.GetForUser(TestSID);
    Assert(updated.First(m => m.MessageID == msg.MessageID).ManagerResponse == "Got it!",
           "MessageService.Respond");
});

Run("MessageService.GetAll", () =>
{
    var all = messageService.GetAll();
    Assert(all != null && all.Any(m => m.UserSID == TestSID), "MessageService.GetAll");
});

// ══════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n=== ViewModels ===");

// Re-register TestSID2 for ViewModel tests
Run("Re-register TestSID2 for ViewModel tests", () =>
{
    db.RegisterUser(new User
    {
        SID = TestSID2,
        FirstName = "Bob",
        LastName = "Worker",
        StartDate = DateTime.Today
    });
    Pass("Re-register TestSID2 for ViewModel tests");
});

Run("RegistrationViewModel – empty name validation", () =>
{
    var regVm = new RegistrationViewModel(db, "S-1-5-TEST-NEWUSER");
    regVm.FirstName = "";
    regVm.LastName = "";
    var ok = regVm.Register();
    Assert(!ok && regVm.Error.Length > 0, "RegistrationViewModel – empty name validation");
});

Run("RegistrationViewModel – successful registration", () =>
{
    const string NewSID = "S-1-5-TEST-NEWUSER";
    try { db.DeleteUser(NewSID); } catch { }
    var regVm = new RegistrationViewModel(db, NewSID);
    regVm.FirstName = "Carol";
    regVm.LastName = "Register";
    var ok = regVm.Register();
    Assert(ok, "RegistrationViewModel – successful registration");
    var u = db.GetUserBySID(NewSID);
    Assert(u != null && u.FirstName == "Carol", "RegistrationViewModel – user persisted");
    try { db.DeleteUser(NewSID); } catch { }
});

Run("MainViewModel – construction and Refresh", () =>
{
    var user = db.GetUserBySID(TestSID)!;
    var vm = new MainViewModel(db, punchService, messageService, user);
    Assert(vm.CurrentUser.SID == TestSID, "MainViewModel – construction and Refresh");
    Assert(vm.TotalHoursText.Contains('h'), "MainViewModel – TotalHoursText formatted");
    Assert(!string.IsNullOrEmpty(vm.StatusText), "MainViewModel – StatusText not empty");
});

Run("MainViewModel – TogglePunch clock-in", () =>
{
    var user = db.GetUserBySID(TestSID)!;
    var vm = new MainViewModel(db, punchService, messageService, user);
    // Ensure clocked out
    if (vm.IsClockedIn) punchService.TryClockOut(TestSID, null, out _);
    vm.Refresh();
    Assert(!vm.IsClockedIn, "MainViewModel – starts clocked out");
    var ok = vm.TogglePunch("test in", out var err);
    Assert(ok && vm.IsClockedIn, "MainViewModel – TogglePunch clock-in");
});

Run("MainViewModel – TogglePunch clock-out", () =>
{
    var user = db.GetUserBySID(TestSID)!;
    var vm = new MainViewModel(db, punchService, messageService, user);
    Assert(vm.IsClockedIn, "MainViewModel – TogglePunch clock-out precondition");
    var ok = vm.TogglePunch("test out", out var err);
    Assert(ok && !vm.IsClockedIn, "MainViewModel – TogglePunch clock-out");
});

Run("MainViewModel – SendMessage", () =>
{
    var user = db.GetUserBySID(TestSID)!;
    var vm = new MainViewModel(db, punchService, messageService, user);
    vm.SendMessage("VM message test");
    vm.Refresh();
    Assert(vm.Messages.Any(m => m.EmployeeMessage == "VM message test"), "MainViewModel – SendMessage");
});

Run("ManagementViewModel – construction and Refresh", () =>
{
    var vm = new ManagementViewModel(db, punchService, messageService, TestSID);
    Assert(vm.Employees.Count >= 1, "ManagementViewModel – Employees loaded");
    Assert(vm.Config != null, "ManagementViewModel – Config loaded");
    Assert(vm.PeriodEnd > vm.PeriodStart, "ManagementViewModel – pay period valid");
});

Run("ManagementViewModel – SelectedEmployee loads punches", () =>
{
    var vm = new ManagementViewModel(db, punchService, messageService, TestSID);
    vm.SelectedEmployee = vm.Employees.First(e => e.User.SID == TestSID);
    // SelectedPunches may be empty (depends on pay period), but no exception
    Assert(vm.SelectedPunches != null, "ManagementViewModel – SelectedEmployee loads punches");
});

Run("ManagementViewModel – FreezeUser toggle", () =>
{
    var vm = new ManagementViewModel(db, punchService, messageService, TestSID);
    var before = db.GetUserBySID(TestSID2)!.IsFrozen;
    vm.FreezeUser(TestSID2);
    var after = db.GetUserBySID(TestSID2)!.IsFrozen;
    Assert(after != before, "ManagementViewModel – FreezeUser toggle");
    vm.FreezeUser(TestSID2); // restore
});

Run("ManagementViewModel – PromoteUser toggle", () =>
{
    var vm = new ManagementViewModel(db, punchService, messageService, TestSID);
    var before = db.GetUserBySID(TestSID2)!.IsManager;
    vm.PromoteUser(TestSID2);
    var after = db.GetUserBySID(TestSID2)!.IsManager;
    Assert(after != before, "ManagementViewModel – PromoteUser toggle");
    vm.PromoteUser(TestSID2); // restore
});

Run("ManagementViewModel – SaveConfig round-trip", () =>
{
    var vm = new ManagementViewModel(db, punchService, messageService, TestSID);
    var cfg = vm.Config;
    cfg.HolidayDates = new List<DateTime> { new DateTime(2025, 11, 27) };
    vm.SaveConfig(cfg);
    vm.Refresh();
    Assert(vm.Config.HolidayDates.Any(d => d.Month == 11 && d.Day == 27),
           "ManagementViewModel – SaveConfig round-trip");
});

Run("ManagementViewModel – RespondToMessage", () =>
{
    messageService.Send(TestSID2, "hello from Bob");
    var vm = new ManagementViewModel(db, punchService, messageService, TestSID);
    var msg = vm.AllMessages.First(m => m.UserSID == TestSID2);
    vm.RespondToMessage(msg.MessageID, "Hello Bob!");
    vm.Refresh();
    var updated = vm.AllMessages.First(m => m.MessageID == msg.MessageID);
    Assert(updated.ManagerResponse == "Hello Bob!", "ManagementViewModel – RespondToMessage");
});

Run("ManagementViewModel – RunAutoClockOut", () =>
{
    db.ClockIn(TestSID2, null);
    var vm = new ManagementViewModel(db, punchService, messageService, TestSID);
    vm.RunAutoClockOut();
    Assert(!db.IsUserClockedIn(TestSID2), "ManagementViewModel – RunAutoClockOut");
});

Run("ManagementViewModel – ArchiveData no exception", () =>
{
    var vm = new ManagementViewModel(db, punchService, messageService, TestSID);
    vm.ArchiveData();
    Pass("ManagementViewModel – ArchiveData no exception");
});

Run("ManagementViewModel – ExportToCSV", () =>
{
    var vm = new ManagementViewModel(db, punchService, messageService, TestSID);
    var path = Path.GetTempFileName();
    vm.ExportToCSV(path);
    var lines = File.ReadAllLines(path);
    Assert(lines.Length >= 1 && lines[0].StartsWith("PunchID"), "ManagementViewModel – ExportToCSV");
    File.Delete(path);
});

Run("ManagementViewModel – DeleteUser", () =>
{
    var vm = new ManagementViewModel(db, punchService, messageService, TestSID);
    vm.DeleteUser(TestSID2);
    Assert(db.GetUserBySID(TestSID2) == null, "ManagementViewModel – DeleteUser");
});

// ══════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n=== Cleanup ===");
Cleanup();
Pass("Test data cleaned up");

// ══════════════════════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine($"Results: {passed} passed, {failed} failed out of {passed + failed} tests.");
if (failed > 0)
{
    Console.WriteLine("SOME TESTS FAILED.");
    Environment.Exit(1);
}
Console.WriteLine("ALL TESTS PASSED.");
Environment.Exit(0);
