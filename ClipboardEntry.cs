using System.Text.Json.Serialization;

namespace CHistory_VS;

public class ClipboardEntry
{
    public string Text { get; }
    public DateTime CopiedAt { get; }

    public string Preview
    {
        get
        {
            var trimmed = Text.Trim();
            return trimmed.Length > 300 ? trimmed[..300] + "\u2026" : trimmed;
        }
    }

    public string DisplayTime
    {
        get
        {
            var now = DateTime.Now;
            if (CopiedAt.Date == now.Date)
                return CopiedAt.ToString("h:mm tt");
            if (CopiedAt.Date == now.Date.AddDays(-1))
                return "Yesterday " + CopiedAt.ToString("h:mm tt");
            return CopiedAt.ToString("MMM d, h:mm tt");
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
