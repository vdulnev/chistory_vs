using Microsoft.Win32;

namespace CHistory_VS;

public static class StartupManager
{
    private const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "CHistory";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: false);
        return key?.GetValue(AppName) is string path &&
               path.Equals(Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
    }

    public static void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: true);
        key?.SetValue(AppName, Environment.ProcessPath!);
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }
}
