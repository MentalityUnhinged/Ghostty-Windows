using System.Windows;
using System.Windows.Controls;

namespace GhosttyWindows;

static class InteractionTests
{
    static void Check(bool value, string name) { if (!value) throw new Exception("Interaction test failed: " + name); SelfTest.Passed.Add(name); }
    static void Equal(string value, string expected, string name) { if (value != expected) throw new Exception($"Interaction test failed: {name}: expected [{expected}], got [{value}]"); SelfTest.Passed.Add(name); }
    static void Pointer(Engine engine, int action, int x, int y, ulong time, bool rectangle = false) => engine.Gesture(action, x, y, x * engine.CellWidthPixels + (action == 2 ? engine.CellWidthPixels - 1 : 1), y * engine.CellHeightPixels + 1, rectangle, time);
    public static async Task Run()
    {
        using (var engine = new Engine())
        {
            engine.Feed("alpha beta gamma");
            Pointer(engine, 0, 7, 0, 1_000_000_000); Pointer(engine, 1, 7, 0, 1_000_000_001);
            Pointer(engine, 0, 7, 0, 1_100_000_000);
            Equal(engine.Text(true), "beta", "Native double-click selects a complete word");
            Pointer(engine, 2, 13, 0, 1_100_000_001);
            Equal(engine.Text(true), "beta gamma", "Native word drag extends by complete words");
            Pointer(engine, 1, 13, 0, 1_100_000_002);
            // Start another timed sequence after clearing the previous gesture.
            engine.Select(-1, 0, 0, 0);
            for (int i = 0; i < 3; i++) { Pointer(engine, 0, 7, 0, 2_000_000_000UL + (ulong)i * 100_000_000); if (i < 2) Pointer(engine, 1, 7, 0, 2_000_000_001); }
            Equal(engine.Text(true), "alpha beta gamma", "Native triple-click selects the logical line");
            engine.Select(-1, 0, 0, 0); Pointer(engine, 0, 2, 0, 3_000_000_000); Pointer(engine, 2, 6, 0, 3_000_000_001);
            Equal(engine.Text(true), "pha b", "Cell drag respects native cell thresholds");
            engine.SelectAll(); Equal(engine.Text(true), "alpha beta gamma", "Select all uses native terminal content bounds");
            engine.Feed("\r\nretained"); Check(engine.Text(true).StartsWith("alpha beta gamma"), "Selection remains tracked after terminal output");
            Pointer(engine, 0, 1, 0, 4_000_000_000); engine.Resize(80, 25, 9, 20); Pointer(engine, 2, 5, 0, 4_000_000_001);
            Equal(engine.Text(true), "", "Resize cancels the native gesture and releases its tracked anchor");
        }
        using (var engine = new Engine())
        {
            engine.Feed("abcdef\r\nghijkl\r\nmnopqr");
            Pointer(engine, 0, 1, 0, 1_000_000_000); Pointer(engine, 2, 3, 2, 1_000_000_001, true);
            Equal(engine.Text(true), "bcd\nhij\nnop", "Native rectangular drag selects columns across rows");
        }
        using (var engine = new Engine())
        {
            engine.Resize(12, 5, 9, 20); engine.Feed("alpha beta gamma delta");
            for (int i = 0; i < 3; i++) { Pointer(engine, 0, 1, 0, (ulong)(1_000_000_000 + i * 100_000_000)); if (i < 2) Pointer(engine, 1, 1, 0, 1_000_000_001); }
            Equal(engine.Text(true), "alpha beta gamma delta", "Triple-click selection follows a soft-wrapped logical line");
        }
        using (var engine = new Engine())
        {
            engine.Resize(30, 3, 9, 20); for (int i = 0; i < 15; i++) engine.Feed($"line {i}\r\n");
            engine.Scroll(-8); string anchor = string.Concat(engine.Capture().Text.Take(30)).TrimEnd();
            Pointer(engine, 0, 0, 0, 1_000_000_000); engine.Scroll(1); Pointer(engine, 2, 8, 2, 1_000_000_001);
            Check(anchor.Length > 0 && engine.Text(true).StartsWith(anchor), "Selection anchor stays attached to history across viewport scrolling");
            engine.SelectAll(); Check(engine.Text(true).Contains("line 0") && engine.Text(true).Contains("line 14"), "Select all includes scrollback outside the viewport");
        }
        using (var engine = new Engine())
        {
            engine.Resize(24, 6, 9, 20); const string target = "https://example.org/path?q=hello";
            engine.Feed("link: " + target + "\r\nnext");
            var frame = engine.Capture(); var links = TerminalLinks.Detect(frame, engine.Cols);
            Check(links.Length == 1 && links[0].Target == target && links[0].Cells.Any(c => c >= engine.Cols), "Detected web link follows native soft-wrap metadata");
            engine.Feed("\x1b[2J\x1b[Hhttps://example.org\r\n/other");
            Equal(TerminalLinks.Detect(engine.Capture(), engine.Cols).Single().Target, "https://example.org", "Hard line breaks cannot join unrelated text into a link");
            engine.Feed("\x1b[2J\x1b[H(https://a.test/a(b)).");
            Equal(TerminalLinks.Detect(engine.Capture(), engine.Cols).Single().Target, "https://a.test/a(b)", "URL punctuation trimming preserves balanced path parentheses");
            engine.Feed("\x1b[2J\x1b[Hhttps://例.test/日本"); frame = engine.Capture(); links = TerminalLinks.Detect(frame, engine.Cols);
            Equal(links.Single().Target, "https://例.test/日本", "Unicode URL detection skips wide-cell spacers");
            Check(links.Single().Cells.Any(c => (frame.Cells[c].Flags & 256) != 0), "Wide-cell URL hit area includes both halves of a glyph");
            engine.Resize(16, 6, 9, 20); engine.Feed("\x1b[2J\x1b[Hhttps://a.test/日本"); frame = engine.Capture();
            Check(frame.Cells.Any(c => (c.Flags & 512) != 0) && TerminalLinks.Detect(frame, engine.Cols).Single().Target == "https://a.test/日本", "Wide glyph wrapping skips the native spacer at the preceding row edge");
            engine.Resize(12, 3, 9, 20); engine.Feed("\x1b[2J\x1b[Hhttps://a.test/" + new string('x', 60)); engine.Scroll(-1000);
            Check(TerminalLinks.Detect(engine.Capture(), engine.Cols).Length == 0, "Viewport-truncated URLs cannot produce partial destinations");
            engine.Resize(24, 6, 9, 20); engine.Scroll(0);
            engine.Feed("\x1b[2J\x1b[H\x1b[8mhttps://hidden.test\x1b[0m");
            Check(TerminalLinks.Detect(engine.Capture(), engine.Cols).Length == 0, "Invisible terminal text cannot create detected links");
            Check(!TerminalLinks.IsWeb("file:///C:/test.exe") && !TerminalLinks.IsWeb("javascript:alert(1)") && !TerminalLinks.IsWeb("https://"), "Link actions accept only valid HTTP and HTTPS destinations");
            string longLink = "https://example.org/" + new string('x', 1800); engine.Feed("\x1b[2J\x1b[H\x1b]8;;" + longLink + "\x1b\\label\x1b]8;;\x1b\\");
            Equal(engine.Link(0, 0), longLink, "Size-queried OSC 8 destination preserves a long URI within the native parser limit");
        }
        var view = new TerminalView(new Settings(), "PowerShell", true); var window = new Window { Content = view, Width = 800, Height = 400 };
        window.Show(); await Task.Delay(80);
        try
        {
            view.Engine.Feed("https://example.org"); view.RefreshFrame();
            Equal(view.LinkAt(new Point(12, 10)) ?? "", "https://example.org", "Production terminal resolves detected link hit targets");
            var menu = view.BuildContextMenu(new Point(12, 10));
            Check(menu.Items.OfType<MenuItem>().Any(i => (string)i.Header == "Open link") && !menu.Items.OfType<MenuItem>().Single(i => (string)i.Header == "Copy").IsEnabled, "Real context menu includes link actions and disables Copy without selection");
            menu.Items.OfType<MenuItem>().Single(i => (string)i.Header == "Select all").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Equal(view.Engine.Text(true), "https://example.org", "Context-menu Select all operates on the production terminal");
            view.Engine.Feed("\x1b[2J\x1b[H\x1b]8;;https://target.test\x1b\\https://visible.test\x1b]8;;\x1b\\"); view.RefreshFrame();
            Equal(view.LinkAt(new Point(12, 10)) ?? "", "https://target.test", "Explicit OSC 8 target takes precedence over visible URL text");
            view.Engine.Feed("\x1b[?2026h"); Check(view.LinkAt(new Point(12, 10)) == null, "Links cannot activate against a frozen synchronized frame"); view.Engine.Feed("\x1b[?2026l");
            Check(view.LinkAt(new Point(0, 0)) == null, "Terminal padding does not activate a neighboring link");
        }
        finally { window.Close(); view.Dispose(); }
    }
}
