using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GhosttyWindows;

static class PartialCompletionTests
{
    static void Check(bool value, string label) { if (!value) throw new Exception("Partial completion: " + label); SelfTest.Passed.Add(label); }
    static async Task Wait(Func<bool> ready) { for (int i = 0; i < 300 && !ready(); i++) await Task.Delay(40); Check(ready(), "Production boundary reached its expected state"); }
    public static async Task Run(string artifacts)
    {
        Check(Settings.ChooseDefaultFont(new[] { "Consolas" }, "Segoe UI") == "Consolas" && Settings.ChooseDefaultFont(Array.Empty<string>(), "Segoe UI") == "Segoe UI", "First-run font defaults choose installed families without substituting an explicit user choice");
        NativeAbi.Validate(NativeAbi.Current);
        Check(NativeAbi.Current.Version == NativeAbi.Version && NativeAbi.Current.PointerSize == 8, "Live native adapter advertises the matching ABI and capabilities before use");
        foreach (int field in Enumerable.Range(0, 6))
        {
            var bad = NativeAbi.Current;
            switch (field) { case 0: bad.Version++; break; case 1: bad.CellSize++; break; case 2: bad.ImageSize++; break; case 3: bad.SystemImageSize++; break; case 4: bad.PointerSize = 4; break; case 5: bad.Capabilities = 0; break; }
            bool rejected = false; try { NativeAbi.Validate(bad); } catch (PlatformNotSupportedException) { rejected = true; }
            Check(rejected, "Mismatched native ABI field rejected: " + field);
        }
        foreach (string json in new[] { "{\"FontFamily\":\"NoGhosttyFont-999\"}", "{\"Theme\":\"unknown\"}", "{\"DefaultProfile\":\"unknown\"}" })
        { bool rejected = false; try { Settings.LoadJson(json, Path.Combine(artifacts, "bad-preferences.json")); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid font/theme/profile is diagnosed rather than silently substituted"); }
        bool refused = false; try { PastePolicy.Prepare("hello\x1b[201~evil", false, "Reject", out _); } catch (ArgumentException) { refused = true; }
        Check(refused, "Unsafe paste is rejected atomically instead of silently losing controls");
        Check(PastePolicy.Prepare("hello\x1b[201~", false, "Strip", out int removed) == "hello[201~" && removed == 1, "Explicit paste stripping reports the number of removed controls");
        Check(PastePolicy.Prepare("a\r\nb\nc", true, "Reject", out _) == "a b c", "Single-line paste preserves content while normalizing CRLF and LF");
        Check(TextContrast.Ratio(TextContrast.Adjust(0x222222, 0x111111, 4.5), 0x111111) >= 4.49, "Palette-based contrast adjustment meets an attainable target");
        using (var engine = new Engine())
        {
            engine.Feed("\x1b[1 qX"); Check(engine.Capture().CursorBlink, "Application blinking cursor metadata survives the native render boundary");
            engine.Feed("\x1b[2 q"); Check(!engine.Capture().CursorBlink, "Application steady cursor metadata survives the native render boundary");
            string title = new string('x', 800) + "👻"; engine.Feed("\x1b]2;" + title + "\u0007");
            Check(engine.Title == title, "Size-based UTF-8 title readback preserves the engine-supported value");
            bool invalid = false; try { engine.FormatText(-1, 0); } catch (NativeOperationException e) { invalid = e.Result == -2; }
            Check(invalid, "Native formatter distinguishes invalid input from allocation or capacity failures");
            engine.Reset(true); engine.SelectionOptions(". ", 500, 8); engine.Feed("foo.bar");
            engine.Gesture(0, 1, 0, 10, 5, time: 1_000_000_000); engine.Gesture(1, 1, 0, 10, 5);
            engine.Gesture(0, 1, 0, 10, 5, time: 1_100_000_000); engine.Gesture(1, 1, 0, 10, 5);
            Check(engine.Text(true) == "foo", "Custom word-boundary codepoints are consumed by real native selection gestures");
            engine.SelectionOptions(". ", 100, 8); engine.ClearSelection();
            engine.Gesture(0, 1, 0, 10, 5, time: 2_000_000_000); engine.Gesture(1, 1, 0, 10, 5);
            engine.Gesture(0, 1, 0, 10, 5, time: 2_200_000_000); engine.Gesture(1, 1, 0, 10, 5);
            Check(!engine.HasSelection, "Configured repeat-click interval distinguishes separated single clicks");
        }
        string file = Path.Combine(artifacts, "quote ' $ & [test].txt"); File.WriteAllText(file, "file-drop fixture");
        foreach (string profile in Settings.Profiles.Keys)
        {
            string quoted = await ShellFiles.QuoteDrop(profile, new[] { file }, CancellationToken.None);
            using var engine = new Engine(); using var pty = new PseudoConsole(); pty.Data += engine.Feed;
            pty.Start(Settings.Profiles[profile] + (profile == "WSL" ? " --exec /bin/sh -i" : ""), artifacts, 110, 30);
            if (profile == "WSL") await Wait(() => engine.Text().TrimEnd().EndsWith('$') || engine.Text().TrimEnd().EndsWith('#'));
            string command = profile == "PowerShell" ? "if (Test-Path -LiteralPath " + quoted + ") {936*37}\r" : profile == "Command Prompt" ? "if exist " + quoted + " set /a 936*37\r" : "[ -f " + quoted + " ] && printf '%s\\n' \"$((936*37))\"\r";
            pty.Send(command);
            try { await Wait(() => engine.Text().Contains("34632")); }
            finally { File.WriteAllText(Path.Combine(artifacts, "file-drop-" + profile.Replace(' ', '-') + ".txt"), quoted + "\n" + engine.Text()); }
            Check(engine.Text().Contains("34632"), "Real " + profile + " receives the exact quoted file path without expanding its punctuation");
            pty.Dispose(); await pty.Cleanup.WaitAsync(TimeSpan.FromSeconds(10));
        }
        refused = false; try { ShellFiles.Quote("Command Prompt", new[] { Path.Combine(artifacts, "%TEMP%!.txt") }); } catch (ArgumentException) { refused = true; }
        Check(refused, "CMD expansion-sensitive paths are refused without rewriting caller input");
        PrimarySelection.Clear(); Check(!PrimarySelection.HasValue && PrimarySelection.Store("memory-only selection") && PrimarySelection.Read() == "memory-only selection", "Primary selection is an independent process-local buffer");
        Check(!PrimarySelection.Store(new string('x', PrimarySelection.MaxBytes + 1)) && PrimarySelection.Read() == "memory-only selection", "Primary selection enforces its byte budget without truncating the previous value");
        using (var view = new TerminalView(new Settings { ShowWelcome = false, ConfirmMultilinePaste = false }, "Command Prompt"))
        {
            var primaryWindow = new Window { Content = view, Width = 800, Height = 500, ShowInTaskbar = false }; primaryWindow.Show();
            try { PrimarySelection.Store("set /a 936*41\r"); view.PastePrimarySelection(); await Wait(() => view.Engine.Text().Contains("38376")); Check(true, "Primary selection pastes through a real shell without reading or replacing the Windows clipboard"); }
            finally { primaryWindow.Close(); view.Dispose(); await view.Console.Cleanup.WaitAsync(TimeSpan.FromSeconds(10)); PrimarySelection.Clear(); }
        }
        var settings = Settings.LoadFile(Path.Combine(artifacts, "partial-settings.json")); settings.ConfirmClose = false; settings.ShowStatusBar = false; settings.MaxOpenTabs = 1; settings.MaxOpenPanes = 1; settings.BalancePadding = true; settings.ShiftMouseOverride = false;
        settings.Save(); Check(!Settings.LoadFile(settings.StoragePath).ShiftMouseOverride, "Shift override policy persists explicitly");
        var window = new MainWindow(settings, true); window.Show(); await Task.Delay(100);
        try
        {
            int count = window.CaptureWorkspace().Tabs.Length; window.NewTab("PowerShell", true); window.Split(true);
            Check(window.CaptureWorkspace().Tabs.Length == count && window.CaptureWorkspace().Tabs[0].Root.Leaves().Count() == 1, "Per-window limits prevent new tabs and splits without destroying live sessions");
            Check(((DockPanel)window.Content).Children.OfType<TextBlock>().Any(t => t.IsVisible && t.Text.Contains("capacity")), "Capacity feedback remains visible when the status bar is hidden");
            Check(window.Active!.CellAt(new Point(window.Active.OriginX, window.Active.OriginY)) == new Point(0, 0), "Balanced padding shares one origin across layout and hit testing");
            Check(new[] { "select-all", "next-tab", "previous-tab", "goto-tab-1", "clear-selection", "close-search", "copy-hovered-url" }.All(id => window.Commands.Any(c => c.Id == id)), "Palette covers selection, tab navigation, search dismissal and link copy actions");
            Check(window.Commands.Single(c => c.Id == "new-tab-default").Display.Contains("Ctrl+Shift+T") && window.Commands.Single(c => c.Id == "paste").Display.Contains("Shift+Insert"), "Palette shortcut labels come from the same chord owner used for dispatch");
            var commandModel = window.Commands.Single(c => c.Id == "copy"); int changes = 0; commandModel.PropertyChanged += (_, _) => changes++;
            window.Active.Engine.Select(0, 0, 1, 0); commandModel.Refresh(); window.Active.Engine.ClearSelection(); commandModel.Refresh();
            Check(changes > 0 && !commandModel.Enabled, "Command availability notifications track native selection changes");

            var view = window.Active!; view.Engine.Reset(true); view.Engine.Feed("\u2603"); view.RefreshFrame();
            double validSize = settings.FontSize; settings.FontSize = double.NaN; view.InvalidateVisual(); window.UpdateLayout(); await Task.Delay(40);
            int ErrorPixels()
            {
                var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
                var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
                int count = 0; for (int p = 0; p < pixels.Length; p += 4) if (pixels[p + 2] > 100 && pixels[p + 2] > pixels[p + 1] * 1.4) count++; return count;
            }
            Check(window.IsVisible && ErrorPixels() > 5, "Injected managed renderer failure stays inside its pane and renders recovery feedback");
            settings.FontSize = validSize; view.RefreshSettings(); view.RefreshFrame(); window.UpdateLayout(); await Task.Delay(40);
            Check(ErrorPixels() < 5, "Correcting renderer settings recovers the same live pane");

        }
        finally { window.Close(); }
        var failure = new MainWindow(new Settings { ConfirmClose = false }, launch: new LaunchOptions("Command Prompt", artifacts, "Z:\\no-such-ghostty-program.exe", null, null, null, false, false)); failure.Show();
        try { await Wait(() => failure.Active!.Engine.Text().Contains("Unable to start shell")); Check(failure.IsVisible, "Failed shell launch is recoverable inside its own terminal pane"); }
        finally { failure.Close(); }
    }
}
