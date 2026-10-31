using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;

namespace GhosttyWindows;

static class App
{
    [STAThread]
    public static void Main(string[] args)
    {
        bool test = args.Contains("--self-test");
        string artifacts = args.SkipWhile(x => x != "--artifacts").Skip(1).FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "artifacts");
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Chrome.xaml") });
        if (test) app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.DispatcherUnhandledException += (_, e) => { Directory.CreateDirectory(Settings.Folder); File.AppendAllText(Path.Combine(Settings.Folder, "errors.log"), DateTimeOffset.Now + " " + e.Exception + "\n"); if (!test) Dialogs.Show(e.Exception.Message, "Ghostty Windows"); e.Handled = true; app.Shutdown(1); };
        app.Startup += async (_, _) =>
        {
            try
            {
                if (test)
                {
                    Directory.CreateDirectory(artifacts);
                    await SelfTest.Run(artifacts);
                    await GraphicsTests.Run(artifacts);
                    await WorkspaceTests.Run(artifacts);
                    await KeyboardTests.Run();
                    await SettingsTests.Run(artifacts);
                    await InteractionTests.Run();
                    await ChromeTests.Run(artifacts);
                    SystemTitleBar.VerifyFollowingChanges(); SelfTest.Passed.Add("Native title bar follows dark/light changes without changing Windows preferences");
                    var window = new MainWindow(new Settings { ConfirmClose = false }, true);
                    window.Show(); await Task.Delay(300); window.ExerciseLayout(); SelfTest.Passed.Add("Nested split, pane close, tab creation and tab close"); await Task.Delay(800);
                    window.UpdateLayout();
                    if (window.Icon == null) throw new Exception("Window icon not loaded");
                    SelfTest.Passed.Add("Multi-resolution icon loads in WPF window");
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var file = File.Create(Path.Combine(artifacts, "preview.png"))) encoder.Save(file);
                    var hashes = new[] { "Ghostty.Windows.exe", "Ghostty.Windows.dll", "ghostty-win.dll" }.ToDictionary(name => name, name => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, name)))).ToLowerInvariant());
                    File.WriteAllText(Path.Combine(artifacts, "result.json"), JsonSerializer.Serialize(new { ok = true, tests = SelfTest.Passed, screenshot = "preview.png", artifacts = hashes }, new JsonSerializerOptions { WriteIndented = true }));
                    window.Close(); app.Shutdown(0);
                }
                else { var settings = Settings.Load(); new MainWindow(settings, restore: settings.Workspace).Show(); }
            }
            catch (Exception e)
            {
                if (test) { Directory.CreateDirectory(artifacts); File.WriteAllText(Path.Combine(artifacts, "result.json"), JsonSerializer.Serialize(new { ok = false, error = e.ToString(), passed = SelfTest.Passed })); }
                else Dialogs.Show(e.ToString(), "Ghostty Windows could not start");
                app.Shutdown(1);
            }
        };
        app.Run();
    }
}

