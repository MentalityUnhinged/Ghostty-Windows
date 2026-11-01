using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace GhosttyWindows;

static class FourthBatchTests
{
    static void Check(bool value, string label) { if (!value) throw new Exception("Fourth batch: " + label); SelfTest.Passed.Add(label); }
    public static async Task Run(string artifacts)
    {
        var settings = Settings.LoadFile(Path.Combine(artifacts, "fourth-settings.json"));
        settings.ConfirmClose = false; settings.AnimateWelcome = false;
        settings.CopyNewlines = "LF"; settings.CopyTrimTrailing = true;
        Check(ClipboardContent.Plain("a  \r\nb\t\r\n", settings) == "a\nb\n", "Clipboard whitespace and LF transforms preserve line structure");
        settings.CopyTrimTrailing = false; settings.CopyNewlines = "Windows";
        Check(ClipboardContent.Plain("a  \nb\t", settings) == "a  \r\nb\t", "Clipboard preserve policy retains trailing whitespace with Windows newlines");
        string fragment = "<b>ghost 👻 café</b>", html = ClipboardContent.Html(fragment); byte[] bytes = Encoding.UTF8.GetBytes(html);
        int Offset(string key) => int.Parse(Regex.Match(html, key + @":(\d+)").Groups[1].Value);
        Check(Encoding.UTF8.GetString(bytes[Offset("StartFragment")..Offset("EndFragment")]) == fragment && Offset("EndHTML") == bytes.Length, "CF_HTML offsets are UTF-8 byte offsets for Unicode fragments");
        settings.TabVisibility = "Always"; settings.TabPlacement = "Bottom"; settings.TabTitleLength = 12; settings.ShowTabCloseButtons = false; settings.MiddleClickClosesTab = true; settings.Save();
        var loaded = Settings.LoadFile(settings.StoragePath);
        Check(loaded.TabVisibility == "Always" && loaded.TabPlacement == "Bottom" && loaded.TabTitleLength == 12 && !loaded.ShowTabCloseButtons && loaded.MiddleClickClosesTab, "Tab presentation preferences persist across reload");
        using (var native = new Engine())
        {
            native.Feed("\x1b[31malpha<&>\x1b[0m beta  \r\nsecond"); native.Select(0, 0, 7, 0);
            Check(native.FormatText(1, 2).Contains("alpha&lt;&amp;&gt;") && !native.FormatText(1, 2).Contains("second"), "HTML selection export respects native selection and escapes markup");
            Check(native.FormatText(2, 2).Contains("--vt-palette-1:") && native.FormatText(2, 2).Contains("second"), "HTML visible-screen export includes its native color palette");
            Check(native.FormatText(0, 1).Contains("\x1b[") && native.FormatText(1, 1).Contains("alpha"), "ANSI exports preserve native styling for history and selection");
            Check(native.FormatText(2, 0, false).Length >= native.FormatText(2, 0, true).Length, "Native formatter supports caller-selected whitespace policy");
        }
        var main = new MainWindow(settings, true); main.Show(); await Task.Delay(80);
        try
        {
            var original = main.CaptureWorkspace();
            Check(main.ExecuteCommand("duplicate-tab"), "Duplicate-tab command is available within workspace capacity");
            var duplicated = main.CaptureWorkspace();
            Check(duplicated.Tabs.Length == 2 && !duplicated.Tabs[0].Root.Leaves().Select(n => n.Id).Intersect(duplicated.Tabs[1].Root.Leaves().Select(n => n.Id)).Any(), "Duplicated layouts receive distinct identities for fresh sessions");
            main.ClosePane(); Check(main.CaptureWorkspace().Tabs.Length == 1, "Closing the duplicate records its reopen recipe");
            main.ExecuteCommand("reopen-tab"); Check(main.CaptureWorkspace().Tabs.Length == 2, "Reopening a tab recreates its saved layout");
            main.ClosePane(); main.ClosePane();
        }
        finally { if (main.IsVisible) main.Close(); }
        var liveSettings = Settings.LoadFile(Path.Combine(artifacts, "fresh-tab-shells.json"));
        liveSettings.ConfirmClose = false; liveSettings.RestoreTabs = false; liveSettings.ShowWelcome = false; liveSettings.DefaultProfile = "Command Prompt";
        var live = new MainWindow(liveSettings); live.Show();
        try
        {
            var first = live.Active!; live.ExecuteCommand("duplicate-tab"); var second = live.Active!;
            first.Console.Send("set /a 936*23\r"); second.Console.Send("set /a 936*25\r");
            for (int i = 0; i < 120 && (!first.Engine.Text().Contains("21528") || !second.Engine.Text().Contains("23400")); i++) await Task.Delay(50);
            Check(!ReferenceEquals(first, second) && first.Engine.Text().Contains("21528") && second.Engine.Text().Contains("23400"), "Duplicating a live tab retains the original ConPTY and starts an independent shell");
            live.ClosePane(); live.ReopenTab(); var reopened = live.Active!;
            reopened.Console.Send("set /a 936*27\r");
            for (int i = 0; i < 120 && !reopened.Engine.Text().Contains("25272"); i++) await Task.Delay(50);
            Check(!ReferenceEquals(reopened, second) && reopened.Engine.Text().Contains("25272"), "Reopening a closed tab starts a fresh working ConPTY session");
        }
        finally { live.Close(); }
        settings.TabVisibility = "Never"; settings.Save();
        var window = new MainWindow(settings, true); window.Show(); await Task.Delay(50);
        try
        {
            Check(window.TabDisplayTitle("abcdefg👻XYZ").Contains("👻"), "Tab title truncation preserves complete Unicode text elements");
            // Read the public visual template, not a second tab-visibility implementation.
            var root = (DockPanel)window.Content; var tabs = root.Children.OfType<WorkspaceTabs>().Single();
            var headers = (FrameworkElement)tabs.Template.FindName("Headers", tabs);
            Check(headers.Visibility == Visibility.Collapsed && tabs.TabStripPlacement == Dock.Bottom, "Tab visibility and placement apply on initial window creation");
            var draft = settings.Edit(); draft.TabVisibility = "Always"; draft.ShowTabCloseButtons = true; settings.Commit(draft); window.UpdateLayout();
            Check(headers.Visibility == Visibility.Visible, "Changing tab visibility updates the existing window immediately");
            Check(window.Commands.Any(c => c.Id == "copy-formatted") && window.Commands.Any(c => c.Id == "tab-overview") && window.Commands.Any(c => c.Id == "paste-single-line"), "New clipboard and tab actions are discoverable in the command catalogue");
        }
        finally { window.Close(); }
    }
}
