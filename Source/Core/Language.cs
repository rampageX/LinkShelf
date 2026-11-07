using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace LinkShelf.Core;

// One bilingual catalogue shared by the manager, mover and native tests.
// Chinese is the stable source key. Storage/status codes are never translated;
// only text presented to the user passes through this class.
public static class L
{
    const string FileName = "languages.json";
    const string ResourceName = "LinkShelf.languages.json";
    const int MaxBytes = 512 * 1024;
    static readonly Lazy<Dictionary<string, string>> English = new(Load);

    public static bool IsChinese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";

    public static string T(string chinese, params object?[] arguments) =>
        ForCulture(CultureInfo.CurrentUICulture.Name, chinese, arguments);

    public static string ForCulture(string? culture, string chinese, params object?[] arguments)
    {
        string selected = culture?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true
            ? chinese : English.Value.GetValueOrDefault(chinese, chinese);
        if (arguments.Length == 0) return selected;
        try { return string.Format(CultureInfo.CurrentCulture, selected, arguments); }
        catch (FormatException) { return selected; } // A hand-edited catalogue must never crash a move.
    }

    // Cached SizeResult.Status and LinkRow.Kind remain invariant Chinese tokens
    // for backwards compatibility with LinkShelf.sizes.json and link safety rules.
    public static string Kind(string internalKind) => T(internalKind);
    public static string Status(string internalStatus) => internalStatus.StartsWith("读取失败：", StringComparison.Ordinal)
        ? T("读取失败：") + internalStatus["读取失败：".Length..] : T(internalStatus);

    static Dictionary<string, string> Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, FileName);
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length <= MaxBytes)
                return Parse(File.ReadAllText(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException
            or InvalidOperationException or KeyNotFoundException or ArgumentException) { }

        try
        {
            using Stream? stream = typeof(L).Assembly.GetManifestResourceStream(ResourceName);
            if (stream is null) return new(StringComparer.Ordinal);
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException
            or InvalidOperationException or KeyNotFoundException or ArgumentException) { return new(StringComparer.Ordinal); }
    }

    static Dictionary<string, string> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("strings", out var entries) || entries.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Invalid bilingual language catalogue");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            string? zh = entry.GetProperty("zh").GetString();
            string? en = entry.GetProperty("en").GetString();
            if (string.IsNullOrEmpty(zh) || string.IsNullOrEmpty(en) || !result.TryAdd(zh, en))
                throw new InvalidDataException("Missing or duplicate translation");
        }
        return result;
    }
}
