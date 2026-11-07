using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace LinkShelf.Core;
public static class Reparse
{
    public const uint Mount = 0xA0000003, Symlink = 0xA000000C;
    // Optional log location for the separate migration helper. Main LinkShelf
    // keeps its existing portable log next to the executable.
    public static string? JournalPathOverride { get; set; }
    const uint OpenFlags = 0x02200000; // BACKUP_SEMANTICS | OPEN_REPARSE_POINT
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(SafeFileHandle h, uint code, byte[]? input, int inputLength, byte[]? output, int outputLength, out int returned, IntPtr overlap);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateSymbolicLinkW(string link, string target, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetFileInformationByHandle(SafeFileHandle h, int cls, byte[] data, uint length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetFinalPathNameByHandleW(SafeFileHandle h, StringBuilder buffer, uint count, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetFileAttributesW(string path);
    static Exception Error() => new Win32Exception(Marshal.GetLastWin32Error());
    public static string Long(string p) => p.StartsWith(@"\\?\") ? p : p.StartsWith(@"\\") ? @"\\?\UNC\" + p[2..] : @"\\?\" + p;
    static SafeFileHandle Open(string path, uint access = 0)
    {
        var h = CreateFileW(Long(path), access, 7, IntPtr.Zero, 3, OpenFlags, IntPtr.Zero);
        if (h.IsInvalid) { var e = Error(); h.Dispose(); throw e; } return h;
    }
    public static uint Attributes(string path)
    {
        var a = GetFileAttributesW(Long(path)); if (a == uint.MaxValue) throw Error(); return a;
    }
    static byte[] ReadRaw(SafeFileHandle h)
    {
        var bytes = new byte[16384];
        if (!DeviceIoControl(h, 0x900A8, null, 0, bytes, bytes.Length, out int read, IntPtr.Zero)) throw Error();
        if (read < 8 || BitConverter.ToUInt16(bytes, 4) + 8 > read) throw new IOException(L.T("重解析数据不完整"));
        return bytes[..read];
    }
    public static LinkRow Read(string path)
    {
        uint attributes = Attributes(path);
        if ((attributes & 16) == 0) throw new NotSupportedException(L.T("仅管理文件夹链接"));
        using var h = Open(path); var row = Parse(path, ReadRaw(h));
        row.IsSystem = (attributes & 4) != 0; return row;
    }
    public static LinkRow Parse(string path, byte[] bytes)
    {
        if (bytes.Length < 8) throw new IOException(L.T("重解析数据不完整"));
        uint tag = BitConverter.ToUInt32(bytes, 0);
        if (tag != Mount && tag != Symlink) throw new NotSupportedException(L.T("其他重解析点"));
        int start = tag == Symlink ? 20 : 16;
        if (bytes.Length < start) throw new IOException(L.T("重解析头损坏"));
        int off = BitConverter.ToUInt16(bytes, 8), len = BitConverter.ToUInt16(bytes, 10);
        if ((len & 1) != 0 || off + start + len > bytes.Length) throw new IOException(L.T("目标路径数据损坏"));
        string stored = Encoding.Unicode.GetString(bytes, start + off, len);
        bool relative = tag == Symlink && (BitConverter.ToUInt32(bytes, 16) & 1) != 0;
        string target = stored.StartsWith(@"\??\UNC\", StringComparison.OrdinalIgnoreCase) ? @"\\" + stored[8..]
            : stored.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase) ? stored[4..] : stored;
        if (target.StartsWith("Volume{", StringComparison.OrdinalIgnoreCase)) target = @"\\?\" + target;
        if (relative) target = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!, stored));
        return new LinkRow { Path = path, Tag = tag, Raw = bytes, Target = target, StoredTarget = stored, Relative = relative };
    }
    public static byte[] Build(uint tag, string target, bool relative = false)
    {
        string subst = target;
        if (tag == Mount) subst = @"\??\" + target;
        else if (!relative) subst = target.StartsWith(@"\\") ? @"\??\UNC\" + target[2..] : @"\??\" + target;
        byte[] a = Encoding.Unicode.GetBytes(subst), b = Encoding.Unicode.GetBytes(target);
        int start = tag == Symlink ? 20 : 16;
        var bytes = new byte[start + a.Length + 2 + b.Length + 2];
        if (bytes.Length > 16384) throw new IOException(L.T("链接目标路径过长"));
        BitConverter.GetBytes(tag).CopyTo(bytes, 0);
        BitConverter.GetBytes((ushort)(bytes.Length - 8)).CopyTo(bytes, 4);
        BitConverter.GetBytes((ushort)a.Length).CopyTo(bytes, 10);
        BitConverter.GetBytes((ushort)(a.Length + 2)).CopyTo(bytes, 12);
        BitConverter.GetBytes((ushort)b.Length).CopyTo(bytes, 14);
        if (tag == Symlink) BitConverter.GetBytes(relative ? 1U : 0U).CopyTo(bytes, 16);
        a.CopyTo(bytes, start); b.CopyTo(bytes, start + a.Length + 2); return bytes;
    }
    static void WriteRaw(SafeFileHandle h, byte[] bytes)
    {
        if (!DeviceIoControl(h, 0x900A4, bytes, bytes.Length, null, 0, out _, IntPtr.Zero)) throw Error();
    }
    static void CheckSame(SafeFileHandle h, LinkRow expected)
    {
        if (!ReadRaw(h).SequenceEqual(expected.Raw)) throw new IOException(L.T("链接已被其他程序修改，请刷新后重试。"));
        if (!expected.Editable) throw new IOException(L.T("此项目不可修改。"));
    }
    static void Journal(string action, object item)
    {
        string path = JournalPathOverride ?? System.IO.Path.Combine(AppContext.BaseDirectory, "LinkShelf.operations.jsonl");
        File.AppendAllText(path, System.Text.Json.JsonSerializer.Serialize(new { time = DateTimeOffset.Now, action, item }) + Environment.NewLine, Encoding.UTF8);
    }
    public static void Delete(LinkRow row)
    {
        using var h = Open(row.Path, 0x10000); CheckSame(h, row);
        Journal("delete-link", new { row.Path, row.Target, row.Tag, raw = Convert.ToBase64String(row.Raw) });
        // Delete the opened reparse-point object, never recurse into its target.
        if (!SetFileInformationByHandle(h, 4, [1], 1)) throw Error();
    }
    public static void Retarget(LinkRow row, string newTarget)
    {
        string target = ValidateTarget(newTarget, row.Path, row.Tag);
        using var h = Open(row.Path, 0x40000000); CheckSame(h, row);
        string stored = row.Relative ? System.IO.Path.GetRelativePath(System.IO.Path.GetDirectoryName(row.Path)!, target) : target;
        if (row.Relative && System.IO.Path.IsPathRooted(stored)) throw new IOException(L.T("相对符号链接不能改到另一卷，请新建绝对符号链接。"));
        Journal("retarget-before", new { row.Path, row.Target, newTarget = target, raw = Convert.ToBase64String(row.Raw) });
        try { WriteRaw(h, Build(row.Tag, stored, row.Relative)); }
        catch (Exception e)
        {
            try { if (!ReadRaw(h).SequenceEqual(row.Raw)) WriteRaw(h, row.Raw); }
            catch (Exception rollback) { throw new IOException(L.T("修改失败，恢复也失败：{0}。旧关系已记录在操作日志。", rollback.Message), e); }
            throw;
        }
    }
    public static string ValidateTarget(string target, string link, uint tag)
    {
        if (!System.IO.Path.IsPathFullyQualified(target)) throw new IOException(L.T("请选择完整的目标路径。"));
        target = System.IO.Path.GetFullPath(target);
        if (tag == Mount && !Paths.IsLocalDrive(target)) throw new IOException(L.T("目录连接点要求本地盘符目标，网络路径请使用符号链接。"));
        if (Paths.Contains(link, target)) throw new IOException(L.T("目标不能是链接自身或链接内的子目录。"));
        if (!Directory.Exists(target)) throw new IOException(L.T("目标目录不存在或不可访问。"));
        return target;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateDirectoryW(string path, IntPtr security);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool RemoveDirectoryW(string path);
    public static void Create(string path, string target, uint tag)
    {
        if (!System.IO.Path.IsPathFullyQualified(path) || !Paths.IsLocalDrive(path)) throw new IOException(L.T("链接位置必须是本地完整路径。"));
        path = System.IO.Path.GetFullPath(path).TrimEnd('\\');
        if (path.Length <= 3) throw new IOException(L.T("不能在卷根目录上创建链接。"));
        target = ValidateTarget(target, path, tag);
        if (!Directory.Exists(System.IO.Path.GetDirectoryName(path))) throw new IOException(L.T("链接的父目录不存在。"));
        Journal("create-before", new { path, target, tag });
        if (tag == Symlink)
        {
            if (!CreateSymbolicLinkW(Long(path), target, 3))
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 87 || !CreateSymbolicLinkW(Long(path), target, 1)) throw Error();
            }
        }
        else
        {
            // Atomic creation avoids using or deleting a pre-existing ordinary directory.
            if (!CreateDirectoryW(Long(path), IntPtr.Zero)) throw Error();
            try { using var h = Open(path, 0x40000000); WriteRaw(h, Build(Mount, target)); }
            catch { RemoveDirectoryW(Long(path)); throw; }
        }
    }
    public static string ResolveTarget(string path)
    {
        using var h = CreateFileW(Long(path), 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (h.IsInvalid) throw Error();
        var b = new StringBuilder(32768); uint n = GetFinalPathNameByHandleW(h, b, (uint)b.Capacity, 0);
        if (n == 0 || n >= b.Capacity) throw Error();
        string p = b.ToString(); return p.StartsWith(@"\\?\UNC\") ? @"\\" + p[8..] : p.StartsWith(@"\\?\") ? p[4..] : p;
    }
}
