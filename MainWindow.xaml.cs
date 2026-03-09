using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace CHistory_VS;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ClipboardEntry> _history = new();
    private readonly ClipboardMonitor _monitor = new();
    private readonly HotkeyManager _hotkeyManager = new();
    private readonly WinForms.NotifyIcon _trayIcon;
    private AppSettings _settings = AppSettings.Load();
    private bool _suppressClipboardEvent;
    private bool _capturingHotkey;
    private bool _forceClose;
    private IntPtr _previousWindow;

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public MainWindow()
    {
        InitializeComponent();

        foreach (var entry in HistoryStore.Load())
            _history.Add(entry);

        HistoryList.ItemsSource = _history;
        _history.CollectionChanged += (_, _) => HistoryStore.Save(_history);

        HotkeyLabel.Text = _settings.HotkeyDisplayString;
        MaxItemsBox.Text = _settings.MaxHistoryItems.ToString();
        UpdateEmptyState();

        _trayIcon = CreateTrayIcon();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _monitor.ClipboardChanged += OnClipboardChanged;
        _monitor.Attach(this);

        _hotkeyManager.Attach(this);
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        _hotkeyManager.Register(_settings.HotkeyModifiers, _settings.HotkeyKey);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _trayIcon.Dispose();
        _monitor.Dispose();
        _hotkeyManager.Dispose();
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        _previousWindow = GetForegroundWindow();
        ShowWindow();
    }

    private void ShowWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
    }

    private WinForms.NotifyIcon CreateTrayIcon()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowWindow());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => { _forceClose = true; Close(); });

        var icon = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = "CHistory",
            Visible = true,
            ContextMenuStrip = menu
        };
        icon.DoubleClick += (_, _) => ShowWindow();
        return icon;
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
                Hide();
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

    // ── Clipboard monitoring ──────────────────────────────────────────────────

    private void OnClipboardChanged(object? sender, EventArgs e)
    {
        if (_suppressClipboardEvent) return;

        try
        {
            if (!Clipboard.ContainsText()) return;
            var text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text)) return;

            var existing = _history.FirstOrDefault(en => en.Text == text);
            if (existing != null)
                _history.Remove(existing);

            _history.Insert(0, new ClipboardEntry(text));

            while (_history.Count > _settings.MaxHistoryItems)
                _history.RemoveAt(_history.Count - 1);

            HistoryList.ScrollIntoView(_history[0]);
        }
        catch { }

        UpdateEmptyState();
    }

    private void CopyEntry(ClipboardEntry entry)
    {
        _suppressClipboardEvent = true;
        try
        {
            Clipboard.SetText(entry.Text);

            int idx = _history.IndexOf(entry);
            if (idx > 0)
            {
                _history.RemoveAt(idx);
                _history.Insert(0, entry);
                HistoryList.ScrollIntoView(entry);
            }
        }
        catch { }
        finally
        {
            _suppressClipboardEvent = false;
        }
    }

    // ── List interaction ──────────────────────────────────────────────────────

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is ClipboardEntry entry)
            _ = CopyAndPaste(entry);
    }

    private void HistoryList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && HistoryList.SelectedItem is ClipboardEntry entry)
        {
            e.Handled = true;
            _ = CopyAndPaste(entry);
        }
    }

    private async Task CopyAndPaste(ClipboardEntry entry)
    {
        CopyEntry(entry);
        var target = _previousWindow;
        Hide();
        if (target == IntPtr.Zero) return;

        await Task.Delay(80);
        SetForegroundWindow(target);
        await Task.Delay(50);

        const byte VK_CONTROL = 0x11;
        const byte VK_V = 0x56;
        const uint KEYEVENTF_KEYUP = 0x0002;
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ClipboardEntry entry)
        {
            _history.Remove(entry);
            UpdateEmptyState();
        }
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Clear all clipboard history?",
            "CHistory",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.OK)
        {
            _history.Clear();
            UpdateEmptyState();
        }
    }

    // ── Search ────────────────────────────────────────────────────────────────

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = SearchBox.Text;
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
        ClearSearchButton.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

        var view = CollectionViewSource.GetDefaultView(_history);
        if (string.IsNullOrWhiteSpace(text))
        {
            view.Filter = null;
            EmptyTitle.Text = "No clipboard history yet";
            EmptySubtitle.Text = "Copy something to get started";
        }
        else
        {
            view.Filter = o => o is ClipboardEntry entry &&
                entry.Text.Contains(text, StringComparison.OrdinalIgnoreCase);
            EmptyTitle.Text = "No results found";
            EmptySubtitle.Text = "Try a different search term";
        }

        UpdateEmptyState();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
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

        while (_history.Count > max)
            _history.RemoveAt(_history.Count - 1);

        UpdateEmptyState();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void UpdateEmptyState()
    {
        var view = CollectionViewSource.GetDefaultView(_history);
        bool hasVisibleItems = view.Cast<object>().Any();

        EmptyState.Visibility = hasVisibleItems ? Visibility.Collapsed : Visibility.Visible;
        HistoryList.Visibility = hasVisibleItems ? Visibility.Visible : Visibility.Collapsed;

        CountLabel.Text = _history.Count == 1 ? "1 item" : $"{_history.Count} items";
    }
}
