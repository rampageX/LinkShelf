using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using LinkShelf.Core;

namespace LinkShelf.Mover;

public sealed record MovePlan(string Source, string Destination, string DestinationParent,
    string SourceParent, string Backup, string Stage, string JobId);

public static class PathRules
{
    private const uint DeleteAccess = 0x00010000;
    private const uint OpenDirectory = 0x02000000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share,
        IntPtr security, uint disposition, uint flags, IntPtr template);

    public static MovePlan MakePlan(string source, string destination)
    {
        string s = Normalize(source), d = Normalize(destination);
        if (!Paths.IsLocalDrive(s) || !Paths.IsLocalDrive(d))
            throw new IOException("源目录和目标目录都必须位于本机盘符，不支持网络目录。");
        if (s.Length <= 3 || d.Length <= 3)
            throw new IOException("不能迁移到磁盘根目录，也不能移动磁盘根目录。");
        string sourceParent = Path.GetDirectoryName(s)!, destParent = Path.GetDirectoryName(d)!;
        if (!Directory.Exists(s)) throw new IOException("源目录不存在或无法访问。");
        if (!Directory.Exists(destParent)) throw new IOException("目标目录的父目录不存在，请先选择有效文件夹。");
        if (File.Exists(d) || Directory.Exists(d) || IsDanglingReparse(d))
            throw new IOException("目标位置已经存在文件或目录（包括失效链接），请换个名字。");

        string[] forbidden = { "Windows", "Users", "Program Files", "Program Files (x86)",
            "ProgramData", "$Recycle.Bin", "System Volume Information", "Recovery", "Boot" };
        if (Path.GetDirectoryName(s)!.Length == 3 &&
            forbidden.Contains(Path.GetFileName(s), StringComparer.OrdinalIgnoreCase))
            throw new IOException("不允许迁移系统级顶层目录；其中的普通应用子目录可单独选择。");
        string root = Path.GetPathRoot(s)!;
        foreach (string protectedName in new[] { "Windows", "$Recycle.Bin", "System Volume Information", "Recovery", "Boot" })
            if (Paths.Contains(Path.Combine(root, protectedName), s))
                throw new IOException("禁止迁移 Windows 系统及其受保护目录：" + s);
        if (Paths.Contains(s, d) || Paths.Contains(d, s))
            throw new IOException("源目录和目标目录不能互相包含。");

        // Reject aliases in every ancestor: lexically unrelated paths can otherwise
        // refer to the same storage through a junction or mount point.
        RejectReparseAncestors(s);
        RejectReparseAncestors(destParent);
        // Also catch SUBST/drive aliases: two different drive letters can map
        // to the same underlying directory without a visible reparse parent.
        string resolvedSource = Reparse.ResolveTarget(s);
        string resolvedParent = Reparse.ResolveTarget(destParent);
        string resolvedDestination = Path.Combine(resolvedParent, Path.GetFileName(d));
        if (!Paths.IsLocalDrive(resolvedSource) || !Paths.IsLocalDrive(resolvedDestination) ||
            Paths.Contains(resolvedSource, resolvedDestination) ||
            Paths.Contains(resolvedDestination, resolvedSource))
            throw new IOException("源和目标经真实路径解析后存在包含关系或非本地别名，迁移已取消。");
        if (!string.Equals(new DriveInfo(Path.GetPathRoot(s)!).DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(new DriveInfo(Path.GetPathRoot(d)!).DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
            throw new IOException("两侧都必须是 NTFS 文件系统，以保留权限和创建 /J 连接点。");
        if (new DriveInfo(Path.GetPathRoot(s)!).DriveType != DriveType.Fixed ||
            new DriveInfo(Path.GetPathRoot(d)!).DriveType != DriveType.Fixed)
            throw new IOException("仅支持固定本地硬盘；不迁移移动盘和网络盘目录。");

        string id = Guid.NewGuid().ToString("N")[..12];
        string backup = Path.Combine(sourceParent, "." + Path.GetFileName(s) + ".LinkShelf-backup-" + id);
        string stage = Path.Combine(destParent, "." + Path.GetFileName(d) + ".LinkShelf-stage-" + id);
        if (Directory.Exists(backup) || File.Exists(backup) || Directory.Exists(stage) || File.Exists(stage))
            throw new IOException("临时目录存在同名冲突，请重试。");
        return new MovePlan(s, d, destParent, sourceParent, backup, stage, id);
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new IOException("必须输入完整的本地绝对路径。");
        string normalized = Path.GetFullPath(path.Trim().Trim('"')).TrimEnd('\\');
        if (normalized.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new IOException("路径含非法字符。");
        return normalized;
    }

    private static bool IsDanglingReparse(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    private static void RejectReparseAncestors(string path)
    {
        for (string? p = path; p is { Length: > 3 }; p = Path.GetDirectoryName(p))
            if ((File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("路径本身或上级目录是重解析点，迁移已取消：" + p);
    }

    public static void CheckSourceRenamePermission(string source)
    {
        using var h = CreateFileW(Reparse.Long(source), DeleteAccess, 7, IntPtr.Zero, 3, OpenDirectory, IntPtr.Zero);
        if (h.IsInvalid)
            throw new IOException("当前账户没有重命名源目录的权限，或目录正在被占用。请先关闭有关软件。", new Win32Exception(Marshal.GetLastWin32Error()));
    }

    public static void CheckDeletePermission(string path, bool directory)
    {
        using var h = CreateFileW(Reparse.Long(path), DeleteAccess, 7, IntPtr.Zero, 3,
            directory ? OpenDirectory : 0, IntPtr.Zero);
        if (h.IsInvalid)
            throw new IOException("没有删除/移动所需的权限，或项目正被其他进程占用：" + path,
                new Win32Exception(Marshal.GetLastWin32Error()));
    }

    public static void CheckCreatePermission(string parent)
    {
        string probe = Path.Combine(parent, ".LinkShelf-permission-test-" + Guid.NewGuid().ToString("N"));
        try { Directory.CreateDirectory(probe); }
        catch (Exception e) { throw new IOException("无法在目录中创建文件夹，请检查权限：" + parent, e); }
        try { Directory.Delete(probe); }
        catch (Exception e) { throw new IOException("无法删除临时测试目录，请检查权限：" + probe, e); }
    }
}
