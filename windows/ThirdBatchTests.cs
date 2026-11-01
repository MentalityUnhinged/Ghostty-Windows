using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GhosttyWindows;

static class ThirdBatchTests
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception("Third batch: " + name); SelfTest.Passed.Add(name); }
    static IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var item in Children<T>(child)) yield return item;
        }
    }
    public static async Task Run(string artifacts)
    {
        var settings = Settings.LoadFile(Path.Combine(artifacts, "appearance.json"));
        settings.ConfirmClose = false; settings.AnimateWelcome = false;
        settings.ScrollCommandLines = 3; settings.ScrollPageFraction = .25;
        settings.CursorShape = "Bar"; settings.CursorColor = "ff0088"; settings.CursorOpacity = .7;
        settings.CursorBlink = true; settings.CursorBlinkMilliseconds = 250; settings.UnfocusedCursor = "Hidden";
        settings.SelectionBackground = "cc1122"; settings.SearchBackground = "2266aa";
        settings.InactivePaneOpacity = .6; settings.PaddingHorizontal = 26; settings.PaddingVertical = 20;
        settings.CellWidthScale = 1.2; settings.CellHeightScale = 1.3;
        settings.BoldFontFamily = settings.FontFamily; settings.ItalicFontFamily = settings.FontFamily;
        settings.AutoTerminalTheme = true; settings.LightTerminalTheme = "Paper"; settings.DarkTerminalTheme = "Nord";
        settings.Save(); var loaded = Settings.LoadFile(settings.StoragePath);
        Check(loaded.CursorShape == "Bar" && loaded.CursorColor == "ff0088" && loaded.CursorOpacity == .7 && loaded.CursorBlink && loaded.CursorBlinkMilliseconds == 250 && loaded.UnfocusedCursor == "Hidden", "Cursor appearance and timing survive preference reload");
        Check(loaded.SelectionBackground == "cc1122" && loaded.SearchBackground == "2266aa" && loaded.CellHeightScale == 1.3 && loaded.CellWidthScale == 1.2 && loaded.PaddingHorizontal == 26 && loaded.PaddingVertical == 20 && loaded.InactivePaneOpacity == .6, "Highlight, geometry and inactive-pane preferences survive reload");
        Check(loaded.StyledFont(1) == settings.FontFamily && loaded.StyledFont(2) == settings.FontFamily, "Separate style families resolve at the typography owner");
        loaded.SetSystemTheme(false); Check(loaded.Colors.Bg == 0xf4f1e9, "Automatic terminal theme selects the configured light palette");
        loaded.SetSystemTheme(true); Check(loaded.Colors.Bg == 0x2e3440, "Automatic terminal theme selects the configured dark palette");
        foreach (Action<Settings> invalid in new Action<Settings>[] { s => s.ScrollPageFraction = double.NaN, s => s.CellHeightScale = 0, s => s.PaddingVertical = -1, s => s.CursorColor = "oops", s => s.CursorBlinkMilliseconds = 0, s => s.UnfocusedCursor = "wrong", s => s.BoldFontFamily = "NoSuchGhosttyFont-123456" })
        {
            string before = File.ReadAllText(settings.StoragePath); var draft = settings.Edit(); invalid(draft);
            try { settings.Commit(draft); throw new Exception("Invalid appearance preference accepted"); }
            catch (ArgumentException) { Check(File.ReadAllText(settings.StoragePath) == before, "Invalid appearance edit preserves the owned settings file"); }
        }
        var main = new MainWindow(settings, true); main.Show(); await Task.Delay(100);
        try
        {
            var view = main.Active!; int oldCols = view.Engine.Cols, oldRows = view.Engine.Rows;
            var draft = settings.Edit(); draft.CellHeightScale = 1; draft.CellWidthScale = 1; draft.PaddingHorizontal = 10; draft.PaddingVertical = 8; settings.Commit(draft);
            await Task.Delay(60);
            Check(view.Engine.Cols > oldCols && view.Engine.Rows > oldRows, "Cell multipliers and padding update the live terminal grid and native pixel geometry");
            draft = settings.Edit(); draft.PaddingHorizontal = 26; draft.PaddingVertical = 20; settings.Commit(draft);
            Check(view.CellAt(new Point(26, 20)) == new Point(0, 0), "Mouse hit-testing shares the configured terminal origin");
            view.Engine.Reset(true);
            for (int i = 0; i < 120; i++) view.Engine.Feed($"line-{i:000}\r\n");
            main.ExecuteCommand("scroll-line-up"); string first = view.Engine.ScreenText();
            main.ExecuteCommand("scroll-line-down"); Check(first != view.Engine.ScreenText(), "Configured line-scroll commands move the native viewport in both directions");
            main.ExecuteCommand("scroll-fraction-up"); first = view.Engine.ScreenText();
            main.ExecuteCommand("scroll-fraction-down"); Check(first != view.Engine.ScreenText(), "Configured fractional-page commands move the native viewport in both directions");
            view.Engine.Reset(true); view.Engine.Feed("alpha beta\r\ngamma delta\r\nomega");
            view.Engine.Select(3, 0, 4, 0); main.ExecuteCommand("selection-line-start");
            Check(view.Engine.Text(true).Contains("alp"), "Line-start selection extension retains its original anchor");
            main.ExecuteCommand("selection-line-end");
            Check(view.Engine.Text(true).Contains("beta"), "Line-end selection extension reaches the final nonblank cell");
            foreach (string id in new[] { "selection-screen-start", "selection-screen-end", "selection-page-up", "selection-page-down" })
            { Check(main.ExecuteCommand(id) && view.Engine.HasSelection, "Native selection action: " + id); }
            view.Engine.ClearSelection(); view.Engine.Feed("\r\n\x1b[1mBOLD\x1b[0m \x1b[3mITALIC\x1b[0m"); view.RefreshFrame();
            var editor = new SettingsWindow(settings, settings.Commit) { Owner = main }; editor.Show(); editor.UpdateLayout();
            try
            {
                var group = Children<Expander>(editor).Single(); group.IsExpanded = true; editor.UpdateLayout();
                var lineField = Children<TextBox>(group).Single(c => System.Windows.Automation.AutomationProperties.GetName(c).StartsWith("Lines per scroll"));
                lineField.Text = "7"; editor.ApplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(settings.ScrollCommandLines == 7 && editor.Feedback.Text == "Settings saved.", "Appearance expander edits commit through the real Settings Apply button");
                var bitmap = new RenderTargetBitmap((int)editor.ActualWidth, (int)editor.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(editor);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(artifacts, "appearance-settings.png")); png.Save(stream);
            }
            finally { editor.Close(); }
        }
        finally { main.Close(); }
    }
}
