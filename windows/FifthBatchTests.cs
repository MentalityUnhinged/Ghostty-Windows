using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GhosttyWindows;

static class FifthBatchTests
{
    static void Check(bool value, string label) { if (!value) throw new Exception("Fifth batch: " + label); SelfTest.Passed.Add(label); }
    static async Task Wait(Func<bool> ready) { for (int i = 0; i < 200 && !ready(); i++) await Task.Delay(30); Check(ready(), "Expected asynchronous state reached"); }
    public static async Task Run(string artifacts)
    {
        var owned = new Settings { Workspace = new Workspace { Tabs = new[] { new WorkspaceTab { Root = PaneLayout.Pane("PowerShell", artifacts) } } }, Extra = new Dictionary<string, JsonElement> { ["Vendor"] = JsonSerializer.SerializeToElement(1) } };
        var edit = owned.Edit(); edit.Workspace!.Tabs[0].Root.Directory = "changed"; edit.Extra!["Vendor"] = JsonSerializer.SerializeToElement(2);
        Check(owned.Workspace.Tabs[0].Root.Directory == artifacts && owned.Extra["Vendor"].GetInt32() == 1, "Settings drafts detach mutable workspace and extension data from the live owner");
        using (var pty = new PseudoConsole())
        {
            bool rejected = false; try { pty.Send(new byte[PseudoConsole.MaxQueuedBytes + 1]); } catch (IOException) { rejected = true; }
            Check(rejected, "ConPTY input has a byte bound as well as a message-count bound");
        }
        using (var engine = new Engine())
        {
            engine.Feed("AAA"); engine.Select(0, 0, 2, 0);
            using var cancellation = new CancellationTokenSource();
            var producer = Task.Run(() => { int i = 0; while (!cancellation.IsCancellationRequested) engine.Feed("\x1b[H" + (++i % 2 == 0 ? "AAA" : "BBB")); });
            try { bool consistent = true; for (int i = 0; i < 30; i++) { var snapshot = engine.SelectionClipboard(true); consistent &= snapshot.Html.Contains(snapshot.Plain); } Check(consistent, "Formatted clipboard representations share one native snapshot during output"); }
            finally { cancellation.Cancel(); await producer; }
        }
        using (var pty = new PseudoConsole())
        using (var engine = new Engine())
        {
            pty.Data += engine.Feed;
            string command = "\"" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe") + "\" /Q /K";
            pty.Start(command, artifacts, 100, 30);
            using var process = System.Diagnostics.Process.GetProcessById((int)pty.ProcessId); _ = process.Handle;
            bool rejected = false; try { pty.Start(command, artifacts, 100, 30); } catch (InvalidOperationException) { rejected = true; }
            pty.Send("set /a 936*31\r"); await Wait(() => engine.Text().Contains("29016"));
            Check(rejected, "Rejecting a second ConPTY start preserves the original working owner");
            pty.Dispose(); await pty.Cleanup.WaitAsync(TimeSpan.FromSeconds(10));
            Check(process.HasExited, "ConPTY cleanup settles the process monitor before releasing its process handle");
        }
        Check(!TestRunner.IsRequested(new[] { "--title", "--self-test" }) && TestRunner.IsRequested(new[] { "--test-fifth-batch" }), "Literal launch values cannot select an internal test mode");
        var parsed = LaunchOptions.Parse(new[] { "--profile", "Command Prompt", "--directory", artifacts, "--title", "hello 👻", "--width", "900", "--height", "700", "--maximize", "--fullscreen" });
        Check(parsed.Profile == "Command Prompt" && parsed.Directory == Path.GetFullPath(artifacts) && parsed.Title == "hello 👻" && parsed.Width == 900 && parsed.Height == 700 && parsed.Maximize && parsed.Fullscreen, "CLI options preserve explicit profile, directory, Unicode title and window controls");
        foreach (string[] bad in new[] { new[] { "--wat" }, new[] { "--title" }, new[] { "--width", "NaN" }, new[] { "--height", "1" }, new[] { "--profile", "not installed" }, new[] { "--title", "a", "--title", "b" } })
        {
            bool rejected = false; try { LaunchOptions.Parse(bad); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Invalid or ambiguous launch request is rejected");
        }
        string imagePath = Path.Combine(artifacts, "background.png");
        var source = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat(new byte[] { 0, 0, 255, 255 }, 4).SelectMany(a => a).ToArray(), 8);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source)); using (var stream = File.Create(imagePath)) encoder.Save(stream);
        var image = new BackgroundImage(); image.Load(imagePath); Check(image.Error == null, "Bounded background image decoder loads a real PNG without retaining its stream");
        using (var exclusive = File.Open(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { Check(exclusive.Length > 0, "Decoded image releases the source file handle"); }
        var settings = Settings.LoadFile(Path.Combine(artifacts, "fifth-settings.json")); settings.BackgroundImagePath = imagePath; settings.BackgroundImageOpacity = .5;
        foreach (string fit in new[] { "Cover", "Contain", "Stretch", "Center" })
        {
            settings.BackgroundImageFit = fit; settings.BackgroundImageRepeat = fit == "Center";
            var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) { dc.DrawRectangle(Brushes.Blue, null, new Rect(0, 0, 40, 40)); image.Draw(dc, new Size(40, 40), settings); }
            var bitmap = new RenderTargetBitmap(40, 40, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); var pixels = new byte[40 * 40 * 4]; bitmap.CopyPixels(pixels, 160, 0);
            Check(pixels[(20 * 40 + 20) * 4 + 2] > 100 && pixels[(20 * 40 + 20) * 4] > 100, "Background image opacity composes correctly for " + fit);
        }
        image.Load(Path.Combine(artifacts, "missing.png")); Check(image.Error != null, "Missing background image has an explicit recoverable error");
        settings.BackgroundImagePath = ""; settings.AutoReloadSettings = true; settings.ReloadDebounceMilliseconds = 100; settings.Save();
        string status = ""; using (settings.ObserveFile(message => status = message))
        {
            var candidate = settings.Edit(); candidate.FontSize = 19; File.WriteAllText(settings.StoragePath, JsonSerializer.Serialize(candidate, Settings.JsonOptions));
            await Wait(() => settings.FontSize == 19);
            File.WriteAllText(settings.StoragePath, "{invalid"); await Wait(() => status.Length > 0);
            Check(settings.FontSize == 19, "Malformed external config leaves live preferences intact and reports the failure");
            candidate.FontSize = 18; File.WriteAllText(settings.StoragePath, JsonSerializer.Serialize(candidate, Settings.JsonOptions));
            await Wait(() => settings.FontSize == 18 && status.Length == 0);
        }
        File.WriteAllText(settings.StoragePath, "{\"FontSize\":22}"); await Task.Delay(200);
        Check(settings.FontSize == 18, "Disposing the final config subscription stops queued reloads");
        var launchSettings = Settings.LoadFile(Path.Combine(artifacts, "launch-settings.json")); launchSettings.ConfirmClose = false; launchSettings.ShowWelcome = false; launchSettings.Save(); string before = File.ReadAllText(launchSettings.StoragePath);
        var request = parsed with { Command = "\"" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe") + "\" /Q /K", Maximize = false, Fullscreen = false };
        var window = new MainWindow(launchSettings, launch: request); window.Show();
        try
        {
            window.Active!.Console.Send("set /a 936*29\r"); await Wait(() => window.Active.Engine.Text().Contains("27144"));
            Check(window.Title.Contains("hello 👻") && window.Width == 900 && window.Height == 700 && launchSettings.DefaultProfile == "PowerShell", "Custom CLI command runs through real ConPTY with transient title and window overrides");
        }
        finally { window.Close(); }
        Check(File.ReadAllText(launchSettings.StoragePath) == before, "Closing an explicit CLI launch does not overwrite saved preferences or workspace");
    }
}
