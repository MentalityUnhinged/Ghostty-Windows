using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GhosttyWindows;

sealed class WindowCommand(string id, string label, Action run, Func<bool> canRun, KeyChord? gesture = null, Func<bool>? state = null) : System.ComponentModel.INotifyPropertyChanged
{
    public string Id { get; } = id;
    public string Label { get; } = label;
    public Action Run { get; } = run;
    public Func<bool> CanRun { get; } = canRun;
    public KeyChord? Gesture { get; internal set; } = gesture;
    public Func<bool>? State { get; } = state;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    bool? lastEnabled;
    string? lastDisplay;
    internal void Refresh()
    {
        bool enabled = Enabled; string display = Display;
        if (lastEnabled != enabled) { lastEnabled = enabled; PropertyChanged?.Invoke(this, new(nameof(Enabled))); }
        if (lastDisplay != display) { lastDisplay = display; PropertyChanged?.Invoke(this, new(nameof(Display))); }
    }
    public bool Enabled => CanRun();
    internal IReadOnlyList<KeyChord> AlternateGestures { get; set; } = Array.Empty<KeyChord>();
    internal bool Matches(KeyEventArgs input) => Gesture?.Matches(input) == true || AlternateGestures.Any(g => g.Matches(input));
    public string Display => Label + (State == null ? "" : State() ? " [on]" : " [off]") + (Gesture == null ? "" : "    " + Gesture.Display) + (AlternateGestures.Count == 0 ? "" : " / " + string.Join(" / ", AlternateGestures.Select(g => g.Display)));
}

