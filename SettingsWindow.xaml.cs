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
        var color = registered
            ? Color.FromRgb(0x42, 0x42, 0x42)
            : Color.FromRgb(0xE5, 0x39, 0x35);
        HotkeyLabel.Foreground = new SolidColorBrush(color);
        HotkeyBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        ChangeHotkeyButton.Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
    }

    private void CancelCapture()
    {
        _capturingHotkey = false;
        HotkeyLabel.Text = _settings.HotkeyDisplayString;
        HotkeyLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x42, 0x42, 0x42));
        HotkeyBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        ChangeHotkeyButton.Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
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

    // ── Footer ────────────────────────────────────────────────────────────────

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
