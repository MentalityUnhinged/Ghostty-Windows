using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GhosttyWindows;

sealed partial class Settings
{
    public int ScrollCommandLines { get; set; } = 1;
    public double ScrollPageFraction { get; set; } = .5;
    public string CursorShape { get; set; } = "Application";
    public string CursorColor { get; set; } = "";
    public double CursorOpacity { get; set; } = .45;
    public bool CursorBlink { get; set; }
    public int CursorBlinkMilliseconds { get; set; } = 600;
    public string UnfocusedCursor { get; set; } = "Hollow";
    public string SelectionBackground { get; set; } = "344d73";
    public string SearchBackground { get; set; } = "69551c";
    public double InactivePaneOpacity { get; set; } = 1;
    public double CellWidthScale { get; set; } = 1;
    public double CellHeightScale { get; set; } = 1;
    public double PaddingHorizontal { get; set; } = 10;
    public double PaddingVertical { get; set; } = 8;
    public bool AutoTerminalTheme { get; set; }
    public string LightTerminalTheme { get; set; } = "Paper";
    public string DarkTerminalTheme { get; set; } = "Midnight";
    public string BoldFontFamily { get; set; } = "";
    public string ItalicFontFamily { get; set; } = "";
    bool systemDark = true;
    internal string EffectiveTheme => AutoTerminalTheme ? systemDark ? DarkTerminalTheme : LightTerminalTheme : Theme;
    internal void SetSystemTheme(bool dark) { if (systemDark == dark) return; systemDark = dark; if (AutoTerminalTheme) Changed?.Invoke(); }
    internal static uint ParseColor(string value) => uint.Parse(value.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    internal static bool ValidColor(string value) => value.TrimStart('#').Length == 6 && uint.TryParse(value.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
    internal string StyledFont(uint flags) => (flags & 3) == 3 && BoldItalicFontFamily.Length > 0 ? BoldItalicFontFamily : (flags & 1) != 0 && BoldFontFamily.Length > 0 ? BoldFontFamily : (flags & 2) != 0 && ItalicFontFamily.Length > 0 ? ItalicFontFamily : FontFamily;
    internal void ValidateAppearance()
    {
        ValidateFourthPreferences();
        ValidateFifthPreferences();
        if (new[] { CursorShape, CursorColor, UnfocusedCursor, SelectionBackground, SearchBackground, LightTerminalTheme, DarkTerminalTheme, BoldFontFamily, ItalicFontFamily }.Any(v => v == null)) throw new ArgumentException("Appearance text settings cannot be null.");
        void Range(double value, double min, double max, string name) { if (!double.IsFinite(value) || value < min || value > max) throw new ArgumentException($"{name} must be between {min} and {max}."); }
        Range(ScrollCommandLines, 1, 1000, "Scroll command lines"); Range(ScrollPageFraction, .05, 2, "Scroll page fraction");
        Range(CursorOpacity, 0, 1, "Cursor opacity"); Range(CursorBlinkMilliseconds, 100, 5000, "Cursor blink milliseconds");
        Range(InactivePaneOpacity, .1, 1, "Inactive pane opacity"); Range(CellWidthScale, .5, 3, "Cell width scale"); Range(CellHeightScale, .5, 3, "Cell height scale");
        Range(PaddingHorizontal, 0, 100, "Horizontal padding"); Range(PaddingVertical, 0, 100, "Vertical padding");
        if (CursorShape is not ("Application" or "Block" or "Bar" or "Underline")) throw new ArgumentException("Unknown cursor shape.");
        if (UnfocusedCursor is not ("Hollow" or "Hidden" or "Block" or "Bar" or "Underline")) throw new ArgumentException("Unknown unfocused cursor shape.");
        if ((CursorColor.Length > 0 && !ValidColor(CursorColor)) || !ValidColor(SelectionBackground) || !ValidColor(SearchBackground)) throw new ArgumentException("Colors must contain six hexadecimal digits (optional #).");
        string[] themes = { "Paper", "Midnight", "Dracula", "Nord" };
        if (!themes.Contains(LightTerminalTheme) || !themes.Contains(DarkTerminalTheme)) throw new ArgumentException("Choose a supported light/dark theme.");
        foreach (string font in new[] { BoldFontFamily, ItalicFontFamily })
            if (font.Length > 0 && !Fonts.SystemFontFamilies.Any(f => f.Source.Equals(font, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException($"Font is not installed: {font}");
    }
}

sealed partial class SettingsWindow
{
    readonly Dictionary<string, Control> appearanceFields = new();
    void AddAppearanceFields(Panel parent)
    {
        var fields = new StackPanel();
        var expander = new Expander { Header = "Appearance, scrolling, clipboard and tabs", Content = fields, Margin = new Thickness(0, 16, 0, 0), Foreground = Brushes.Gainsboro };
        parent.Children.Add(expander);
        void Add(string name, string label, string[]? choices = null)
        {
            var property = typeof(Settings).GetProperty(name)!; object value = property.GetValue(source)!;
            Control control = property.PropertyType == typeof(bool) ? new CheckBox { Content = label, IsChecked = (bool)value, Foreground = Brushes.Gainsboro }
                : choices != null ? new ComboBox { ItemsSource = choices, SelectedItem = value }
                : new TextBox { Text = Convert.ToString(value, CultureInfo.InvariantCulture) };
            if (control is not CheckBox) fields.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 10, 0, 4) });
            System.Windows.Automation.AutomationProperties.SetName(control, label);
            fields.Children.Add(control); appearanceFields.Add(name, control);
        }
        Add(nameof(Settings.CaptureProtocolDiagnostics), "Capture bounded unsupported OSC/APC samples (may contain terminal data)");
        Add(nameof(Settings.ShortcutPrecedence), "Default shortcut precedence", new[] { "Application", "Terminal" });
        fields.Children.Add(new TextBlock { Text = "Custom KeyBindings are edited in Open settings JSON. Ctrl+, and Ctrl+Shift+P remain recovery shortcuts.", TextWrapping = TextWrapping.Wrap });
        Add(nameof(Settings.UsePrimarySelection), "Copy mouse selections to a separate in-memory primary buffer");
        Add(nameof(Settings.ShiftMouseOverride), "Shift forces local mouse handling");
        Add(nameof(Settings.UseCustomWordBoundaries), "Use custom word-boundary characters");
        Add(nameof(Settings.WordBoundaryCharacters), "Word boundaries (literal characters)");
        Add(nameof(Settings.RepeatClickMilliseconds), "Repeat click interval ms (0 = Windows)");
        Add(nameof(Settings.RepeatClickDistance), "Repeat click distance pixels (0 = Windows)");
        Add(nameof(Settings.PasteControlPolicy), "Pasted control characters", new[] { "Reject", "Strip" });
        Add(nameof(Settings.ConfirmBracketedMultilinePaste), "Confirm multiline paste even in bracketed-paste mode");
        Add(nameof(Settings.BackgroundImageVerticalAlignment), "Background vertical alignment", new[] { "Top", "Center", "Bottom" });
        Add(nameof(Settings.CursorTextColor), "Block cursor text color (blank chooses contrast)");
        Add(nameof(Settings.FollowApplicationCursorBlink), "Follow application-requested cursor blinking");
        Add(nameof(Settings.TextBaselineOffset), "Text baseline offset DIPs (-20–20)");
        Add(nameof(Settings.DecorationThickness), "Underline/strike thickness DIPs (0.5–8)");
        Add(nameof(Settings.BalancePadding), "Balance unused cell space around the terminal grid");
        Add(nameof(Settings.MinimumTextContrast), "Minimum contrast against terminal cell colors (1–21)");
        Add(nameof(Settings.MaxOpenTabs), "Maximum tabs per window (1–20)");
        Add(nameof(Settings.MaxOpenPanes), "Maximum panes per window (1–64)");
        Add(nameof(Settings.AutoReloadSettings), "Automatically reload edited settings files");
        Add(nameof(Settings.ReloadDebounceMilliseconds), "Reload debounce in milliseconds (100–5000)");
        Add(nameof(Settings.BackgroundImagePath), "Background image path (blank disables)");
        var browseImage = new Button { Content = "Choose background image...", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 6) };
        browseImage.Click += (_, _) => { var picker = new Microsoft.Win32.OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff" }; if (picker.ShowDialog(this) == true) ((TextBox)appearanceFields[nameof(Settings.BackgroundImagePath)]).Text = picker.FileName; };
        fields.Children.Add(browseImage);
        Add(nameof(Settings.BackgroundImageOpacity), "Background image opacity (0–1)");
        Add(nameof(Settings.BackgroundImageFit), "Background image fit", new[] { "Cover", "Contain", "Stretch", "Center" });
        Add(nameof(Settings.BackgroundImageAlignment), "Background horizontal alignment", new[] { "Left", "Center", "Right" });
        Add(nameof(Settings.BackgroundImageRepeat), "Tile background image");
        Add(nameof(Settings.CopyTrimTrailing), "Trim trailing whitespace when copying");
        Add(nameof(Settings.CopyNewlines), "Copied text line endings", new[] { "Windows", "LF" });
        Add(nameof(Settings.OpenExportsInEditor), "Open exported files in the default application");
        Add(nameof(Settings.TabVisibility), "Tab strip visibility", new[] { "Automatic", "Always", "Never" });
        Add(nameof(Settings.TabPlacement), "Tab strip placement", new[] { "Top", "Bottom" });
        Add(nameof(Settings.TabTitleLength), "Tab title maximum characters (8–200)");
        Add(nameof(Settings.ShowTabCloseButtons), "Show tab close buttons");
        Add(nameof(Settings.MiddleClickClosesTab), "Middle-click a tab to close it");
        Add(nameof(Settings.BoldItalicFontFamily), "Bold-italic font family (blank uses bold/italic fallback)");
        Add(nameof(Settings.ScrollCommandLines), "Lines per scroll command (1–1000)"); Add(nameof(Settings.ScrollPageFraction), "Fraction per page-scroll command (0.05–2)");
        Add(nameof(Settings.CursorShape), "Focused cursor shape", new[] { "Application", "Block", "Bar", "Underline" });
        Add(nameof(Settings.CursorColor), "Cursor color (RRGGBB; blank follows foreground)"); Add(nameof(Settings.CursorOpacity), "Cursor opacity (0–1)");
        Add(nameof(Settings.CursorBlink), "Blink focused cursor"); Add(nameof(Settings.CursorBlinkMilliseconds), "Blink half-period in milliseconds (100–5000)");
        Add(nameof(Settings.UnfocusedCursor), "Unfocused cursor", new[] { "Hollow", "Hidden", "Block", "Bar", "Underline" });
        Add(nameof(Settings.SelectionBackground), "Selection background (RRGGBB)"); Add(nameof(Settings.SearchBackground), "Search highlight (RRGGBB)");
        Add(nameof(Settings.InactivePaneOpacity), "Inactive pane opacity (0.1–1)"); Add(nameof(Settings.CellWidthScale), "Cell width multiplier (0.5–3)"); Add(nameof(Settings.CellHeightScale), "Cell height multiplier (0.5–3)");
        Add(nameof(Settings.PaddingHorizontal), "Horizontal terminal padding (0–100 DIPs)"); Add(nameof(Settings.PaddingVertical), "Vertical terminal padding (0–100 DIPs)");
        Add(nameof(Settings.AutoTerminalTheme), "Follow Windows light/dark mode for terminal colors");
        Add(nameof(Settings.LightTerminalTheme), "Light terminal theme", new[] { "Paper", "Midnight", "Dracula", "Nord" });
        Add(nameof(Settings.DarkTerminalTheme), "Dark terminal theme", new[] { "Paper", "Midnight", "Dracula", "Nord" });
        Add(nameof(Settings.BoldFontFamily), "Bold font family (blank inherits regular)"); Add(nameof(Settings.ItalicFontFamily), "Italic font family (blank inherits regular)");
    }
    bool ReadAppearance(Settings draft)
    {
        try
        {
            foreach (var (name, control) in appearanceFields)
            {
                var property = typeof(Settings).GetProperty(name)!;
                object? value = control switch { CheckBox c => c.IsChecked == true, ComboBox c => c.SelectedItem, TextBox t => Convert.ChangeType(t.Text.Trim(), property.PropertyType, CultureInfo.InvariantCulture), _ => throw new InvalidOperationException() };
                property.SetValue(draft, value);
            }
            draft.ValidateAppearance(); return true;
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException) { Feedback.Text = e.Message; return false; }
    }
}
