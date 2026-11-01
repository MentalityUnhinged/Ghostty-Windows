using System.Windows;
using System.Windows.Media;

namespace GhosttyWindows;

sealed partial class Settings
{
    internal static string DefaultRegularFont() => ChooseDefaultFont(Fonts.SystemFontFamilies.Select(f => f.Source), SystemFonts.MessageFontFamily.Source);
    internal static string ChooseDefaultFont(IEnumerable<string> installed, string fallback) { var names = new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase); return names.Contains("Cascadia Mono") ? "Cascadia Mono" : names.Contains("Consolas") ? "Consolas" : fallback; }
    public bool UsePrimarySelection { get; set; }
    public bool ShiftMouseOverride { get; set; } = true;
    public bool UseCustomWordBoundaries { get; set; }
    public string WordBoundaryCharacters { get; set; } = " \t,;:()[]{}<>\"'";
    public int RepeatClickMilliseconds { get; set; }
    public double RepeatClickDistance { get; set; }
    public string BackgroundImageVerticalAlignment { get; set; } = "Center";
    public string CursorTextColor { get; set; } = "";
    public bool FollowApplicationCursorBlink { get; set; }
    public double TextBaselineOffset { get; set; }
    public double DecorationThickness { get; set; } = 1;
    public bool BalancePadding { get; set; }
    public double MinimumTextContrast { get; set; } = 1;
    public int MaxOpenTabs { get; set; } = Workspace.MaxTabs;
    public int MaxOpenPanes { get; set; } = Workspace.MaxPanes;
    public string PasteControlPolicy { get; set; } = "Reject";
    public bool ConfirmBracketedMultilinePaste { get; set; } = true;
    void ValidatePartialPreferences()
    {
        if (string.IsNullOrWhiteSpace(FontFamily) || !Fonts.SystemFontFamilies.Any(f => f.Source.Equals(FontFamily, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("The regular font family is not installed: " + FontFamily + ". Choose an installed font such as Consolas in settings.json.");
        if (!double.IsFinite(FontSize) || FontSize is < 8 or > 48) throw new ArgumentException("Font size must be between 8 and 48.");
        if (Theme is not ("Midnight" or "Paper" or "Dracula" or "Nord")) throw new ArgumentException("Unknown terminal theme: " + Theme);
        if (DefaultProfile == null || !Profiles.ContainsKey(DefaultProfile)) throw new ArgumentException("Unknown default shell profile: " + DefaultProfile);
        if (WordBoundaryCharacters == null || WordBoundaryCharacters.Length > 4096 || RepeatClickMilliseconds is < 0 or > 5000 || !double.IsFinite(RepeatClickDistance) || RepeatClickDistance is < 0 or > 100) throw new ArgumentException("Invalid word boundary or repeat-click preference.");
        if (BackgroundImageVerticalAlignment is not ("Top" or "Center" or "Bottom")) throw new ArgumentException("Unknown vertical image alignment.");
        if (CursorTextColor == null || CursorTextColor.Length > 0 && !ValidColor(CursorTextColor)) throw new ArgumentException("Cursor text color must be blank or six hex digits.");
        if (!double.IsFinite(TextBaselineOffset) || TextBaselineOffset is < -20 or > 20 || !double.IsFinite(DecorationThickness) || DecorationThickness is < .5 or > 8 || !double.IsFinite(MinimumTextContrast) || MinimumTextContrast is < 1 or > 21) throw new ArgumentException("Invalid baseline, decoration thickness or contrast preference.");
        if (MaxOpenTabs is < 1 or > Workspace.MaxTabs || MaxOpenPanes is < 1 or > Workspace.MaxPanes) throw new ArgumentException("Per-window limits must be 1–20 tabs and 1–64 panes.");
        if (PasteControlPolicy is not ("Reject" or "Strip")) throw new ArgumentException("Unknown paste control policy.");
    }
}

static class PastePolicy
{
    internal static string Prepare(string text, bool singleLine, string policy, out int removed)
    {
        static bool Unsafe(char c) => (c < ' ' && c is not ('\r' or '\n' or '\t')) || c == '\x7f' || c is >= '\x80' and <= '\x9f';
        int count = text.Count(Unsafe); removed = 0;
        if (count > 0 && policy == "Reject") throw new ArgumentException($"Paste was not sent: {count} control characters would be interpreted as terminal input. Remove them or explicitly choose Strip in Settings.");
        if (count > 0) { text = new string(text.Where(c => !Unsafe(c)).ToArray()); removed = count; }
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return singleLine ? text.Replace('\n', ' ') : text.Replace('\n', '\r');
    }
}

static class TextContrast
{
    internal static double Light(uint color)
    {
        double Channel(uint value) { double v = value / 255.0; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
        return .2126 * Channel((color >> 16) & 255) + .7152 * Channel((color >> 8) & 255) + .0722 * Channel(color & 255);
    }
    internal static double Ratio(uint a, uint b) { double x = Light(a), y = Light(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05); }
    internal static uint Adjust(uint foreground, uint background, double minimum)
    {
        if (minimum <= 1 || Ratio(foreground, background) >= minimum) return foreground;
        uint target = Ratio(0, background) > Ratio(0xffffff, background) ? 0u : 0xffffffu;
        uint Blend(double amount)
        {
            uint Part(int shift) => (uint)Math.Round(((foreground >> shift) & 255) * (1 - amount) + ((target >> shift) & 255) * amount);
            return Part(16) << 16 | Part(8) << 8 | Part(0);
        }
        double lo = 0, hi = 1; for (int i = 0; i < 12; i++) { double mid = (lo + hi) / 2; if (Ratio(Blend(mid), background) >= minimum) hi = mid; else lo = mid; }
        return Blend(hi);
    }
}
