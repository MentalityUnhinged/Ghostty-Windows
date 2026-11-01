using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GhosttyWindows;

static class WelcomeTests
{
    static void Check(bool value, string name) { if (!value) throw new Exception("Welcome check failed: " + name); SelfTest.Passed.Add(name); }
    public static async Task Run(string artifacts)
    {
        using (var engine = new Engine())
        {
            uint foreground = engine.Capture().Cells[0].Fg;
            engine.Feed(Welcome.Render(80));
            Check(engine.Text().Contains("W I N D O W S") && engine.Capture().Cells.Where(c => c.Length > 0).Select(c => c.Fg).Distinct().Count() >= 5, "Welcome art contains a true-color cyan-to-violet gradient");
            var cursor = engine.Capture(); int next = cursor.CursorY * engine.Cols + cursor.CursorX;
            engine.Feed("PROMPT"); var frame = engine.Capture();
            Check(frame.Text[next] == "P" && frame.Cells[next].Fg == foreground, "Welcome resets ANSI styling before subsequent output");
            using var compact = new Engine(); compact.Resize(30, 30, 9, 20); compact.Feed(Welcome.Render(30));
            Check(compact.Text().Contains("Ghostty Windows") && Welcome.Render(10) == "", "Welcome uses compact artwork in narrow panes and skips unusably narrow panes");
        }
        Check(WelcomeAnimation.At(3).Expression == WelcomeAnimation.Eyes.Chevrons && WelcomeAnimation.At(3.8).Expression == WelcomeAnimation.Eyes.At && WelcomeAnimation.At(4.7).Expression == WelcomeAnimation.Eyes.Terminal && WelcomeAnimation.At(4.7).Turn == 0 && WelcomeAnimation.At(5.6).Turn > 0, "Animation loops one rotation followed by three stationary eye states");
        Check(WelcomeAnimation.Render(0, 80, 30, WelcomeAnimation.Eyes.At).Contains('@') && WelcomeAnimation.Render(0, 80, 30).Contains("\x1b[38;2;0;0;0m"), "Eye rendering uses literal at symbols and black foreground masks");
        Check(new[] { WelcomeAnimation.Eyes.Chevrons, WelcomeAnimation.Eyes.At, WelcomeAnimation.Eyes.Terminal }.Select(e => WelcomeAnimation.Render(0, 80, 30, e)).Distinct().Count() == 3, "All three ghost expressions produce distinct ASCII frames");
        Check(Math.Abs(WelcomeAnimation.Bounce(0)) < .0001 && WelcomeAnimation.Bounce(.45) < -.99 && WelcomeAnimation.Bounce(1.35) > .99 && Math.Abs(WelcomeAnimation.Bounce(1.8)) < .0001, "Ghost bounce moves smoothly up and down over a repeating cycle");
        Check(WelcomeAnimation.Render(0, 80, 30, bounce: -1) != WelcomeAnimation.Render(0, 80, 30, bounce: 1), "Bounce moves the rendered ghost while preserving its frame dimensions");
        string facing = WelcomeAnimation.Render(0, 80, 30), turned = WelcomeAnimation.Render(.25, 80, 30);
        Check(facing != turned && facing == WelcomeAnimation.Render(1, 80, 30), "Procedural ASCII ghost rotates and settles at its original orientation");
        using (var animated = new Engine()) { animated.Resize(80, 30, 9, 20); for (int i = 0; i < 40; i++) animated.Feed(WelcomeAnimation.Render(i / 39.0, 80, 30)); Check(animated.Text().Contains("animation preview") && !animated.Text().Contains("W I N D O W S"), "Animation frames replace their own screen region without a shell or startup banner"); }
        using (var preview = new TerminalView(new Settings { FontSize = 11 }, "PowerShell", true))
        {
            var previewWindow = new Window { Content = preview, Width = 880, Height = 760, ShowInTaskbar = false };
            try
            {
                previewWindow.Show(); await Task.Delay(80);
                foreach (var expression in Enum.GetValues<WelcomeAnimation.Eyes>())
                {
                    preview.Engine.Reset(true); preview.Engine.Feed(WelcomeAnimation.Render(0, preview.Engine.Cols, preview.Engine.Rows, expression));
                    preview.RefreshFrame(); previewWindow.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)previewWindow.ActualWidth, (int)previewWindow.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(previewWindow); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = File.Create(Path.Combine(artifacts, "ghost-eyes-" + expression + ".png")); encoder.Save(output);
                }
            }
            finally { previewWindow.Close(); }
        }
        foreach (string profile in Settings.Profiles.Keys)
        {
            Check(Welcome.Command(profile, 80, false) == Settings.Profiles[profile], profile + " launch is unchanged when welcome art is disabled");
            string command = Welcome.Command(profile, 80, true);
            Check(command.Length < 8191, profile + " welcome bootstrap fits the CMD command-line limit");
            using var engine = new Engine(); using var pty = new PseudoConsole(); pty.Data += engine.Feed; engine.Reply += pty.Send;
            try
            {
                pty.Start(command, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 100, 30);
                for (int i = 0; i < 300 && !engine.Text().Contains("W I N D O W S"); i++) await Task.Delay(50);
                Check(engine.Text().Contains("W I N D O W S"), "Real " + profile + " prints gradient welcome through ConPTY");
                Check(engine.Capture().Cells.Where(c => c.Length > 0).Select(c => c.Fg).Distinct().Count() >= 4, "Real " + profile + " preserves welcome gradient colors");
                string input = profile == "PowerShell" ? "Write-Output (936*17)\r" : profile == "Command Prompt" ? "set /a 936*17\r" : "printf '%s\\n' \"$((936*17))\"\r";
                pty.Send(input);
                for (int i = 0; i < 160 && !engine.Text().Contains("15912"); i++) await Task.Delay(50);
                Check(engine.Text().Contains("15912"), "Real " + profile + " remains interactive after the welcome graphic");
            }
            finally { pty.Dispose(); await pty.Cleanup.WaitAsync(TimeSpan.FromSeconds(10)); }
        }
        var view = new TerminalView(new Settings(), "Command Prompt"); var window = new Window { Content = view, Width = 1060, Height = 440, Title = "Ghostty welcome preview" }; window.Show();
        try
        {
            for (int i = 0; i < 200 && !view.Engine.Text().Contains("W I N D O W S"); i++) await Task.Delay(50);
            Check(view.Engine.Text().Contains("W I N D O W S"), "Production terminal launches with welcome enabled by default");
            await Task.Delay(400); view.RefreshFrame(); window.UpdateLayout();
            var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(window);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(Path.Combine(artifacts, "welcome-preview.png")); png.Save(file);
        }
        finally { window.Close(); view.Dispose(); await view.Console.Cleanup.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
}
