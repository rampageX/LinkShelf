using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LinkShelf.Core;
using Microsoft.Win32.SafeHandles;

namespace LinkShelf.Mover;

public sealed record MoveProgress(string Message, bool CanCancel = true);
public sealed record MoveResult(string Source, string Destination, long Bytes, int FileCount, string? BackupLeft);

public static class MigrationEngine
{
    private sealed record FileEntry(string Relative, long Length, DateTime LastWriteUtc);
    private sealed record Inventory(List<FileEntry> Files, HashSet<string> Directories, long Bytes);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInfo
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Modified;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileIdInfo data);

    public static Task<MoveResult> MoveAsync(string source, string destination,
        IProgress<MoveProgress> report, CancellationToken token, uint linkTag = Reparse.Mount) =>
        Task.Run(() => Move(source, destination, report, token, linkTag), token);

    private static MoveResult Move(string source, string destination,
        IProgress<MoveProgress> report, CancellationToken token, uint linkTag)
    {
        if (linkTag is not (Reparse.Mount or Reparse.Symlink))
            throw new ArgumentOutOfRangeException(nameof(linkTag), "只能建立目录连接点 /J 或目录符号链接 /D。");
        report.Report(new("检查路径、目录权限和文件占用…"));
        MovePlan plan = PathRules.MakePlan(source, destination);
        token.ThrowIfCancellationRequested();
        PathRules.CheckSourceRenamePermission(plan.Source);
        // Creating the eventual junction also requires permission in source parent.
        PathRules.CheckCreatePermission(plan.SourceParent);
        PathRules.CheckCreatePermission(plan.DestinationParent);
        Inventory before = InventoryOf(plan.Source, report, token, checkFileLocks: true);
        long free = new DriveInfo(Path.GetPathRoot(plan.Destination)!).AvailableFreeSpace;
        if (before.Bytes > free - 64L * 1024 * 1024)
            throw new IOException($"目标盘剩余空间不足：数据 {Paths.Format(before.Bytes)}，可用 {Paths.Format(free)}，还需预留 64 MiB。");
        if (File.Exists(plan.Destination) || Directory.Exists(plan.Destination))
            throw new IOException("检查期间目标路径被其他程序占用，请重新选择。");

        token.ThrowIfCancellationRequested();
        Reparse.JournalPathOverride = MoveJournal.OperationLogPath;
        MoveJournal journal = MoveJournal.Begin(plan);
        bool movedSource = false, movedStage = false, madeJunction = false;
        try
        {
            report.Report(new($"预检通过：{before.Files.Count:N0} 个文件，{before.Directories.Count:N0} 个目录，{Paths.Format(before.Bytes)}。正在复制…"));
            journal.SetPhase("COPYING");
            Directory.CreateDirectory(plan.Stage);
            Robocopy(plan, token).GetAwaiter().GetResult();

            report.Report(new("复制完成，正在核对目录及数据内容（SHA-256）…"));
            Inventory staged = InventoryOf(plan.Stage, report, token, checkFileLocks: false);
            CompareInventory(before, staged);
            foreach (var (entry, idx) in before.Files.Select((entry, idx) => (entry, idx)))
            {
                token.ThrowIfCancellationRequested();
                string left = Path.Combine(plan.Source, entry.Relative);
                string right = Path.Combine(plan.Stage, entry.Relative);
                using var s = new FileStream(left, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var d = new FileStream(right, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (!SHA256.HashData(s).AsSpan().SequenceEqual(SHA256.HashData(d)))
                    throw new IOException("复制后校验失败，源数据未移动：" + entry.Relative);
                if (idx % 100 == 0)
                    report.Report(new($"已校验 {idx + 1:N0}/{before.Files.Count:N0} 个文件…"));
            }
            // Check for mutations during transfer and avoid replacing a changed source.
            CompareInventory(before, InventoryOf(plan.Source, report, token, checkFileLocks: true));
            PathRules.CheckSourceRenamePermission(plan.Source);
            token.ThrowIfCancellationRequested();

            // From this point cancellation is disabled. These three namespace
            // operations are reversible, but cannot be made atomic cross-volume.
            report.Report(new("校验通过，正在切换目录（此阶段不可取消）…", false));
            journal.SetPhase("COMMITTING");
            if (Directory.Exists(plan.Destination) || File.Exists(plan.Destination) ||
                Directory.Exists(plan.Backup) || Directory.Exists(plan.Stage) == false)
                throw new IOException("提交前路径发生变化，未移动源目录。");
            Directory.Move(plan.Source, plan.Backup);
            movedSource = true;
            journal.SetPhase("SOURCE_BACKED_UP");
            Directory.Move(plan.Stage, plan.Destination);
            movedStage = true;
            journal.SetPhase("TARGET_READY");
            Reparse.Create(plan.Source, plan.Destination, linkTag);
            madeJunction = true;
            LinkRow actual = Reparse.Read(plan.Source);
            if (actual.Tag != linkTag ||
                !Path.GetFullPath(actual.Target).Equals(plan.Destination, StringComparison.OrdinalIgnoreCase))
                throw new IOException("新链接核验失败，准备恢复旧目录。");
            journal.SetPhase("LINK_CREATED");
        }
        catch (Exception failure)
        {
            if (madeJunction || movedSource || movedStage)
            {
                try
                {
                    if (madeJunction)
                    {
                        LinkRow link = Reparse.Read(plan.Source);
                        if (!Path.GetFullPath(link.Target).Equals(plan.Destination, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("链接指向发生变化，不自动删除。");
                        Reparse.Delete(link); // native no-follow deletion, never touches target
                    }
                    if (movedStage) Directory.Move(plan.Destination, plan.Stage);
                    if (movedSource) Directory.Move(plan.Backup, plan.Source);
                    journal.SetPhase("ROLLED_BACK");
                }
                catch (Exception rollback)
                {
                    try { journal.SetPhase("RECOVERY_REQUIRED"); } catch { /* keep original error */ }
                    throw new IOException("迁移未完成，自动恢复也失败！请勿手动删除临时目录。\n" +
                        $"原路径：{plan.Source}\n保留的源备份：{plan.Backup}\n目标：{plan.Destination}\n" +
                        "请先检查以上路径，必要时将源备份改名还原。恢复错误：" + rollback.Message, failure);
                }
            }
            else { try { journal.SetPhase(token.IsCancellationRequested ? "CANCELLED" : "ROLLED_BACK"); } catch { } }
            try { SafeRemoveTree(plan.Stage); }
            catch (Exception cleanup) { report.Report(new("未能清理部分暂存数据，请人工检查：" + plan.Stage + "。" + cleanup.Message, false)); }
            throw;
        }

        report.Report(new("目录链接已建立，正在清理原目录备份以释放源盘空间…", false));
        try
        {
            SafeRemoveTree(plan.Backup);
            journal.SetPhase("COMPLETED");
            report.Report(new("原目录备份已清理，迁移全部完成，目录链接正常。", false));
            return new MoveResult(plan.Source, plan.Destination, before.Bytes, before.Files.Count, null);
        }
        catch (Exception ex)
        {
            try { journal.SetPhase("COMMITTED_WITH_BACKUP"); } catch { }
            report.Report(new("目录链接已生效，但清理源目录备份未完成：" + ex.Message, false));
            return new MoveResult(plan.Source, plan.Destination, before.Bytes, before.Files.Count, plan.Backup);
        }
    }

    private static Inventory InventoryOf(string root, IProgress<MoveProgress> report,
        CancellationToken token, bool checkFileLocks)
    {
        var files = new List<FileEntry>();
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "." };
        var pending = new Stack<string>();
        pending.Push(root);
        long bytes = 0;
        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            string folder = pending.Pop();
            foreach (string entry in Directory.EnumerateFileSystemEntries(folder))
            {
                FileAttributes attr = File.GetAttributes(entry);
                if ((attr & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("存在内部链接或其他重解析点，避免递归/误迁移，已停止：" + entry);
                if ((attr & FileAttributes.Encrypted) != 0)
                    throw new IOException("存在 EFS 加密项目，迁移可能破坏加密关系，已停止：" + entry);
                if ((attr & FileAttributes.ReadOnly) != 0 && (attr & FileAttributes.Directory) == 0)
                    throw new IOException("存在只读文件，迁移后可能无法清理旧目录，请先确认：" + entry);
                string relative = Path.GetRelativePath(root, entry);
                if ((attr & FileAttributes.Directory) != 0)
                {
                    if (checkFileLocks) PathRules.CheckDeletePermission(entry, directory: true);
                    folders.Add(relative);
                    pending.Push(entry);
                }
                else
                {
                    var info = new FileInfo(entry);
                    if (checkFileLocks)
                    {
                        PathRules.CheckDeletePermission(entry, directory: false);
                        try
                        {
                            using var s = new FileStream(entry, FileMode.Open, FileAccess.Read, FileShare.Read);
                            if (!GetFileInformationByHandle(s.SafeFileHandle, out FileIdInfo identity))
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                            if (identity.Links > 1)
                                throw new IOException("文件存在硬链接，跨盘复制无法保持硬链接关系：" + entry);
                        }
                        catch (IOException) { throw; }
                        catch (Exception ex) { throw new IOException("文件无法读取或被其他程序占用：" + entry, ex); }
                    }
                    files.Add(new FileEntry(relative, info.Length, info.LastWriteTimeUtc));
                    bytes = checked(bytes + info.Length);
                    if (files.Count % 1000 == 0)
                        report.Report(new($"正在检查：{files.Count:N0} 个文件，{Paths.Format(bytes)}…"));
                }
            }
        }
        files.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Relative, b.Relative));
        return new Inventory(files, folders, bytes);
    }

    private static void CompareInventory(Inventory source, Inventory target)
    {
        if (source.Files.Count != target.Files.Count || source.Directories.Count != target.Directories.Count ||
            !source.Directories.SetEquals(target.Directories))
            throw new IOException("复制期间文件/目录数量发生变化，操作已停止。");
        for (int i = 0; i < source.Files.Count; i++)
        {
            FileEntry a = source.Files[i], b = target.Files[i];
            if (!a.Relative.Equals(b.Relative, StringComparison.OrdinalIgnoreCase) ||
                a.Length != b.Length || a.LastWriteUtc != b.LastWriteUtc)
                throw new IOException("文件列表或大小/时间发生变化，操作已停止：" + a.Relative);
        }
    }

    private static async Task Robocopy(MovePlan plan, CancellationToken token)
    {
        // Robocopy writes localized ANSI/OEM text to redirected stdout. Use its
        // Unicode log for diagnostics, so Chinese filenames do not become mojibake.
        string logFolder = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "LinkShelf", "Mover");
        Directory.CreateDirectory(logFolder);
        string logPath = Path.Combine(logFolder, "Robocopy-" + plan.JobId + ".log");
        var psi = new ProcessStartInfo("robocopy.exe")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (string arg in new[] { plan.Source, plan.Stage, "/E", "/COPY:DATS", "/DCOPY:DAT",
            "/R:0", "/W:0", "/XJ", "/MT:8", "/NP", "/NFL", "/NDL", "/NJH", "/NJS",
            "/UNILOG:" + logPath })
            psi.ArgumentList.Add(arg);
        using var proc = new Process { StartInfo = psi };
        if (!proc.Start()) throw new IOException("无法启动 Windows Robocopy。");
        Task<string> stdout = proc.StandardOutput.ReadToEndAsync();
        Task<string> stderr = proc.StandardError.ReadToEndAsync();
        try { await proc.WaitForExitAsync(token); }
        catch (OperationCanceledException)
        {
            if (!proc.HasExited) proc.Kill(entireProcessTree: true);
            await proc.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        string fallback = (await stdout + "\n" + await stderr).Trim();
        string details;
        try { details = File.ReadAllText(logPath, Encoding.Unicode); }
        catch { details = fallback; }
        if (proc.ExitCode >= 8)
        {
            string? denied = details.Split('\n').FirstOrDefault(line =>
                line.Contains("0x00000005", StringComparison.OrdinalIgnoreCase));
            if (denied != null)
            {
                string? affected = Regex.Match(denied, @"[A-Za-z]:\\[^\r\n]*").Value.Trim();
                throw new MovePermissionException("复制时 Windows 拒绝访问（错误 5）。\n" +
                    (string.IsNullOrEmpty(affected) ? "" : "涉及目录：" + affected + "\n") +
                    "可能是源目录受到保护，也可能是复制 NTFS 权限时缺少权限。\n" +
                    "请先退出相关软件；可以选择以管理员身份重新检查并迁移。\n" +
                    "原目录未被移动。\n详细日志：" + logPath);
            }
            throw new IOException($"复制失败（Robocopy 退出码 {proc.ExitCode}），原目录未移动。\n" +
                (details.Length > 650 ? details[^650..] : details) + "\n详细日志：" + logPath);
        }
    }

    private static void SafeRemoveTree(string path)
    {
        if (!Directory.Exists(path)) return;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("待清理目录变成重解析点，拒绝删除：" + path);
        foreach (string entry in Directory.EnumerateFileSystemEntries(path))
        {
            FileAttributes attr = File.GetAttributes(entry);
            if ((attr & FileAttributes.ReparsePoint) != 0)
                throw new IOException("待清理目录中出现重解析点，拒绝自动删除：" + entry);
            if ((attr & FileAttributes.Directory) != 0) SafeRemoveTree(entry);
            else File.Delete(entry);
        }
        Directory.Delete(path);
    }
}
