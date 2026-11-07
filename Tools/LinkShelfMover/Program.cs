using LinkShelf.UI;
using LinkShelf.Core;

namespace LinkShelf.Mover;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (_, e) => Dark.ShowError(null, e.Exception.Message);
        bool elevatedRetry = args.Length == 4 && args[0] == "--resume-elevated";
        using var singleton = new Mutex(initiallyOwned: true, "Local\\LinkShelfMover.SingleInstance", out bool acquired);
        if (!acquired && elevatedRetry)
        {
            // The unelevated parent exits as soon as ShellExecute(runas)
            // succeeds. Give it time to release the single-instance mutex.
            try { acquired = singleton.WaitOne(TimeSpan.FromSeconds(20)); }
            catch (AbandonedMutexException) { acquired = true; }
        }
        if (!acquired)
        {
            Dark.ShowError(null, L.T("已有一个 LinkShelf Mover 正在运行。为了避免两个迁移任务冲突，请先完成前一个任务。"));
            return;
        }
        if (args.Length > 1 && !elevatedRetry)
        {
            Dark.ShowError(null, L.T("每次只能迁移一个目录。请在资源管理器中仅选择一个文件夹。"));
            return;
        }
        Application.Run(new MoverForm(elevatedRetry ? args[1] : args.Length == 1 ? args[0] : "",
            elevatedRetry ? args[2] : "",
            elevatedRetry && args[3] == "D" ? Reparse.Symlink : Reparse.Mount));
        if (acquired) singleton.ReleaseMutex();
    }
}
