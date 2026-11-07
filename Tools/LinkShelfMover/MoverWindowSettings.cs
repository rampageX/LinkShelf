using System.Text.Json;

namespace LinkShelf.Mover;

public sealed class MoverWindowSettings
{
    public int X { get; set; } = int.MinValue;
    public int Y { get; set; }
    public int Width { get; set; } = 920;
    public int Height { get; set; } = 530;
    public bool Maximized { get; set; }

    private static readonly string PortablePath = Path.Combine(AppContext.BaseDirectory, "LinkShelfMover.settings.json");
    private static readonly string FallbackPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LinkShelf", "Mover", "LinkShelfMover.settings.json");

    public static MoverWindowSettings Load()
    {
        foreach (string path in new[] { PortablePath, FallbackPath })
            try { if (File.Exists(path)) return JsonSerializer.Deserialize<MoverWindowSettings>(File.ReadAllText(path)) ?? new(); }
            catch { /* ignore invalid/stale settings */ }
        return new();
    }

    public void Apply(Form form)
    {
        if (X != int.MinValue && Width >= 810 && Height >= 505)
        {
            Rectangle area = new(X, Y, Width, Height);
            bool visible = Screen.AllScreens.Any(s =>
                Rectangle.Intersect(area, s.WorkingArea) is { Width: >= 160, Height: >= 100 });
            if (visible)
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Bounds = area;
                if (Maximized) form.WindowState = FormWindowState.Maximized;
                return;
            }
        }
        // Dark.Apply(Form) sets CenterParent; for top-level application windows
        // this has no owner, so always restore CenterScreen explicitly.
        form.StartPosition = FormStartPosition.CenterScreen;
    }

    public static void Save(Form form)
    {
        Rectangle rect = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
        var settings = new MoverWindowSettings
        {
            X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height,
            Maximized = form.WindowState == FormWindowState.Maximized
        };
        foreach (string path in new[] { PortablePath, FallbackPath })
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(settings));
                File.Move(temp, path, true);
                return;
            }
            catch { /* portable directory may be read-only; try per-user fallback */ }
        }
    }
}
