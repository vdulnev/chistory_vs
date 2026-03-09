using System.Windows;

namespace CHistory_VS;

public static class ThemeManager
{
    public static void Apply(bool isDark)
    {
        var dicts = Application.Current.Resources.MergedDictionaries;
        dicts.Clear();
        dicts.Add(new ResourceDictionary
        {
            Source = new Uri(isDark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        });
    }
}
