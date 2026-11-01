using System.IO;
using System.Windows;

namespace GhosttyWindows;

static class AnimatedWelcomeTests
{
    static void Check(bool value, string name) { if (!value) throw new Exception(name); SelfTest.Passed.Add(name); }
    static async Task Until(Func<bool> predicate)
    {
        for (int i = 0; i < 400 && !predicate(); i++) await Task.Delay(50);
        Check(predicate(), "Animated welcome reaches the expected shell state");
    }
    public static async Task Run(string artifacts)
    {
        string path = Path.Combine(artifacts, "animated-settings.json");
        var settings = Settings.LoadFile(path); var draft = settings.Edit(); draft.AnimateWelcome = true; settings.Commit(draft);
        Check(Settings.LoadFile(path).AnimateWelcome && !new Settings().AnimateWelcome, "Animation preference persists and defaults off");
        using (var view = new TerminalView(settings, "Command Prompt"))
        {
            var window = new Window { Content = view, Width = 900, Height = 700, ShowInTaskbar = false };
            try
            {
                window.Show(); await Task.Delay(150);
                Check(view.IntroActive && view.Engine.Text().Contains("G H O S T T Y"), "Real terminal displays animated welcome before starting its shell");
                await Until(() => !view.IntroActive && view.Engine.Text().Contains("W I N D O W S"));
                view.Console.Send("set /a 936*19\r");
                await Until(() => view.Engine.Text().Contains("17784"));
                Check(!view.Engine.Text().Contains("animation preview"), "Animation relinquishes the terminal before command output");
            }
            finally { window.Close(); }
        }
        using (var view = new TerminalView(settings, "Command Prompt"))
        {
            var window = new Window { Content = view, Width = 900, Height = 700, ShowInTaskbar = false };
            try
            {
                window.Show(); await Task.Delay(80); Check(view.IntroActive, "Second session starts its own intro");
                view.FinishIntro(); view.FinishIntro();
                await Until(() => view.Engine.Text().Contains("W I N D O W S"));
                Check(!view.IntroActive, "Skipping the intro is idempotent and starts the real shell");
            }
            finally { window.Close(); }
        }
        using (var view = new TerminalView(settings, "Command Prompt"))
        {
            var window = new Window { Content = view, Width = 900, Height = 700, ShowInTaskbar = false };
            window.Show(); await Task.Delay(80); view.Dispose(); window.Close();
            Check(!view.IntroActive, "Closing during the intro cancels it before shell startup");
        }
    }
}
