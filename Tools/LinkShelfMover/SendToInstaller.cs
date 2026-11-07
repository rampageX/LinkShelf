using System.Runtime.InteropServices;

namespace LinkShelf.Mover;

public static class SendToInstaller
{
    private const string ShortcutName = "LinkShelf - 移动并建立目录连接点.lnk";
    private const string Marker = "通过 LinkShelf 安全迁移普通文件夹并在原位置创建 /J 连接点";
    private static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.SendTo), ShortcutName);

    public static bool Installed
    {
        get
        {
            if (!File.Exists(ShortcutPath)) return false;
            try
            {
                Type? type = Type.GetTypeFromProgID("WScript.Shell");
                if (type == null) return false;
                object obj = Activator.CreateInstance(type)!;
                try
                {
                    dynamic shell = obj;
                    object shortcut = shell.CreateShortcut(ShortcutPath);
                    try { dynamic link = shortcut; return (string)link.Description == Marker; }
                    finally { if (Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut); }
                }
                finally { if (Marshal.IsComObject(obj)) Marshal.FinalReleaseComObject(obj); }
            }
            catch { return false; }
        }
    }

    public static void Install()
    {
        string executable = Environment.ProcessPath ?? throw new IOException("无法定位当前程序的 exe 文件。");
        string folder = Path.GetDirectoryName(executable)!;
        Directory.CreateDirectory(Path.GetDirectoryName(ShortcutPath)!);
        if (File.Exists(ShortcutPath))
            throw new IOException("‘发送到’菜单已经存在同名项目；请先检查该快捷方式，不会覆盖其他文件。");
        Type type = Type.GetTypeFromProgID("WScript.Shell") ??
                    throw new InvalidOperationException("系统未提供 Windows Script Host 快捷方式组件。");
        object obj = Activator.CreateInstance(type)!;
        try
        {
            dynamic shell = obj;
            object shortcut = shell.CreateShortcut(ShortcutPath);
            try
            {
                dynamic link = shortcut;
                link.TargetPath = executable;
                link.WorkingDirectory = folder;
                link.Description = Marker;
                link.IconLocation = executable + ",0";
                link.Save();
            }
            finally { if (Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut); }
        }
        finally { if (Marshal.IsComObject(obj)) Marshal.FinalReleaseComObject(obj); }
    }

    public static void Uninstall()
    {
        if (!File.Exists(ShortcutPath)) return;
        if (!Installed) throw new IOException("同名快捷方式不属于 LinkShelf Mover，拒绝删除。");
        File.Delete(ShortcutPath);
    }
}
