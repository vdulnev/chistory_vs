using System.IO;
using System.Text.Json;

namespace CHistory_VS;

public static class HistoryStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CHistory");

    private static readonly string HistoryFile   = Path.Combine(Dir, "history.json");
    private static readonly string FavoritesFile = Path.Combine(Dir, "favorites.json");

    public static bool FavoritesFileExists() => File.Exists(FavoritesFile);

    public static List<ClipboardEntry> Load()
    {
        try
        {
            if (!File.Exists(HistoryFile)) return [];
            var json = File.ReadAllText(HistoryFile);
            return JsonSerializer.Deserialize<List<ClipboardEntry>>(json) ?? [];
        }
        catch { return []; }
    }

    public static void Save(IEnumerable<ClipboardEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(HistoryFile, JsonSerializer.Serialize(entries.ToList()));
        }
        catch { }
    }

    public static List<ClipboardEntry> LoadFavorites()
    {
        try
        {
            if (!File.Exists(FavoritesFile)) return [];
            var json = File.ReadAllText(FavoritesFile);
            return JsonSerializer.Deserialize<List<ClipboardEntry>>(json) ?? [];
        }
        catch { return []; }
    }

    public static void SaveFavorites(IEnumerable<ClipboardEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FavoritesFile, JsonSerializer.Serialize(entries.ToList()));
        }
        catch { }
    }
}
