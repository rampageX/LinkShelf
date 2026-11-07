using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using LinkShelf.Core;

namespace LinkShelf.Mover;

public sealed class MovePermissionException : IOException
{
    public MovePermissionException(string message) : base(message) { }
}

internal static class Elevation
{
    public static bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static bool IsAccessDenied(Exception e)
    {
        for (Exception? current = e; current != null; current = current.InnerException)
        {
            if (current is MovePermissionException or UnauthorizedAccessException) return true;
            if (current is Win32Exception native && native.NativeErrorCode is 5 or 1314) return true;
            if (current.HResult == unchecked((int)0x80070005)) return true;
        }
        return false;
    }

    public static bool RestartAsAdministrator(string source, string destination, uint linkTag)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add("--resume-elevated");
        start.ArgumentList.Add(source);
        start.ArgumentList.Add(destination);
        start.ArgumentList.Add(linkTag == Reparse.Symlink ? "D" : "J");
        try
        {
            using Process? process = Process.Start(start);
            return process != null;
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223)
        {
            // User declined UAC. Nothing changes in the original directory.
            return false;
        }
    }
}
