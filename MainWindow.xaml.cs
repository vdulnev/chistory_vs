using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using WinForms = System.Windows.Forms;

namespace CHistory_VS;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ClipboardEntry> _history   = new();
    private readonly ObservableCollection<ClipboardEntry> _favorites = new();
    private readonly ClipboardMonitor _monitor = new();
    private readonly HotkeyManager _hotkeyManager = new();
    private readonly WinForms.NotifyIcon _trayIcon;
    private AppSettings _settings = AppSettings.Load();
    private bool _suppressClipboardEvent;
    private bool _forceClose;
    private bool _showFavorites;
    private IntPtr _previousWindow;

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public MainWindow()
    {
        InitializeComponent();

        // Load history
        foreach (var entry in HistoryStore.Load())
            _history.Add(entry);

        // Load favorites — migrate from old history.json if favorites.json doesn't exist yet
        if (HistoryStore.FavoritesFileExists())
        {
            foreach (var entry in HistoryStore.LoadFavorites())
            {
                entry.IsFavorite = true;
                _favorites.Add(entry);
            }
        }
        else
        {
            // First run with new format: extract favorites from history entries
            foreach (var entry in _history.Where(e => e.IsFavorite).ToList())
                _favorites.Add(new ClipboardEntry(entry.Text, entry.CopiedAt) { IsFavorite = true });
            HistoryStore.SaveFavorites(_favorites);
        }

        // Sync IsFavorite flag on history entries so the star button renders correctly
        SyncHistoryFavoriteFlags();

        HistoryList.ItemsSource = _history;
        _history.CollectionChanged   += (_, _) => HistoryStore.Save(_history);
        _favorites.CollectionChanged += (_, _) => HistoryStore.SaveFavorites(_favorites);

        UpdateEmptyState();

        _trayIcon = CreateTrayIcon();
    }

    // Sets IsFavorite on history entries to match presence in _favorites
    private void SyncHistoryFavoriteFlags()
    {
        var favTexts = new HashSet<string>(_favorites.Select(f => f.Text));
        foreach (var entry in _history)
            entry.IsFavorite = favTexts.Contains(entry.Text);
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
        FocusFirstItem();
    }

    private void FocusFirstItem()
    {
        var source = _showFavorites ? _favorites : _history;
        if (source.Count == 0) return;
        HistoryList.SelectedIndex = 0;
        Dispatcher.InvokeAsync(() =>
        {
            if (HistoryList.ItemContainerGenerator.ContainerFromIndex(0) is ListViewItem item)
                item.Focus();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
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

    // ── Settings ──────────────────────────────────────────────────────────────

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings, _hotkeyManager) { Owner = this };
        dialog.ShowDialog();

        TrimHistory(_settings.MaxHistoryItems);
        UpdateEmptyState();
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

            // Remove existing history entry for this text (dedup)
            var existing = _history.FirstOrDefault(en => en.Text == text);
            if (existing != null)
                _history.Remove(existing);

            // New entry — mark as favorite if this text is already in _favorites
            var entry = new ClipboardEntry(text)
            {
                IsFavorite = _favorites.Any(f => f.Text == text)
            };
            _history.Insert(0, entry);

            TrimHistory(_settings.MaxHistoryItems);

            if (!_showFavorites)
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

            // Find the entry in history (entry may have come from _favorites)
            var histEntry = _history.FirstOrDefault(h => h.Text == entry.Text);
            if (histEntry != null)
            {
                int idx = _history.IndexOf(histEntry);
                if (idx > 0)
                {
                    _history.RemoveAt(idx);
                    _history.Insert(0, histEntry);
                }
            }
            else
            {
                // Item only in favorites (pruned or never in history); add to history
                var newEntry = new ClipboardEntry(entry.Text, entry.CopiedAt)
                {
                    IsFavorite = _favorites.Any(f => f.Text == entry.Text)
                };
                _history.Insert(0, newEntry);
                TrimHistory(_settings.MaxHistoryItems);
            }

            if (!_showFavorites && _history.Count > 0)
                HistoryList.ScrollIntoView(_history[0]);
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
        if (sender is not Button btn || btn.Tag is not ClipboardEntry entry) return;

        if (_showFavorites)
        {
            // Remove from favorites; clear the flag on the matching history entry
            _favorites.Remove(entry);
            var histEntry = _history.FirstOrDefault(h => h.Text == entry.Text);
            if (histEntry != null) histEntry.IsFavorite = false;
        }
        else
        {
            // Remove from history only; favorites unaffected
            _history.Remove(entry);
        }

        UpdateEmptyState();
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
        ApplyFilter();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    // ── Tabs ──────────────────────────────────────────────────────────────────

    private void AllTab_Checked(object sender, RoutedEventArgs e)
    {
        if (EmptyTitle == null) return; // called before InitializeComponent completes
        _showFavorites = false;
        HistoryList.ItemsSource = _history;
        ApplyFilter();
    }

    private void FavoritesTab_Checked(object sender, RoutedEventArgs e)
    {
        _showFavorites = true;
        HistoryList.ItemsSource = _favorites;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var text = SearchBox.Text;
        var activeCollection = _showFavorites ? (object)_favorites : _history;
        var view = CollectionViewSource.GetDefaultView(activeCollection);

        if (string.IsNullOrWhiteSpace(text))
            view.Filter = null;
        else
            view.Filter = o => o is ClipboardEntry e &&
                e.Text.Contains(text, StringComparison.OrdinalIgnoreCase);

        if (_showFavorites)
        {
            EmptyIcon.Text = "\u2605";
            EmptyTitle.Text = "No favorites yet";
            EmptySubtitle.Text = "Star an item to pin it here";
        }
        else if (string.IsNullOrWhiteSpace(text))
        {
            EmptyIcon.Text = "\uD83D\uDCCB";
            EmptyTitle.Text = "No clipboard history yet";
            EmptySubtitle.Text = "Copy something to get started";
        }
        else
        {
            EmptyIcon.Text = "\uD83D\uDD0D";
            EmptyTitle.Text = "No results found";
            EmptySubtitle.Text = "Try a different search term";
        }

        UpdateEmptyState();
    }

    // ── Key handling ──────────────────────────────────────────────────────────

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void TrimHistory(int max)
    {
        // Favorites are a separate collection; _history is pruned by count unconditionally
        while (_history.Count > max)
            _history.RemoveAt(_history.Count - 1);
    }

    private void FavoriteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ClipboardEntry entry) return;

        if (_showFavorites)
        {
            // Unstar: remove from favorites, clear flag on history entry
            _favorites.Remove(entry);
            var histEntry = _history.FirstOrDefault(h => h.Text == entry.Text);
            if (histEntry != null) histEntry.IsFavorite = false;
        }
        else
        {
            // Toggle star in history
            var existingFav = _favorites.FirstOrDefault(f => f.Text == entry.Text);
            if (existingFav != null)
            {
                _favorites.Remove(existingFav);
                entry.IsFavorite = false;
            }
            else
            {
                _favorites.Insert(0, new ClipboardEntry(entry.Text, entry.CopiedAt) { IsFavorite = true });
                entry.IsFavorite = true;
            }
        }

        ApplyFilter();
    }

    private void UpdateEmptyState()
    {
        var activeCollection = _showFavorites ? (object)_favorites : _history;
        var view = CollectionViewSource.GetDefaultView(activeCollection);
        bool hasVisibleItems = view.Cast<object>().Any();

        EmptyState.Visibility = hasVisibleItems ? Visibility.Collapsed : Visibility.Visible;
        HistoryList.Visibility = hasVisibleItems ? Visibility.Visible : Visibility.Collapsed;

        CountLabel.Text = _history.Count == 1 ? "1 item" : $"{_history.Count} items";
    }
}
