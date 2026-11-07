using System.Text;
using System.Text.Json;

namespace LinkShelf.Core;

// Two small portable JSON files next to the executable. Size results and
// Everything-instance snapshots are intentionally independent: a damaged
// snapshot must not discard measured data (or vice versa).
public sealed class PersistentStore
{
    const int Schema = 1;
    const long MaxJsonBytes = 32 * 1024 * 1024;
    readonly string sizesPath, linksPath;
    readonly Dictionary<string, LinkSnapshot> snapshots = new(StringComparer.OrdinalIgnoreCase);
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public Dictionary<string, SizeResult> Sizes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTimeOffset? CreatedUtc { get; private set; }
    public DateTimeOffset? UpdatedUtc { get; private set; }
    public bool SizeLoadFailed { get; private set; }
    public bool SnapshotLoadFailed { get; private set; }

    public PersistentStore(string directory)
    {
        sizesPath = Path.Combine(directory, "LinkShelf.sizes.json");
        linksPath = Path.Combine(directory, "LinkShelf.links.json");
        LoadSizes();
        LoadSnapshots();
    }

    void LoadSizes()
    {
        if (!File.Exists(sizesPath)) return;
        try
        {
            if (new FileInfo(sizesPath).Length > MaxJsonBytes) throw new InvalidDataException("大小缓存超出安全上限");
            var doc = JsonSerializer.Deserialize<SizeDocument>(File.ReadAllText(sizesPath));
            if (doc is null || doc.Schema != Schema || doc.Entries is null) throw new InvalidDataException("大小缓存版本或格式不正确");
            CreatedUtc = doc.CreatedUtc;
            UpdatedUtc = doc.UpdatedUtc;
            foreach (var entry in doc.Entries)
                if (IsPersistable(entry.Key, entry.Value)) Sizes[entry.Key] = entry.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or InvalidDataException)
        { Sizes.Clear(); CreatedUtc = null; UpdatedUtc = null; SizeLoadFailed = true; }
    }

    void LoadSnapshots()
    {
        if (!File.Exists(linksPath)) return;
        try
        {
            if (new FileInfo(linksPath).Length > MaxJsonBytes) throw new InvalidDataException("链接快照超出安全上限");
            var doc = JsonSerializer.Deserialize<LinkDocument>(File.ReadAllText(linksPath));
            if (doc is null || doc.Schema != Schema || doc.Instances is null) throw new InvalidDataException("链接快照版本或格式不正确");
            foreach (var pair in doc.Instances)
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value?.Paths != null)
                    snapshots[pair.Key] = new LinkSnapshot
                    {
                        CapturedUtc = pair.Value.CapturedUtc,
                        Paths = pair.Value.Paths.Where(p => !string.IsNullOrWhiteSpace(p))
                            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                    };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or InvalidDataException)
        { snapshots.Clear(); SnapshotLoadFailed = true; }
    }

    static bool IsPersistable(string path, SizeResult? result) =>
        Paths.IsLocalDrive(path) && result is { Bytes: >= 0, Files: >= 0, Errors: >= 0, Skipped: >= 0 } &&
        result.Status is { } status && !status.StartsWith("网络", StringComparison.OrdinalIgnoreCase);

    public bool SetSize(string path, SizeResult size)
    {
        Sizes[path] = size;
        // A refreshed local target may have become a network alias. Save the
        // filtered document so an older on-disk value is not resurrected.
        return Paths.IsLocalDrive(path);
    }

    public bool ForgetSize(string path) => Sizes.Remove(path);

    public void SaveSizes()
    {
        var valid = Sizes.Where(e => IsPersistable(e.Key, e.Value))
            .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;
        var created = CreatedUtc ?? now;
        WriteAtomically(sizesPath, JsonSerializer.Serialize(new SizeDocument
        { Schema = Schema, CreatedUtc = created, UpdatedUtc = now, Entries = valid }, Json));
        CreatedUtc = created;
        UpdatedUtc = now;
        SizeLoadFailed = false;
    }

    // Null means this Everything instance has never been recorded. An empty
    // set is a real baseline: newly discovered paths should be highlighted.
    public HashSet<string>? PreviousLinks(string instance) =>
        snapshots.TryGetValue(instance, out var snapshot)
            ? snapshot.Paths.ToHashSet(StringComparer.OrdinalIgnoreCase) : null;

    public static HashSet<string> NewlyDiscovered(IEnumerable<string> current, IReadOnlySet<string>? baseline)
    {
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (baseline is null) return added; // First run only establishes a baseline.
        foreach (var path in current)
            if (!string.IsNullOrWhiteSpace(path) && !baseline.Contains(path)) added.Add(path);
        return added;
    }

    public void SaveLinks(string instance, IEnumerable<string> paths)
    {
        if (string.IsNullOrWhiteSpace(instance)) return;
        var distinct = paths.Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        // An unexpectedly empty index must not erase a previously populated
        // baseline; a genuinely empty first snapshot can still be recorded.
        if (distinct.Length == 0 && snapshots.TryGetValue(instance, out var old) && old.Paths.Length != 0) return;
        var next = new Dictionary<string, LinkSnapshot>(snapshots, StringComparer.OrdinalIgnoreCase)
        {
            [instance] = new LinkSnapshot { CapturedUtc = DateTimeOffset.UtcNow, Paths = distinct }
        };
        WriteAtomically(linksPath, JsonSerializer.Serialize(new LinkDocument { Schema = Schema, Instances = next }, Json));
        snapshots[instance] = next[instance];
        SnapshotLoadFailed = false;
    }

    static void WriteAtomically(string path, string contents)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, contents, new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public sealed class SizeDocument
    {
        public int Schema { get; set; }
        public DateTimeOffset? CreatedUtc { get; set; }
        public DateTimeOffset? UpdatedUtc { get; set; }
        public Dictionary<string, SizeResult>? Entries { get; set; }
    }

    public sealed class LinkSnapshot
    {
        public DateTimeOffset CapturedUtc { get; set; }
        public string[] Paths { get; set; } = [];
    }

    public sealed class LinkDocument
    {
        public int Schema { get; set; }
        public Dictionary<string, LinkSnapshot>? Instances { get; set; }
    }
}
