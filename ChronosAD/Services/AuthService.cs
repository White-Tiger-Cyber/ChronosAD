using System.Security.Principal;

namespace ChronosAD.Services;

public class AuthService
{
    public string GetCurrentUserSID()
    {
        var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value ?? throw new InvalidOperationException("Cannot determine current user SID.");
    }

    public string GetCurrentUserName()
    {
        var identity = WindowsIdentity.GetCurrent();
        return identity.Name;
    }
}
