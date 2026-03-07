using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CHistory_VS;

public partial class MainWindow : Window
{
    private const int MaxHistorySize = 100;

    private readonly ObservableCollection<ClipboardEntry> _history = new();
    private readonly ClipboardMonitor _monitor = new();
    private bool _suppressClipboardEvent;
    private bool _handlingSelection;

    public MainWindow()
    {
        InitializeComponent();
        HistoryList.ItemsSource = _history;
        UpdateEmptyState();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _monitor.ClipboardChanged += OnClipboardChanged;
        _monitor.Attach(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _monitor.Dispose();
    }

    private void OnClipboardChanged(object? sender, EventArgs e)
    {
        if (_suppressClipboardEvent) return;

        try
        {
            if (!Clipboard.ContainsText()) return;
            var text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text)) return;

            // Remove existing duplicate anywhere in history
            var existing = _history.FirstOrDefault(en => en.Text == text);
            if (existing != null)
                _history.Remove(existing);

            _history.Insert(0, new ClipboardEntry(text));

            if (_history.Count > MaxHistorySize)
                _history.RemoveAt(_history.Count - 1);

            HistoryList.ScrollIntoView(_history[0]);
        }
        catch
        {
            // Clipboard can be locked by other processes; silently ignore
        }

        UpdateEmptyState();
    }

    private void CopyEntry(ClipboardEntry entry)
    {
        _suppressClipboardEvent = true;
        try
        {
            Clipboard.SetText(entry.Text);

            // Move to top
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

    private void UpdateEmptyState()
    {
        var view = CollectionViewSource.GetDefaultView(_history);
        bool hasVisibleItems = view.Cast<object>().Any();

        EmptyState.Visibility = hasVisibleItems ? Visibility.Collapsed : Visibility.Visible;
        HistoryList.Visibility = hasVisibleItems ? Visibility.Visible : Visibility.Collapsed;

        CountLabel.Text = _history.Count == 1 ? "1 item" : $"{_history.Count} items";
    }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_handlingSelection) return;
        if (HistoryList.SelectedItem is not ClipboardEntry entry) return;

        _handlingSelection = true;
        CopyEntry(entry);
        HistoryList.SelectedItem = null;
        _handlingSelection = false;
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
}
