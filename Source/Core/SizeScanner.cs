using System.Diagnostics;
namespace LinkShelf.Core;
public static class SizeScanner
{
    public static SizeResult Scan(string target, CancellationToken token, Action<long,long>? progress = null)
    {
        if (!Paths.IsLocalDrive(target)) return new(0, 0, 0, 0, false, "网络/卷目标 · 未计算");
        try
        {
            var drive = new DriveInfo(System.IO.Path.GetPathRoot(target)!);
            if (drive.DriveType == DriveType.Network) return new(0, 0, 0, 0, false, "网络目标 · 未计算");
            if (!drive.IsReady) return new(0, 0, 0, 0, false, "目标盘未就绪");
        }
        catch (Exception e) { return new(0, 0, 1, 0, false, "目标盘不可访问", e.Message); }
        try
        {
            // Resolve the top-level target once for aliases, then skip all descendant reparse points.
            target = Reparse.ResolveTarget(target);
            if (!Paths.IsLocalDrive(target)) return new(0, 0, 0, 0, false, "网络目标 · 未计算");
            uint a = Reparse.Attributes(target);
            if ((a & 16) == 0) return new(0, 0, 1, 0, false, "目标不是目录");
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            string state = e.NativeErrorCode is 2 or 3 ? "目标不存在" : e.NativeErrorCode == 5 ? "无权访问目标" : "目标不可访问";
            return new(0, 0, 1, 0, false, state, e.Message);
        }
        long bytes = 0, files = 0; int errors = 0, skipped = 0;
        var stack = new Stack<string>(); stack.Push(target);
        var watch = Stopwatch.StartNew(); long last = 0;
        var options = new EnumerationOptions { RecurseSubdirectories = false, IgnoreInaccessible = false, AttributesToSkip = 0, ReturnSpecialDirectories = false };
        while (stack.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            if (watch.Elapsed.TotalMinutes > 10) return new(bytes, files, errors, skipped, false, "已到 10 分钟上限 · 部分大小", "", target);
            var dir = stack.Pop();
            try
            {
                // Recheck immediately before enumeration in case a directory was replaced by a link.
                if ((Reparse.Attributes(dir) & 0x400) != 0) { skipped++; continue; }
                foreach (var entry in new DirectoryInfo(dir).EnumerateFileSystemInfos("*", options))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var attr = entry.Attributes;
                        if ((attr & FileAttributes.ReparsePoint) != 0) { skipped++; continue; }
                        if ((attr & FileAttributes.Directory) != 0) stack.Push(entry.FullName);
                        else { bytes = checked(bytes + ((FileInfo)entry).Length); files++; }
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { errors++; }
                    if (watch.ElapsedMilliseconds - last > 350) { progress?.Invoke(bytes, files); last = watch.ElapsedMilliseconds; }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { errors++; }
        }
        return new(bytes, files, errors, skipped, errors == 0, errors > 0 ? "部分大小 · 有读取失败" : skipped > 0 ? "正常 · 跳过内部链接" : "正常", "", target);
    }
}
