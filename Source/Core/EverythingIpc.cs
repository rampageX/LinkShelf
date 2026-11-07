using System.Runtime.InteropServices;
using System.Text;
namespace LinkShelf.Core;
public sealed record EverythingInstance(IntPtr Handle, string ClassName)
{
    public override string ToString() => ClassName == "EVERYTHING_TASKBAR_NOTIFICATION" ? "默认实例" : ClassName.Replace("EVERYTHING_TASKBAR_NOTIFICATION", "实例 ").Trim();
}
public static class EverythingIpc
{
    [StructLayout(LayoutKind.Sequential)] public struct CopyData { public IntPtr Id; public int Length; public IntPtr Data; }
    delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassNameW(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SendMessageTimeoutW(IntPtr hwnd, uint msg, IntPtr wparam, ref CopyData data, uint flags, uint timeout, out IntPtr result);
    public static List<EverythingInstance> Instances()
    {
        var result = new List<EverythingInstance>();
        EnumWindows((h, _) =>
        {
            var b = new StringBuilder(256); GetClassNameW(h, b, b.Capacity); var name = b.ToString();
            if (name == "EVERYTHING_TASKBAR_NOTIFICATION" || name.StartsWith("EVERYTHING_TASKBAR_NOTIFICATION_(", StringComparison.Ordinal)) result.Add(new(h, name));
            return true;
        }, IntPtr.Zero);
        return result;
    }
    public static void Send(EverythingInstance instance, IntPtr replyWindow, uint replyId)
    {
        // Attribute-only query uses the existing index. Tag and target reads happen in our process.
        byte[] search = Encoding.Unicode.GetBytes("folder: attrib:L\0");
        byte[] packet = new byte[20 + search.Length];
        BitConverter.GetBytes(unchecked((uint)replyWindow.ToInt64())).CopyTo(packet, 0);
        BitConverter.GetBytes(replyId).CopyTo(packet, 4);
        BitConverter.GetBytes(100000U).CopyTo(packet, 16);
        search.CopyTo(packet, 20);
        var p = Marshal.AllocHGlobal(packet.Length);
        try
        {
            Marshal.Copy(packet, 0, p, packet.Length);
            var cds = new CopyData { Id = new IntPtr(2), Length = packet.Length, Data = p };
            if (SendMessageTimeoutW(instance.Handle, 0x4A, replyWindow, ref cds, 2, 5000, out var accepted) == IntPtr.Zero || accepted == IntPtr.Zero)
                throw new IOException("Everything 未接受查询。请确认客户端正在运行，且两边使用相同的权限级别。");
        }
        finally { Marshal.FreeHGlobal(p); }
    }
    public static List<string> Parse(byte[] data)
    {
        if (data.Length < 28) throw new IOException("Everything 返回数据不完整。");
        uint total = BitConverter.ToUInt32(data, 8), count = BitConverter.ToUInt32(data, 20);
        if (count > 100000 || 28L + count * 12L > data.Length) throw new IOException("Everything 返回数据无效。");
        if (total > count) throw new IOException("候选目录超过十万项，本次结果不完整；请缩小 Everything 索引范围后重试。");
        string GetString(uint offset)
        {
            if ((offset & 1) != 0 || offset >= data.Length) throw new IOException("Everything 字符串偏移无效。");
            int end = (int)offset;
            while (end + 1 < data.Length && (data[end] != 0 || data[end + 1] != 0)) end += 2;
            if (end + 1 >= data.Length) throw new IOException("Everything 字符串未终止。");
            return Encoding.Unicode.GetString(data, (int)offset, end - (int)offset);
        }
        var paths = new List<string>();
        for (int i = 0; i < count; i++)
        {
            int entry = 28 + i * 12;
            if ((BitConverter.ToUInt32(data, entry) & 1) == 0) continue;
            string name = GetString(BitConverter.ToUInt32(data, entry + 4));
            string parent = GetString(BitConverter.ToUInt32(data, entry + 8));
            paths.Add(System.IO.Path.Combine(parent, name));
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
