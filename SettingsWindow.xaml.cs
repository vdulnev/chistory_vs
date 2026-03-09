using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CHistory_VS;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly HotkeyManager _hotkeyManager;
    private bool _capturingHotkey;

    public SettingsWindow(AppSettings settings, HotkeyManager hotkeyManager)
    {
        InitializeComponent();
        _settings = settings;
        _hotkeyManager = hotkeyManager;

        HotkeyLabel.Text = _settings.HotkeyDisplayString;
        MaxItemsBox.Text = _settings.MaxHistoryItems.ToString();
        DarkThemeCheckBox.IsChecked = _settings.IsDarkTheme;
    }

    // ── Hotkey capture ────────────────────────────────────────────────────────

    private void ChangeHotkey_Click(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = true;
        HotkeyLabel.Text = "Press shortcut\u2026";
        HotkeyLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
        HotkeyBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
        ChangeHotkeyButton.Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
        Focus();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (!_capturingHotkey)
        {
            if (key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else
            {
                base.OnPreviewKeyDown(e);
            }
            return;
        }

        // Ignore standalone modifier keys
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        if (key == Key.Escape)
        {
            CancelCapture();
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None)
            return; // require at least one modifier

        _settings.HotkeyModifiers = modifiers;
        _settings.HotkeyKey = key;
        _settings.Save();

        bool ok = _hotkeyManager.Register(modifiers, key);
        CommitCapture(ok);
        e.Handled = true;
    }

    private void CommitCapture(bool registered)
    {
        _capturingHotkey = false;
        HotkeyLabel.Text = registered
            ? _settings.HotkeyDisplayString
            : _settings.HotkeyDisplayString + " (conflict)";

        // Restore DynamicResource colors by clearing local values
        HotkeyLabel.ClearValue(ForegroundProperty);
        HotkeyBorder.ClearValue(Border.BorderBrushProperty);
        ChangeHotkeyButton.ClearValue(ForegroundProperty);

        if (!registered)
            HotkeyLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));
    }

    private void CancelCapture()
    {
        _capturingHotkey = false;
        HotkeyLabel.Text = _settings.HotkeyDisplayString;
        HotkeyLabel.ClearValue(ForegroundProperty);
        HotkeyBorder.ClearValue(Border.BorderBrushProperty);
        ChangeHotkeyButton.ClearValue(ForegroundProperty);
    }

    // ── Max items ─────────────────────────────────────────────────────────────

    private void ApplyMax_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MaxItemsBox.Text, out int max) || max < 1)
        {
            MaxItemsBox.Text = _settings.MaxHistoryItems.ToString();
            return;
        }
        max = Math.Min(max, 9999);
        _settings.MaxHistoryItems = max;
        _settings.Save();
        MaxItemsBox.Text = max.ToString();
    }

    // ── Dark theme ────────────────────────────────────────────────────────────

    private void DarkTheme_Click(object sender, RoutedEventArgs e)
    {
        _settings.IsDarkTheme = DarkThemeCheckBox.IsChecked == true;
        _settings.Save();
        ThemeManager.Apply(_settings.IsDarkTheme);
    }

    // ── Footer ────────────────────────────────────────────────────────────────

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
