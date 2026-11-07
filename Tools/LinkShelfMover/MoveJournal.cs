using System.Text.Json;

namespace LinkShelf.Mover;

public sealed class MoveJournal
{
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Backup { get; set; } = "";
    public string Stage { get; set; } = "";
    public string Id { get; set; } = "";
    public string Phase { get; set; } = "";
    public DateTimeOffset Updated { get; set; }

    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LinkShelf", "Mover");
    public static string OperationLogPath => Path.Combine(DataDir, "LinkShelfMover.operations.jsonl");
    private string FilePath => Path.Combine(DataDir, Id + ".json");

    public static MoveJournal Begin(MovePlan plan)
    {
        var journal = new MoveJournal
        {
            Source = plan.Source, Destination = plan.Destination, Backup = plan.Backup,
            Stage = plan.Stage, Id = plan.JobId
        };
        journal.SetPhase("PREPARED");
        return journal;
    }

    public void SetPhase(string phase)
    {
        Directory.CreateDirectory(DataDir);
        Phase = phase;
        Updated = DateTimeOffset.Now;
        string temp = FilePath + ".tmp";
        using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(fs, this);
            fs.Flush(flushToDisk: true);
        }
        File.Move(temp, FilePath, true);
    }

    public static IEnumerable<MoveJournal> Unfinished()
    {
        if (!Directory.Exists(DataDir)) yield break;
        foreach (string path in Directory.EnumerateFiles(DataDir, "*.json"))
        {
            MoveJournal? item;
            try { item = JsonSerializer.Deserialize<MoveJournal>(File.ReadAllText(path)); }
            catch { continue; }
            if (item is { Phase: not ("COMPLETED" or "ROLLED_BACK" or "CANCELLED") })
                yield return item;
        }
    }
}
