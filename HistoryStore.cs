using System.IO;
using System.Text.Json;

namespace CHistory_VS;

public static class HistoryStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CHistory", "history.json");

    public static List<ClipboardEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<ClipboardEntry>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IEnumerable<ClipboardEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(entries.ToList()));
        }
        catch { }
    }
}