sealed partial class MainWindow
{
    List<WindowCommand>? commands;
    internal IReadOnlyList<WindowCommand> Commands => commands ??= CreateCommands();
    bool fullscreen;
    Rect normalBounds;
    WindowState previousState;
    WindowStyle previousStyle;
    ResizeMode previousResize;
    void ApplyWindowSettings() { RefreshShortcutLabels(); ApplyTabPreferences(); tools.Visibility = settings.ShowToolbar ? Visibility.Visible : Visibility.Collapsed; status.Visibility = settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed; if (active != null) UpdateStatus(active); }
    internal bool ExecuteCommand(string id)
    {
        var command = Commands.SingleOrDefault(c => c.Id == id) ?? throw new ArgumentException("Unknown command: " + id);
        if (!command.Enabled) return false; command.Run(); if (active != null) UpdateStatus(active); return true;
    }
    List<WindowCommand> CreateCommands()
    {
        var result = new List<WindowCommand>();
        bool Pane() => active != null;
        bool Selection() => active?.Engine.HasSelection == true;
        void Add(string id, string label, Action run, Func<bool>? available = null, KeyChord? gesture = null, Func<bool>? state = null) => result.Add(new(id,label,run,available ?? Pane,gesture,state));
        var controlShift = ModifierKeys.Control | ModifierKeys.Shift;
        Add("reset-terminal", "Reset terminal (clears history and modes)", () => { active!.ResetTerminal(true); }, gesture: null);
        Add("clear-screen", "Clear screen (keep scrollback)", () => active!.ResetTerminal(false), gesture: new(Key.K,controlShift));
        Add("read-only", "Read-only mode", () => active!.ToggleReadOnly(), gesture: new(Key.L,controlShift), state: () => active?.ReadOnly == true);
        Add("mouse-reporting", "Application mouse reporting", () => active!.ToggleMouseReporting(), state: () => active?.MouseReportingEnabled == true);
        Add("reset-font-size", "Reset font size", settings.ResetFontSize, gesture: new(Key.D0,controlShift));
        Add("set-font-size", "Set font size…", SetFontSizeDialog);
        Add("scroll-top", "Scroll to top of history", () => active!.Engine.ScrollEdge(false), gesture: new(Key.Home,controlShift));
        Add("scroll-bottom", "Scroll to bottom", () => active!.Engine.ScrollEdge(true), gesture: new(Key.End,controlShift));
        Add("copy-title", "Copy tab title", () => TerminalView.CopyText(EffectiveTitle()));
        Add("export-screen", "Export visible screen…", () => ExportRegion("screen"));
        Add("export-selection", "Export selection…", () => ExportRegion("selection"), Selection);
        Add("search-selection", "Search selected text", SearchSelection, Selection);
        Add("move-tab-left", "Move tab left", () => MoveTab(-1), () => Current != null && states.IndexOf(Current) > 0, new(Key.PageUp,controlShift));
        Add("move-tab-right", "Move tab right", () => MoveTab(1), () => Current != null && states.IndexOf(Current) < states.Count-1, new(Key.PageDown,controlShift));
        Add("split-zoom", "Zoom active split", ToggleSplitZoom, () => Current?.Views.Count > 1, new(Key.Z,controlShift), () => Current?.ZoomedId != null);
        Add("equalize-splits", "Equalize split areas", EqualizeSplits, () => Current?.Views.Count > 1 && Current.ZoomedId == null);
        foreach (var direction in new[] { Key.Left,Key.Right,Key.Up,Key.Down }) Add("focus-" + direction.ToString().ToLowerInvariant(), "Focus pane " + direction.ToString().ToLowerInvariant(), () => NavigatePane(direction), () => Current?.Views.Count > 1, new(direction,ModifierKeys.Control|ModifierKeys.Alt));
        Add("show-toolbar", "Show toolbar", () => { var draft = settings.Edit(); draft.ShowToolbar = !settings.ShowToolbar; draft.Workspace = CaptureWorkspace(); settings.Commit(draft); }, () => true, state: () => settings.ShowToolbar);
        Add("always-on-top", "Always on top", () => Topmost = !Topmost, () => true, state: () => Topmost);
        Add("decorations", "Window decorations", () => WindowStyle = WindowStyle == WindowStyle.None ? WindowStyle.SingleBorderWindow : WindowStyle.None, () => !fullscreen, state: () => WindowStyle != WindowStyle.None);
        Add("reset-window-size", "Reset window size", () => { if (fullscreen) ToggleFullscreen(); WindowState = WindowState.Normal; Width = 1160; Height = 760; }, () => true);
        Add("focus-follows-mouse", "Focus follows mouse", () => { var draft = settings.Edit(); draft.FocusFollowsMouse = !settings.FocusFollowsMouse; draft.Workspace = CaptureWorkspace(); settings.Commit(draft); }, () => true, state: () => settings.FocusFollowsMouse);
        foreach (string profile in Settings.Profiles.Keys) Add("new-tab-" + profile, "New tab — " + profile, () => NewTab(profile), () => true);
        Add("new-tab-folder", "New tab in folder…", NewTabInFolder, () => true);
        Add("new-window", "New window", () => new MainWindow(settings).Show(), () => true);
        Add("rename-tab", "Rename tab…", RenameTab); Add("save-workspace", "Save workspace now", SaveWorkspace);
        Add("split-right", "Split right", () => Split(true)); Add("split-down", "Split down", () => Split(false)); Add("close-pane", "Close pane", ClosePane);
        Add("find", "Find in terminal", ShowFind); Add("copy", "Copy selection", () => active!.CopySelection(), Selection);
        Add("paste", "Paste", () => active!.Paste(), () => active != null && !active.ReadOnly);
        Add("increase-font-size", "Increase font size", () => active!.Zoom(1)); Add("decrease-font-size", "Decrease font size", () => active!.Zoom(-1));
        Add("export-scrollback", "Export scrollback…", () => ExportRegion("scrollback"));
        Add("fullscreen", "Fullscreen", ToggleFullscreen, () => true, state: () => fullscreen);
        Add("settings", "Settings", SettingsDialog, () => true); AddSecondBatch(result); AddThirdBatch(result); AddFourthBatch(result); AddFifthBatch(result); AddPartialCommands(result); BindStandardShortcuts(result); return result;
    }
    internal string EffectiveTitle() => Current?.Name ?? (active?.Engine.Title is string title && title.Length > 0 ? title : active?.Profile ?? Title);
    internal string ExportContent(string region) => region switch { "screen" => active?.Engine.ScreenText() ?? "", "selection" => active?.Engine.Text(true) ?? "", "scrollback" => active?.Engine.Text() ?? "", "html" => active?.Engine.StyledText() ?? "", _ => throw new ArgumentException("Unknown export region") };
    void ExportRegion(string region)
    {
        string text = ExportContent(region); var picker = new Microsoft.Win32.SaveFileDialog { Filter = region=="html"?"HTML file|*.html":"Text file|*.txt", FileName = region + (region=="html"?".html":".txt") };
        if (picker.ShowDialog(this) != true) return;
        try { File.WriteAllText(picker.FileName,text); OpenExport(picker.FileName); }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { Dialogs.Show(this,failure.Message,"Could not export text"); }
    }
    void SearchSelection()
    {
        string text = active!.Engine.Text(true); if (text.Length == 0) return; ShowFind(); search.Text = text;
    }
    internal string SearchQuery => search.Text;
    void SetFontSizeDialog()
    {
        var dialog = new Window { Title = "Set font size", Owner = this, Width = 360, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        SystemTitleBar.Attach(dialog); var panel = new StackPanel { Margin = new Thickness(20) }; dialog.Content = panel;
        panel.Children.Add(new TextBlock { Text = "Font size (8–48)" }); var input = new TextBox { Text = settings.FontSize.ToString(CultureInfo.CurrentCulture), Margin = new Thickness(0,8,0,8) }; panel.Children.Add(input);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap }; panel.Children.Add(error);
        var apply = new Button { Content = "Apply", IsDefault = true, Margin = new Thickness(0,8,0,0) }; panel.Children.Add(apply);
        apply.Click += (_, _) => { if (!double.TryParse(input.Text,out var size) || !double.IsFinite(size) || size < 8 || size > 48) { error.Text = "Enter a finite size from 8 to 48."; return; } settings.SetRuntimeFontSize(size); dialog.Close(); };
        dialog.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) dialog.Close(); }; dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); }; dialog.ShowDialog();
    }
    internal void MoveTab(int delta)
    {
        var state = Current; if (state == null) return; int from = states.IndexOf(state), to = Math.Clamp(from + delta,0,states.Count-1); if (from == to) return;
        var desired = state.Views[state.ActiveId!]; restoringFocus = true; states.RemoveAt(from); states.Insert(to,state); tabs.Items.Remove(state.Tab); tabs.Items.Insert(to,state.Tab); tabs.SelectedItem = state.Tab; QueueFocus(state,desired);
    }
    internal void ToggleSplitZoom()
    {
        var state = Current; if (state == null || active == null || state.Views.Count < 2) return;
        var desired = active; if (state.ZoomedId == null) { SyncFractions(state); state.ZoomedId = state.ActiveId; } else state.ZoomedId = null;
        restoringFocus = true; Rebuild(state); QueueFocus(state,desired);
    }
    internal void EqualizeSplits()
    {
        var state = Current; if (state == null || active == null) return; var desired = active;
        int Balance(PaneLayout node) { if (node.Kind == "pane") return 1; int a = Balance(node.First!), b = Balance(node.Second!); node.Fraction = (double)a/(a+b); return a+b; }
        Balance(state.Layout); restoringFocus = true; Rebuild(state); QueueFocus(state,desired);
    }
    internal void NavigatePane(Key direction)
    {
        var state = Current; if (state == null || active == null) return;
        if (state.ZoomedId != null) { state.ZoomedId = null; Rebuild(state); UpdateLayout(); }
        Rect Bounds(TerminalView view) => new(view.TranslatePoint(new Point(),state.Root),view.RenderSize);
        Rect start = Bounds(active); Point center = new(start.Left+start.Width/2,start.Top+start.Height/2);
        TerminalView? best = null; double score = double.MaxValue;
        foreach (var view in state.Panes.Where(v => v != active))
        {
            Rect bounds = Bounds(view); double dx = bounds.Left+bounds.Width/2-center.X, dy = bounds.Top+bounds.Height/2-center.Y;
            double primary = direction switch { Key.Left => -dx, Key.Right => dx, Key.Up => -dy, Key.Down => dy, _ => -1 }; if (primary <= .5) continue;
            bool horizontal = direction is Key.Left or Key.Right;
            bool overlap = horizontal ? bounds.Top < start.Bottom && bounds.Bottom > start.Top : bounds.Left < start.Right && bounds.Right > start.Left;
            double candidate = primary + .25 * Math.Abs(horizontal ? dy : dx) + (overlap ? 0 : 100000);
            if (candidate < score) { best = view; score = candidate; }
        }
        QueueFocus(state,best ?? active);
    }
    internal void ToggleFullscreen()
    {
        if (!fullscreen)
        {
            previousStyle = WindowStyle; previousState = WindowState; previousResize = ResizeMode; normalBounds = WindowState == WindowState.Normal ? new Rect(Left,Top,Width,Height) : RestoreBounds;
            fullscreen = true; WindowState = WindowState.Normal; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; WindowState = WindowState.Maximized;
        }
        else
        {
            fullscreen = false; WindowState = WindowState.Normal; WindowStyle = previousStyle; ResizeMode = previousResize;
            Left = normalBounds.Left; Top = normalBounds.Top; Width = normalBounds.Width; Height = normalBounds.Height; WindowState = previousState;
        }
    }
}
