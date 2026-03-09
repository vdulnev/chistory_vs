using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace CHistory_VS;

public class AppSettings
{
    public ModifierKeys HotkeyModifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Shift;
    public Key HotkeyKey { get; set; } = Key.H;
    public int MaxHistoryItems { get; set; } = 100;
    public bool IsDarkTheme { get; set; } = false;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CHistory", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new();
        }
        catch { return new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch { }
    }

    public string HotkeyDisplayString
    {
        get
        {
            var parts = new List<string>();
            if (HotkeyModifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (HotkeyModifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (HotkeyModifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (HotkeyModifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(KeyToDisplayString(HotkeyKey));
            return string.Join("+", parts);
        }
    }

    private static string KeyToDisplayString(Key key)
    {
        if (key >= Key.D0 && key <= Key.D9)
            return ((int)(key - Key.D0)).ToString();
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return "Num" + (int)(key - Key.NumPad0);
        return key.ToString();
    }
}
