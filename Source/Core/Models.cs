using System.Text.Json;
namespace LinkShelf.Core;
public sealed class LinkRow
{
    public string Path { get; set; } = "";
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd('\\'));
    public uint Tag { get; set; }
    public byte[] Raw { get; set; } = [];
    public string StoredTarget { get; set; } = "";
    public string Target { get; set; } = "";
    public bool Relative { get; set; }
    public bool IsSystem { get; set; }
    public string Error { get; set; } = "";
    public SizeResult? Size { get; set; }
    public string Kind => Tag == 0xA000000C ? "符号链接 /D" : Tag == 0xA0000003 ? (StoredTarget.StartsWith(@"\??\Volume{", StringComparison.OrdinalIgnoreCase) ? "卷挂载点" : "目录连接点 /J") : "读取失败";
    public bool Editable => Error.Length == 0 && Kind != "卷挂载点" && (Tag == 0xA0000003 || Tag == 0xA000000C);
    public string Status => Error.Length > 0 ? Error : Size?.Status ?? (Target.StartsWith(@"\\") ? "网络目标 · 未计算" : "未计算");
    public string EffectiveTarget => string.IsNullOrEmpty(Size?.Canonical) ? Target : Size.Canonical;
    public bool Offloaded => Paths.IsDrive(Path, 'C') && Paths.IsLocalDrive(EffectiveTarget) && !Paths.IsDrive(EffectiveTarget, 'C') && Kind != "卷挂载点";
}
public sealed record SizeResult(long Bytes, long Files, int Errors, int Skipped, bool Complete, string Status, string Detail = "", string Canonical = "");
public static class Paths
{
    public static string Normalize(string p) => System.IO.Path.GetFullPath(p).TrimEnd('\\');
    public static bool IsDrive(string p, char drive) => p.Length >= 3 && char.ToUpperInvariant(p[0]) == char.ToUpperInvariant(drive) && p[1] == ':' && p[2] == '\\';
    public static bool IsLocalDrive(string p) => p.Length >= 3 && char.IsAsciiLetter(p[0]) && p[1] == ':' && p[2] == '\\';
    public static bool Contains(string parent, string child) => child.Equals(parent, StringComparison.OrdinalIgnoreCase) || child.StartsWith(parent.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    public static bool IsMissingTarget(LinkRow row) => row.Size?.Status == "目标不存在";
    // A missing target has no measurable bytes and must not shadow a valid
    // descendant during target-root deduplication or count as a partial scan.
    public static List<LinkRow> CountableOffloads(IEnumerable<LinkRow> rows) =>
        rows.Where(r => r.Offloaded && !r.IsSystem && !IsMissingTarget(r)).ToList();
    public static string Format(long bytes)
    {
        string[] u = ["B", "KiB", "MiB", "GiB", "TiB"];
        double n = bytes; int i = 0; while (n >= 1024 && i < u.Length - 1) { n /= 1024; i++; }
        return n.ToString(i == 0 ? "N0" : "N2") + " " + u[i];
    }
    // A parent and child cannot both contribute to the offload total.
    public static List<LinkRow> UniqueRoots(IEnumerable<LinkRow> rows)
    {
        var result = new List<LinkRow>();
        foreach (var row in rows.OrderBy(r => r.EffectiveTarget.Length))
            if (!result.Any(x => Contains(x.EffectiveTarget, row.EffectiveTarget))) result.Add(row);
        return result;
    }
}
public sealed class Settings
{
    public int X { get; set; } = -99999; public int Y { get; set; }
    public int Width { get; set; } = 1220; public int Height { get; set; } = 760;
    public bool Maximized { get; set; }
    public bool HideSystem { get; set; } = true;
    public string Instance { get; set; } = "";
    public string SortColumn { get; set; } = "Path";
    public bool SortAscending { get; set; } = true;
    public int QueryTimeoutSeconds { get; set; } = 30;
    public int[] ColumnWidths { get; set; } = [];
    public int ColumnLayoutVersion { get; set; }
    public static readonly string FilePath = System.IO.Path.Combine(AppContext.BaseDirectory, "LinkShelf.settings.json");
    public static Settings Load() { try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); } catch { return new(); } }
    public void Save() { var tmp = FilePath + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); File.Move(tmp, FilePath, true); }
}