static class SelfTest
{
    public static List<string> Passed { get; } = new();
    static void Check(bool ok, string name) { if (!ok) throw new Exception("Test failed: " + name); Passed.Add(name); }
    public static async Task Run(string artifacts)
    {
        using var engine = new Engine();
        engine.Theme(0xdce3ef, 0x11151e);
        engine.Feed("\x1b[2J\x1b[HHello \x1b[38;2;255;128;0mcolor\x1b[0m\r\ncafé 日本語 🙂\r\n");
        Check(engine.Text().Contains("Hello color") && engine.Text().Contains("日本語"), "Ghostty VT parsing and Unicode");
        var frame = engine.Capture();
        Check(frame.Cells.Any(c => c.Fg == 0xff8000), "24-bit color preserved by render adapter");
        engine.Select(0, 0, 4, 0); Check(engine.Text(true) == "Hello", "Selection formatter");
        engine.Feed("unrelated output"); Check(engine.Text(true) == "Hello", "Selection readback after terminal mutation");
        engine.Select(-1, 0, 0, 0);
        Check(Encoding.UTF8.GetString(engine.Key(38, 0, 1)) == "\x1b[A", "Normal cursor key encoding");
        engine.Feed("\x1b[?1h"); Check(Encoding.UTF8.GetString(engine.Key(38, 0, 1)) == "\x1bOA", "Application cursor key encoding");
        Check(Encoding.UTF8.GetString(engine.Key('C', 2, 1)) == "\x03", "Ctrl+C encoding");
        engine.Feed("\x1b[?2004h"); Check(engine.Paste("hello") == "\x1b[200~hello\x1b[201~", "Mode-aware bracketed paste");
        engine.Feed("\x1b[?2004l"); Check(engine.Paste("hello") == "hello", "Unbracketed paste");
        engine.Feed("\x1b[?1000h\x1b[?1006h");
        var mouse = engine.Mouse(0, 1, 0, 50, 40, 10, 20, true);
        Check(mouse != null && Encoding.UTF8.GetString(mouse) == "\x1b[<0;6;3M", "SGR mouse protocol encoding");
        engine.Feed("\x1b[?1000l"); Check(engine.Mouse(0, 1, 0, 50, 40, 10, 20, true) == null, "Mouse tracking disabled by application");
        engine.Feed("\x1b[?1004h"); Check(Encoding.UTF8.GetString(engine.Focus(true)) == "\x1b[I", "Focus reporting");
        engine.Feed("\x1b[HBEFORE\x1b[?2026h\x1b[HAFTER ");
        Check(string.Concat(engine.Capture().Text).Contains("BEFORE"), "Synchronized rendering holds completed frame");
        engine.Feed("\x1b[?2026l"); Check(string.Concat(engine.Capture().Text).Contains("AFTER"), "Synchronized rendering releases next frame");
        engine.Feed("\x1b]2;Windows smoke test\x07"); Check(engine.Title == "Windows smoke test", "OSC title");
        engine.Feed("\x1b[H\x1b]8;;https://ghostty.org\x1b\\Ghostty\x1b]8;;\x1b\\"); Check(engine.Link(0, 0) == "https://ghostty.org", "OSC 8 hyperlink readback");
        var reply = new List<byte>(); engine.Reply += b => reply.AddRange(b); engine.Feed("\x1b[6n"); Check(Encoding.UTF8.GetString(reply.ToArray()).Contains('R'), "Device status response");
        string before = engine.Text(); engine.Feed("\x1b[?1049hALTERNATE\x1b[?1049l"); Check(engine.Text() == before, "Alternate screen round trip");
        for (int i = 0; i < 150; i++) engine.Feed($"scroll-line-{i}\r\n");
        Check(engine.Text().Contains("scroll-line-0"), "Scrollback retained");
        Check(engine.Find("scroll-line-0", true) >= 1, "Search includes scrollback");
        engine.Resize(60, 20, 9, 20); Check(engine.Text().Contains("scroll-line-149"), "Resize preserves history");
        // Exercise real ConPTY I/O with each shell; no mock subprocess or redirected-pipe substitute.
        foreach (string profile in new[] { "Command Prompt", "PowerShell", "WSL" })
        {
            using var terminal = new Engine(); using var pty = new PseudoConsole();
            pty.Data += terminal.Feed; terminal.Reply += pty.Send;
            var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously); pty.Exited += c => exited.TrySetResult(c);
            string cmd = profile switch
            {
                "Command Prompt" => Settings.Profiles[profile] + " /d /q /c echo GHOSTTY_CONPTY_OK",
                "PowerShell" => Settings.Profiles[profile] + " -NoProfile -Command \"Write-Output GHOSTTY_CONPTY_OK\"",
                _ => Settings.Profiles[profile] + " --exec /bin/sh -c \"printf GHOSTTY_CONPTY_OK\""
            };
            pty.Start(cmd, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 100, 30);
            pty.Resize(90, 25); terminal.Resize(90, 25, 9, 20);
            int code = await exited.Task.WaitAsync(TimeSpan.FromSeconds(45));
            for (int i = 0; i < 40 && !terminal.Text().Contains("GHOSTTY_CONPTY_OK"); i++) await Task.Delay(50);
            Check(code == 0 && terminal.Text().Contains("GHOSTTY_CONPTY_OK"), "Real ConPTY " + profile);
        }
        // Test interactive stdin, not just one-shot process output.
        using (var terminal = new Engine())
        using (var pty = new PseudoConsole())
        {
            pty.Data += terminal.Feed; terminal.Reply += pty.Send;
            pty.Start(Settings.Profiles["Command Prompt"] + " /d /q", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 100, 30);
            pty.Send("set /a 936*17\r");
            for (int i = 0; i < 100 && !terminal.Text().Contains("15912"); i++) await Task.Delay(50);
            Check(terminal.Text().Contains("15912"), "Interactive command executes through ConPTY");
            uint pid = pty.ProcessId; pty.Dispose(); await pty.Cleanup.WaitAsync(TimeSpan.FromSeconds(10));
            bool alive; try { using var proc = System.Diagnostics.Process.GetProcessById((int)pid); alive = !proc.HasExited; } catch (ArgumentException) { alive = false; }
            Check(!alive, "Closing session terminates its owned shell and settles I/O");
        }
        File.WriteAllText(Path.Combine(artifacts, "engine-scrollback.txt"), engine.Text());
    }
}
