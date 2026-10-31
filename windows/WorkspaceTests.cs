using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace GhosttyWindows;

static class WorkspaceTests
{
    static void Check(bool value, string name) { if (!value) throw new Exception("Workspace test failed: " + name); SelfTest.Passed.Add(name); }
    static void Reject(Workspace workspace, string name)
    {
        bool rejected = false; try { workspace.Validate(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, name);
    }
    public static async Task Run(string artifacts)
    {
        string folder = Path.Combine(artifacts,"workspace launch folder"); Directory.CreateDirectory(folder);
        string path = Path.Combine(artifacts,"workspace-settings-test.json");
        string legacy = JsonSerializer.Serialize(new { Tabs = new[] {"PowerShell","WSL"}, WorkingDirectory = folder, VendorOptions = new { Keep = 42 } });
        File.WriteAllText(path,legacy);
        var settings = Settings.LoadFile(path);
        Check(settings.Workspace?.Tabs.Select(t => t.Root.Profile).SequenceEqual(new[] {"PowerShell","WSL"}) == true, "Legacy flat tabs migrate to workspace layout");
        settings.SaveFile(path);
        Check(File.ReadAllText(path + ".backup") == legacy, "Atomic settings save backs up the previous file");
        using (var document = JsonDocument.Parse(File.ReadAllText(path)))
            Check(!document.RootElement.TryGetProperty("Tabs",out _) && document.RootElement.GetProperty("VendorOptions").GetProperty("Keep").GetInt32() == 42,
                "Settings migration removes obsolete tabs and preserves unknown options");
        Check(Settings.LoadFile(path).Workspace!.Tabs[0].Root.Id == settings.Workspace!.Tabs[0].Root.Id, "Workspace identities survive file serialization");
        string invalidPath = Path.Combine(artifacts,"invalid-legacy-workspace.json");
        const string invalidLegacy = "{\"Tabs\":[\"UnavailableCustomShell\"]}"; File.WriteAllText(invalidPath,invalidLegacy);
        bool legacyRejected = false; try { Settings.LoadFile(invalidPath); } catch (InvalidDataException) { legacyRejected = true; }
        Check(legacyRejected && File.ReadAllText(invalidPath) == invalidLegacy, "Unsupported legacy profiles are preserved rather than silently dropped");
        var a = PaneLayout.Pane("PowerShell",folder); var b = PaneLayout.Pane("Command Prompt",folder); var c = PaneLayout.Pane("WSL",folder);
        var inner = PaneLayout.Split(false,b,c); inner.Fraction = .64;
        var root = PaneLayout.Split(true,a,inner); root.Fraction = .31;
        var workspace = new Workspace { Width = 1100, Height = 720, Tabs = new[] {
            new WorkspaceTab { Root = root, ActivePaneId = c.Id, Name = "Build & logs" },
            new WorkspaceTab { Root = PaneLayout.Pane("Command Prompt",folder), Name = "Tools" }
        } };
        workspace.Validate();
        Reject(new Workspace { Version = 99 }, "Future workspace schema rejected without overwriting settings");
        var duplicate = PaneLayout.Split(true,a.Copy(),a.Copy());
        Reject(new Workspace { Tabs = new[] {new WorkspaceTab {Root = duplicate}} }, "Duplicate pane identities rejected");
        var badSplit = root.Copy(); badSplit.Fraction = 1.5;
        Reject(new Workspace { Tabs = new[] {new WorkspaceTab {Root = badSplit}} }, "Invalid split proportions rejected");
        var cyclic = PaneLayout.Split(true,a.Copy(),b.Copy()); cyclic.First = cyclic;
        Reject(new Workspace { Tabs = new[] {new WorkspaceTab {Root = cyclic}} }, "Cyclic workspace model rejected");
        var invalidFocus = new Workspace { Tabs = new[] {new WorkspaceTab {Root = a.Copy(), ActivePaneId = "unknown"}} };
        Reject(invalidFocus, "Active pane must belong to its restored tab");
        PaneLayout Balanced(PaneLayout[] leaves) => leaves.Length == 1 ? leaves[0] : PaneLayout.Split(true, Balanced(leaves[..(leaves.Length/2)]), Balanced(leaves[(leaves.Length/2)..]));
        Reject(new Workspace { Tabs = new[] {new WorkspaceTab {Root = Balanced(Enumerable.Range(0,65).Select(_ => PaneLayout.Pane("PowerShell",folder)).ToArray())}} }, "Automatic workspace shell count is bounded");

        var preferences = new Settings { ConfirmClose = false };
        var first = new MainWindow(preferences, true, workspace); first.Show();
        Workspace snapshot;
        try
        {
            await Task.Delay(250); first.UpdateLayout();
            snapshot = first.CaptureWorkspace();
            Check(snapshot.Tabs.Length == 2 && snapshot.Tabs[0].Root.Leaves().Count() == 3 && snapshot.Tabs[0].Root.Kind == "horizontal" && snapshot.Tabs[0].Root.Second!.Kind == "vertical", "Real window restores nested splits and shell profiles");
            Check(Math.Abs(snapshot.Tabs[0].Root.Fraction!.Value - .31) < .025 && Math.Abs(snapshot.Tabs[0].Root.Second!.Fraction!.Value - .64) < .025, "Restored split ratios match saved geometry");
            Check(snapshot.Tabs[0].ActivePaneId == c.Id && first.Active?.Profile == "WSL", "Restored active pane receives focus");
            Check(snapshot.Tabs[0].Name == "Build & logs" && snapshot.Tabs[0].Root.Leaves().All(p => p.Directory == folder), "Named tabs and launch folders restored");
            var tabs = ((DockPanel)first.Content).Children.OfType<TabControl>().Single();
            tabs.SelectedIndex = 1; await Task.Delay(50); tabs.SelectedIndex = 0; await Task.Delay(50);
            Check(first.CaptureWorkspace().Tabs[0].ActivePaneId == c.Id && first.Active?.Profile == "WSL", "Switching tabs remembers each tab's active pane");
            var split = (Grid)((Grid)((TabItem)tabs.Items[0]).Content).Children[0];
            split.ColumnDefinitions[0].Width = new GridLength(.27,GridUnitType.Star); split.ColumnDefinitions[2].Width = new GridLength(.73,GridUnitType.Star); first.UpdateLayout();
            snapshot = first.CaptureWorkspace(); Check(Math.Abs(snapshot.Tabs[0].Root.Fraction!.Value - .27) < .025, "User-adjusted pane sizes are captured from live layout");
            first.Active!.Engine.Feed("\x1b]2;shell tries to rename the tab\x07"); await Task.Delay(100);
            Check(((TextBlock)((StackPanel)((TabItem)tabs.Items[0]).Header).Children[0]).Text == "Build & logs", "Custom tab name survives shell title updates");
            first.Active.Engine.Feed("\x1b]7;" + new Uri(folder).AbsoluteUri + "\x07");
            Check(Path.GetFullPath(first.Active.DirectoryForNewSession).Equals(Path.GetFullPath(folder),StringComparison.OrdinalIgnoreCase), "OSC 7 local working directory decoded for new sessions");
            first.Active.Engine.Feed("\x1b]7;file://foreign.example/not-a-local-launch-folder\x07");
            Check(first.Active.DirectoryForNewSession == folder, "Foreign OSC directory does not replace local launch folder");
            snapshot = first.CaptureWorkspace();
        }
        finally { first.Close(); }
        preferences.Workspace = snapshot; preferences.SaveFile(path);
        var persisted = Settings.LoadFile(path).Workspace!;
        var second = new MainWindow(preferences,true,persisted); second.Show();
        try
        {
            await Task.Delay(200); second.UpdateLayout();
            var reopened = second.CaptureWorkspace();
            Check(reopened.Tabs[0].Root.Leaves().Select(p => (p.Id,p.Profile,p.Directory)).SequenceEqual(snapshot.Tabs[0].Root.Leaves().Select(p => (p.Id,p.Profile,p.Directory))) && reopened.Tabs[0].Name == snapshot.Tabs[0].Name && reopened.Tabs[0].ActivePaneId == snapshot.Tabs[0].ActivePaneId, "Save/reopen round trip preserves pane identity, folders, names and focus");
            Check(Math.Abs(reopened.Tabs[0].Root.Fraction!.Value - snapshot.Tabs[0].Root.Fraction!.Value) < .025, "Save/reopen round trip preserves resized pane proportions");
        }
        finally { second.Close(); }
        // Exercise the production TerminalView startup path, not only a raw ConPTY helper.
        using var terminal = new TerminalView(new Settings(),"Command Prompt", directory:folder);
        var shellWindow = new Window { Content = terminal, Width = 700, Height = 400, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            shellWindow.Show(); await Task.Delay(100); terminal.Console.Send("cd\r");
            for (int i = 0; i < 100 && !terminal.Engine.Text().Contains(folder,StringComparison.OrdinalIgnoreCase); i++) await Task.Delay(50);
            Check(terminal.Engine.Text().Contains(folder,StringComparison.OrdinalIgnoreCase), "Production pane launches real shell in selected folder");
        }
        finally { shellWindow.Close(); terminal.Dispose(); await terminal.Console.Cleanup.WaitAsync(TimeSpan.FromSeconds(10)); }
        var liveSettings = Settings.LoadFile(Path.Combine(artifacts,"live-workspace-settings.json"));
        liveSettings.ConfirmClose = false; liveSettings.RestoreTabs = false; liveSettings.DefaultProfile = "Command Prompt"; liveSettings.WorkingDirectory = folder;
        var live = new MainWindow(liveSettings); live.Show();
        var original = live.Active!; TerminalView? neighbor = null;
        try
        {
            for (int i = 0; i < 100 && original.Console.ProcessId == 0; i++) await Task.Delay(25);
            uint pid = original.Console.ProcessId; Check(pid > 0, "Real workspace starts its selected shell");
            live.Split(true); neighbor = live.Active!; await Task.Delay(100);
            Check(original.Console.ProcessId == pid && !ReferenceEquals(original,neighbor), "Creating a split preserves the existing shell owner");
            live.ClosePane(); await neighbor.Console.Cleanup.WaitAsync(TimeSpan.FromSeconds(10));
            Check(ReferenceEquals(live.Active,original) && original.Console.ProcessId == pid, "Closing a neighbor promotes the existing pane without replacement");
            original.Console.Send("set /a 936*19\r");
            for (int i = 0; i < 100 && !original.Engine.Text().Contains("17784"); i++) await Task.Delay(25);
            Check(original.Engine.Text().Contains("17784"), "Original shell remains interactive after split and close");
        }
        finally { live.Close(); await original.Console.Cleanup.WaitAsync(TimeSpan.FromSeconds(10)); if (neighbor != null) await neighbor.Console.Cleanup.WaitAsync(TimeSpan.FromSeconds(10)); }
        Check(File.Exists(Path.Combine(artifacts,"live-workspace-settings.json")), "Workspace saves to its owning configuration path");
        File.WriteAllText(Path.Combine(artifacts,"workspace-roundtrip.json"), JsonSerializer.Serialize(snapshot,Settings.JsonOptions));
    }
}
