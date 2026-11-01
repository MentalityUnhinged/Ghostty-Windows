using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace GhosttyWindows;

static class MissingCompletionTests
{
    static void Check(bool value, string label) { if (!value) throw new Exception(label); SelfTest.Passed.Add(label); }
    public static async Task Run(string artifacts)
    {
        Check(KeyChord.Parse("Ctrl+Shift+Plus") == new KeyChord(Key.OemPlus, ModifierKeys.Control | ModifierKeys.Shift), "Custom chord parsing supports punctuation aliases and terminal chords");
        foreach (string bad in new[] { "Ctrl++", "Ctrl+Ctrl+A", "NoModifier+A", "Ctrl+None", "Ctrl+999" }) { bool rejected = false; try { KeyChord.Parse(bad); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid custom chord rejected: " + bad); }
        foreach (var bindings in new[] {
            new[] { new CustomBinding { Chord = "Ctrl+Comma", Action = "ignore" } },
            new[] { new CustomBinding { Chord = "Ctrl+F12", Action = "ignore" }, new CustomBinding { Chord = "Control+F12", Action = "unbind" } },
            new[] { new CustomBinding { Chord = "F12", Action = "send-typo", Value = "x" } },
            new[] { new CustomBinding { Chord = "F12", Action = "send-cursor", Value = "typo" } },
            new[] { new CustomBinding { Chord = "F12", Action = "send-text" } } })
        { bool rejected = false; try { new Settings { KeyBindings = bindings.ToList() }.Validate(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Custom binding conflict/invalid action fails validation"); }
        using (var engine = new Engine())
        {
            foreach (var (action, value, expected) in new[] { ("send-text", "h\u00e9llo", "h\u00e9llo"), ("send-csi", "5~", "\x1b[5~"), ("send-escape", "a", "\u001ba") })
                Check(Encoding.UTF8.GetString(new CustomBinding { Action = action, Value = value }.Encode(engine)) == expected, "Configured " + action + " encodes its declared bytes");
            engine.Feed("\x1b[?1h"); Check(Encoding.ASCII.GetString(new CustomBinding { Action = "send-cursor", Value = "Left" }.Encode(engine)) == "\x1bOD", "Configured cursor actions honor native application-cursor mode"); engine.Feed("\x1b[?1l");
            var reply = new StringBuilder(); engine.Reply += bytes => reply.Append(Encoding.UTF8.GetString(bytes));
            engine.Feed("\x05\x1b[>q"); Check(reply.ToString() == HostIdentity.Text + "\x1bP>|" + HostIdentity.Text + "\x1b\\", "ENQ and XTVERSION return the Windows product/version through real VT callbacks");
            reply.Clear(); engine.Theme(0xffffff, 0); Check(reply.Length == 0, "Theme changes do not send unsolicited disabled reports");
            engine.Feed("\x1b[?996n"); Check(reply.ToString() == "\x1b[?997;1n", "Color scheme query reports the actual dark terminal palette");
            reply.Clear(); engine.Feed("\x1b[?2031h"); engine.Theme(0,0xffffff); Check(reply.ToString() == "\x1b[?997;2n", "Enabled color-scheme mode receives light theme transition");
            reply.Clear(); engine.Theme(0,0xffffff); Check(reply.Length == 0, "Repeated unchanged theme does not duplicate reports");
            engine.Feed("\x1b[c"); Check(reply.ToString() == "\x1b[?62;22c", "DA1 conservatively advertises VT220 and ANSI colors without Sixel/clipboard claims");
            engine.Feed("\x1b]7400;disabled\a"); Check(engine.UnknownSnapshot(true).Total == 0, "Unknown protocol payload capture defaults off");
            engine.CaptureUnknown(true); engine.Feed("\x1b]7400;" + new string('x', 700) + "\a");
            var sample = engine.UnknownSnapshot(true); Check(sample.Total == 1 && sample.Samples[0].CapturedBytes == 256 && sample.Samples[0].Truncated && sample.Samples[0].Kind == "OSC", "Native unknown OSC capture preserves bounded data and truncation");
            for (int i = 0; i < 40; i++) engine.Feed("\x1b]7400;" + i + "\a");
            sample = engine.UnknownSnapshot(false); Check(sample.Total == 41 && sample.Evicted == 9 && sample.Samples.Length == 32 && sample.Samples.All(s => s.Base64 == null), "Diagnostic ring evicts oldest records and omits payloads from default snapshots");
            engine.Feed("\x1b_unknown\x1b\\"); Check(engine.UnknownSnapshot(true).Samples.Last().Kind == "APC", "Unknown APC is retained without executing its payload");
            engine.CaptureUnknown(false); Check(engine.UnknownSnapshot(true).Samples.Length == 0, "Disabling capture erases retained samples");
        }
        var settings = new Settings { ConfirmClose = false, ShowWelcome = false, ShortcutPrecedence = "Terminal", KeyBindings = new() { new() { Chord = "Ctrl+F12", Action = "new-tab-default" }, new() { Chord = "Alt+F12", Action = "ignore" } } };
        settings.Validate(); var edit = settings.Edit(); edit.KeyBindings.Clear(); Check(settings.KeyBindings.Count == 2, "Editing binding lists does not mutate live preferences");
        var window = new MainWindow(settings, true); window.Show(); await Task.Delay(150);
        try
        {
            Check(window.Commands.Single(c => c.Id == "new-tab-default").Display.Contains("F12") && !window.Commands.Single(c => c.Id == "copy").Display.Contains("Ctrl"), "Palette shows effective custom chords and terminal-first precedence");
            int before = window.CaptureWorkspace().Tabs.Length; Check(window.DispatchCustom(KeyChord.Parse("Ctrl+F12")) && window.CaptureWorkspace().Tabs.Length == before + 1, "Custom command binding creates an actual tab");
            var inspector = Inspector.Create(window, window.Active!, window.Commands); inspector.Show(); await Task.Delay(50); Check(inspector.IsVisible, "Terminal inspector opens against a real pane"); inspector.Close();
            window.Active!.Engine.Feed("PRIVATE-SCREEN-SENTINEL");
            string path = Path.Combine(artifacts, "diagnostics.json"); DiagnosticReport.Write(path, window.Active); DiagnosticReport.Write(path, window.Active);
            using var json = JsonDocument.Parse(File.ReadAllText(path)); Check(json.RootElement.GetProperty("artifacts").TryGetProperty("ghostty-win.dll", out _) && File.Exists(path + ".backup"), "User diagnostics export contains artifact identity and preserves previous report");
            Check(!File.ReadAllText(path).Contains("PRIVATE-SCREEN-SENTINEL"), "Diagnostics does not export screen text");
        }
        finally { window.Close(); }
    }
}
