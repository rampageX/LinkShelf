using LinkShelf.UI;
namespace LinkShelf;
internal static class Program
{
    [STAThread] static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (_, e) => Dark.ShowError(null, e.Exception.Message);
        Application.Run(new MainForm());
    }
}
