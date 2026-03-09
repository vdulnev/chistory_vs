using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace CHistory_VS;

public class ClipboardEntry : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Text { get; }
    public DateTime CopiedAt { get; }
    public string? SourceAppPath { get; set; }

    private bool _isFavorite;
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite == value) return;
            _isFavorite = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFavorite)));
        }
    }

    [JsonIgnore]
    private BitmapSource? _sourceAppIcon;

    [JsonIgnore]
    public BitmapSource? SourceAppIcon
    {
        get
        {
            if (_sourceAppIcon != null) return _sourceAppIcon;
            if (string.IsNullOrEmpty(SourceAppPath) || !File.Exists(SourceAppPath)) return null;
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(SourceAppPath);
                if (icon == null) return null;
                _sourceAppIcon = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                _sourceAppIcon.Freeze();
                return _sourceAppIcon;
            }
            catch { return null; }
        }
    }

    public string Preview
    {
        get
        {
            var trimmed = Text.Trim();
            return trimmed.Length > 300 ? trimmed[..300] + "\u2026" : trimmed;
        }
    }

    public ClipboardEntry(string text) : this(text, DateTime.Now) { }

    [JsonConstructor]
    public ClipboardEntry(string text, DateTime copiedAt)
    {
        Text = text;
        CopiedAt = copiedAt;
    }
}
