using System.Windows.Input;

namespace GhosttyWindows;

sealed partial class MainWindow
{
    void CapacityNotice(string message) { paneWarningMessage = message; RefreshNotice(); }
    void BindStandardShortcuts(List<WindowCommand> commands)
    {
        var cs = ModifierKeys.Control | ModifierKeys.Shift;
        var bindings = new Dictionary<string, KeyChord>
        {
            ["new-tab-default"] = new(Key.T, cs), ["close-pane"] = new(Key.W, cs),
            ["split-right"] = new(Key.D, cs), ["split-down"] = new(Key.E, cs),
            ["find"] = new(Key.F, cs), ["command-palette"] = new(Key.P, cs),
            ["new-window"] = new(Key.N, cs), ["new-tab-folder"] = new(Key.O, cs),
            ["rename-tab"] = new(Key.R, cs), ["increase-font-size"] = new(Key.OemPlus, cs),
            ["decrease-font-size"] = new(Key.OemMinus, cs), ["select-all"] = new(Key.A, cs),
            ["copy"] = new(Key.C, cs), ["paste"] = new(Key.V, cs),
            ["settings"] = new(Key.OemComma, ModifierKeys.Control), ["fullscreen"] = new(Key.F11),
            ["next-tab"] = new(Key.Tab, ModifierKeys.Control), ["previous-tab"] = new(Key.Tab, cs),
            ["scroll-page-up"] = new(Key.PageUp, ModifierKeys.Shift), ["scroll-page-down"] = new(Key.PageDown, ModifierKeys.Shift)
        };
        for (int i = 1; i <= 9; i++) bindings["goto-tab-" + i] = new(Key.D1 + i - 1, ModifierKeys.Alt);
        for (int i = 0; i < commands.Count; i++) if (bindings.TryGetValue(commands[i].Id, out var gesture)) commands[i].Gesture = gesture;
        int paste = commands.FindIndex(c => c.Id == "paste"); commands[paste].AlternateGestures = new[] { new KeyChord(Key.Insert, ModifierKeys.Shift) };
    }
    void AddPartialCommands(List<WindowCommand> commands)
    {
        void Add(string id, string title, Action action, Func<bool>? available = null) => commands.Add(new(id, title, action, available ?? (() => active != null)));
        Add("terminal-inspector", "Open terminal inspector", () => Inspector.Show(this, active!, Commands));
        Add("export-diagnostics", "Export diagnostics JSON", () => DiagnosticReport.Export(this, active));
        Add("new-tab-default", "New tab (default profile)", () => NewTab(settings.DefaultProfile), () => true);
        Add("command-palette", "Open command palette", Palette, () => true);
        Add("close-tab", "Close active tab", () => CloseTab(Current!), () => Current != null);
        Add("close-window", "Close terminal window", () => Close(), () => true);
        Add("preview-welcome", "Preview animated welcome ghost", () => WelcomeAnimation.Show(this, settings), () => true);
        Add("find-next", "Find next terminal match", () => Find(true), () => active != null && search.Text.Length > 0);
        Add("scroll-page-up", "Scroll one page up", () => active!.Engine.Scroll(-active.Engine.Rows));
        Add("scroll-page-down", "Scroll one page down", () => active!.Engine.Scroll(active.Engine.Rows));
        Add("copy-primary-selection", "Copy selection to primary buffer", () => active!.CopyPrimarySelection(), () => active?.Engine.HasSelection == true);
        Add("paste-primary-selection", "Paste primary selection", () => active!.PastePrimarySelection(), () => active != null && !active.ReadOnly && PrimarySelection.HasValue);
        Add("clear-primary-selection", "Clear primary selection buffer", PrimarySelection.Clear, () => PrimarySelection.HasValue);
        Add("select-all", "Select all terminal text", () => active!.Engine.SelectAll());
        Add("next-tab", "Next tab", () => tabs.SelectedIndex = (tabs.SelectedIndex + 1) % tabs.Items.Count, () => tabs.Items.Count > 1);
        Add("previous-tab", "Previous tab", () => tabs.SelectedIndex = (tabs.SelectedIndex + tabs.Items.Count - 1) % tabs.Items.Count, () => tabs.Items.Count > 1);
        for (int i = 0; i < Workspace.MaxTabs; i++) { int index = i; Add("goto-tab-" + (i + 1), "Go to tab " + (i + 1), () => tabs.SelectedIndex = index, () => tabs.Items.Count > index); }
        Add("clear-selection", "Clear selection", () => active!.ClearLocalSelection(), () => active?.Engine.HasSelection == true);
        Add("close-search", "Close terminal search", () => { searchBar.Visibility = System.Windows.Visibility.Collapsed; active?.Find(""); active?.Focus(); }, () => searchBar.IsVisible);
        Add("copy-hovered-url", "Copy last hovered link address", () => active!.CopyHoveredLink(), () => active?.HoveredLink != null);
    }
}
