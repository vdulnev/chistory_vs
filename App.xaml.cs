using System.Windows;

namespace CHistory_VS;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.Apply(AppSettings.Load().IsDarkTheme);
    }
}
