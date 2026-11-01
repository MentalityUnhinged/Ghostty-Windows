using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace GhosttyWindows;

// A procedural ASCII surface, rather than a bitmap or a recording of the website.
static class WelcomeAnimation
{
    internal enum Eyes { Chevrons, At, Terminal }
    static readonly string[] AtGlyph = { " ##### ", "##   ##", "#  ## #", "# # # #", "#  ####", "##     ", " ##### " };
    internal static (double Turn, Eyes Expression, bool Finished) At(double seconds)
    {
        // One turn, then three stationary expressions, repeating as a single loop.
        const double rotation = 2.8, hold = .85;
        double elapsed = Math.Max(0, seconds) % (rotation + hold * 3);
        if (elapsed >= rotation)
        {
            int expression = Math.Min(2, (int)((elapsed - rotation) / hold));
            return (0, (Eyes)expression, false);
        }
        double t = elapsed / rotation;
        double turn = t < .5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
        return (turn, Eyes.Terminal, false);
    }
    internal static bool EyeCutout(double x, double y, Eyes expression)
    {
        bool left = x < 0;
        double ex = (x - (left ? -.26 : .26)) / .17, ey = (y + .15) / .18;
        if (Math.Abs(ex) > 1 || Math.Abs(ey) > 1) return false;
        if (expression == Eyes.At) return true;
        if (expression == Eyes.Terminal && !left) return Math.Abs(ey) < .22;
        if (expression == Eyes.Chevrons && !left) ex = -ex;
        return Math.Abs(ex - (.65 - Math.Abs(ey) * 1.3)) < .3;
    }
    internal static double Bounce(double seconds) => -Math.Sin(seconds * Math.PI * 2 / 1.8);
    internal static string Render(double turn, int columns, int rows, Eyes expression = Eyes.Terminal, double bounce = 0)
    {
        int width = Math.Min(columns, 90), height = Math.Min(rows - 2, 41);
        if (width < 24 || height < 12) return "";
        var chars = Enumerable.Repeat(' ', width * height).ToArray();
        var colors = new int[chars.Length];
        var depth = Enumerable.Repeat(double.PositiveInfinity, chars.Length).ToArray();
        turn -= Math.Floor(turn);
        double yaw = turn * Math.PI * 2, scale = Math.Min((width - 4) / 3.4, (height - 2) / 2.3);
        for (double y = -1; y <= 1.02; y += .025)
        {
            double radius = y < -.3 ? Math.Sqrt(Math.Max(0, .49 - (y + .3) * (y + .3))) : .7;
            for (double angle = 0; angle < Math.PI * 2; angle += .025)
            {
                double x = radius * Math.Cos(angle), z = radius * Math.Sin(angle);
                // Three soft scallops along the hem, carried around the whole surface.
                if (y > .84 + .15 * Math.Cos(angle * 6)) continue;
                double rx = x * Math.Cos(yaw) + z * Math.Sin(yaw), rz = z * Math.Cos(yaw) - x * Math.Sin(yaw);
                int px = (int)Math.Round(width / 2.0 + rx * scale * 2);
                int py = (int)Math.Round(height / 2.0 + y * scale + bounce);
                if (px < 0 || px >= width || py < 0 || py >= height) continue;
                int index = py * width + px;
                if (rz >= depth[index]) continue;
                depth[index] = rz;
                double light = Math.Clamp(.35 + .45 * (-rz / .7) - .2 * rx, 0, 1);
                char symbol = ".:-=+*#%@"[(int)(light * 8)];
                bool eye = z < -.46 && EyeCutout(x, y, expression);
                bool atStroke = false;
                if (eye && expression == Eyes.At)
                {
                    double center = x < 0 ? -.26 : .26;
                    int gx = Math.Clamp((int)(((x - center) / .17 + 1) * 3.5), 0, 6);
                    int gy = Math.Clamp((int)(((y + .15) / .18 + 1) * 3.5), 0, 6);
                    atStroke = AtGlyph[gy][gx] == '#';
                }
                chars[index] = eye ? (atStroke ? '@' : '#') : symbol;
                colors[index] = eye && !atStroke ? 3 : Math.Abs(rx) > radius * .88 ? 1 : 2;
            }
        }
        var output = new StringBuilder("\x1b[?25l\x1b[H");
        for (int y = 0; y < height; y++)
        {
            int previous = -1;
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x, color = colors[index];
                if (color != previous) { output.Append(color == 3 ? "\x1b[38;2;0;0;0m" : color == 1 ? "\x1b[38;2;100;120;255m" : "\x1b[38;2;210;224;245m"); previous = color; }
                output.Append(chars[index]);
            }
            output.Append("\x1b[0m\x1b[K\r\n");
        }
        return output.Append("\x1b[38;2;135;155;255m   G H O S T T Y   /   animation preview\x1b[0m\x1b[K").ToString();
    }
    internal static void Show(Window owner, Settings settings)
    {
        var previewSettings = settings.Edit(); previewSettings.FontSize = 11;
        var view = new TerminalView(previewSettings, "PowerShell", true);
        var dialog = new Window { Owner = owner, Title = "Ghostty animated welcome preview", Width = 880, Height = 760,
            MinWidth = 600, MinHeight = 460, Background = new SolidColorBrush(Color.FromRgb(17, 21, 30)), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        SystemTitleBar.Attach(dialog);
        var root = new DockPanel(); dialog.Content = root;
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        DockPanel.SetDock(controls, Dock.Bottom); root.Children.Add(controls);
        var replay = new Button { Content = "Replay sequence", Padding = new Thickness(12, 6, 12, 6) };
        var close = new Button { Content = "Close", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        controls.Children.Add(replay); controls.Children.Add(close); root.Children.Add(view);
        var clock = new System.Diagnostics.Stopwatch();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        void Paint()
        {
            var state = At(clock.Elapsed.TotalSeconds);
            view.Engine.Feed(Render(state.Turn, view.Engine.Cols, view.Engine.Rows, state.Expression, Bounce(clock.Elapsed.TotalSeconds)));
            if (state.Finished) timer.Stop();
        }
        void Replay() { view.Engine.Reset(true); clock.Restart(); timer.Start(); Paint(); }
        timer.Tick += (_, _) => Paint(); replay.Click += (_, _) => Replay(); close.Click += (_, _) => dialog.Close();
        dialog.Loaded += (_, _) => Replay();
        dialog.SizeChanged += (_, _) => { if (dialog.IsLoaded) { view.Engine.Reset(true); Paint(); } };
        dialog.Closed += (_, _) => { timer.Stop(); view.Dispose(); };
        dialog.ShowDialog();
    }
}
