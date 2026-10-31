using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Markup;

namespace GhosttyWindows;

sealed class MainWindow : Window
{
    readonly Settings settings;
    readonly WorkspaceTabs tabs = new();
    readonly TextBox search = new() { Width = 260, Margin = new Thickness(6), ToolTip = "Search active pane and scrollback. Enter: next result" };
    readonly TextBlock matchCount = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6) };
    readonly StackPanel searchBar = new() { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
    readonly TextBlock status = new() { Margin = new Thickness(14, 5, 14, 5), Foreground = Brushes.SlateGray };
    readonly List<TabState> states = new();
    TerminalView? active;
    bool closing;
    bool restoringFocus;
    int selectionVersion;
    readonly bool demoMode;
    SettingsWindow? settingsWindow;
    internal sealed class TabState
    {
        public required TabItem Tab; public required Grid Root; public required PaneLayout Layout;
        public readonly Dictionary<string, TerminalView> Views = new();
        public readonly Dictionary<string, Grid> Splits = new();
        public List<TerminalView> Panes => Layout.Leaves().Select(p => Views[p.Id]).ToList();
        public string? ActiveId, Name;
    }
    public MainWindow(Settings settings, bool demo = false, Workspace? restore = null)
    {
        this.settings = settings; demoMode = demo; Title = "Ghostty Windows · Preview"; Width = 1160; Height = 760; MinWidth = 540; MinHeight = 320;
        SystemTitleBar.Attach(this);
        Icon = new System.Windows.Media.Imaging.IconBitmapDecoder(new Uri("pack://application:,,,/Assets/ghostty.ico"), System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames.MaxBy(f => f.PixelWidth);
        Background = new SolidColorBrush(Color.FromRgb(0x11, 0x15, 0x1e)); Foreground = Brushes.Gainsboro;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new DockPanel(); Content = root;
        var tools = new WrapPanel { Background = new SolidColorBrush(Color.FromRgb(0x1b, 0x21, 0x2d)), Margin = new Thickness(0) };
        DockPanel.SetDock(tools, Dock.Top); root.Children.Add(tools);
        var brand = new TextBlock { Text = "  GHOSTTY   /   WINDOWS PREVIEW  ", FontWeight = FontWeights.SemiBold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4), Foreground = Brushes.LightSteelBlue };
        tools.Children.Add(brand);
        tools.Children.Add(Button("+ Tab", () => NewTab(settings.DefaultProfile), "Ctrl+Shift+T"));
        tools.Children.Add(Button("Shell ▾", ShowProfiles));
        tools.Children.Add(Button("Split →", () => Split(true), "Ctrl+Shift+D"));
        tools.Children.Add(Button("Split ↓", () => Split(false), "Ctrl+Shift+E"));
        tools.Children.Add(Button("Find", ShowFind, "Ctrl+Shift+F"));
        tools.Children.Add(Button("Commands", Palette, "Ctrl+Shift+P"));
        tools.Children.Add(Button("Settings", SettingsDialog));
        DockPanel.SetDock(searchBar, Dock.Top); root.Children.Add(searchBar);
        searchBar.Children.Add(search); searchBar.Children.Add(Button("Next", () => Find(true))); searchBar.Children.Add(matchCount);
        searchBar.Children.Add(Button("×", () => { searchBar.Visibility = Visibility.Collapsed; active?.Find(""); active?.Focus(); }));
        search.TextChanged += (_, _) => Find(false);
        search.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Find(true); e.Handled = true; } };
        DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status); status.Visibility = settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
        tabs.Background = Brushes.Transparent; tabs.BorderThickness = new Thickness(0);
        tabs.SelectionStarting = () => restoringFocus = true;
        tabs.SelectionFinished = () =>
        {
            if (Current is not TabState state) { ++selectionVersion; restoringFocus = false; return; }
            var desired = state.ActiveId != null && state.Views.TryGetValue(state.ActiveId, out var remembered) ? remembered : state.Panes[0];
            QueueFocus(state,desired);
        };
        root.Children.Add(tabs);
        tabs.Resources.Add(typeof(TabItem), XamlReader.Parse("""
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="TabItem">
          <Setter Property="Foreground" Value="#aab8cf"/><Setter Property="Background" Value="#1b212d"/>
          <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TabItem">
            <Border Name="Tile" Background="{TemplateBinding Background}" Padding="4,2" BorderThickness="0,0,0,2" BorderBrush="Transparent" Margin="1,2,1,0">
              <ContentPresenter ContentSource="Header"/>
            </Border>
            <ControlTemplate.Triggers><Trigger Property="IsSelected" Value="True"><Setter TargetName="Tile" Property="Background" Value="#293345"/><Setter TargetName="Tile" Property="BorderBrush" Value="#81a8ff"/><Setter Property="Foreground" Value="#e6edfa"/></Trigger></ControlTemplate.Triggers>
          </ControlTemplate></Setter.Value></Setter>
        </Style>
        """));
        PreviewKeyDown += HandleShortcut;
        Closing += (_, e) =>
        {
            if (!demo && settings.ConfirmClose && !closing && states.Any(s => s.Panes.Count > 0) && Dialogs.Show(this, "Close all terminal sessions and their running processes?", "Close Ghostty Windows", MessageBoxButton.OKCancel) != MessageBoxResult.OK) { e.Cancel = true; return; }
            closing = true;
            if (!demo)
            {
                try { settings.Workspace = CaptureWorkspace(); settings.Save(); }
                catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
                { e.Cancel = true; closing = false; Dialogs.Show(this, "Could not save the workspace. Your sessions remain open.\n\n" + failure.Message, "Save workspace"); return; }
            }
            foreach (var view in states.SelectMany(s => s.Panes)) view.Dispose();
        };
        if (settings.RestoreTabs && restore is { Tabs.Length: > 0 })
        {
            restore.Validate(); Width = restore.Width; Height = restore.Height;
            foreach (var tab in restore.Tabs) CreateTab(tab.Root.Copy(), tab.Name, tab.ActivePaneId);
            tabs.SelectedIndex = restore.SelectedTab;
            if (restore.Maximized) WindowState = WindowState.Maximized;
        }
        else NewTab(demo ? "PowerShell" : settings.DefaultProfile, demo);
        Loaded += (_, _) => active?.Focus();
    }
    TabState? Current => states.FirstOrDefault(s => s.Tab == tabs.SelectedItem);
    static Button Button(string label, Action action, string? tip = null)
    {
        var b = new Button { Content = label, ToolTip = tip, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(3), Background = new SolidColorBrush(Color.FromRgb(0x29, 0x33, 0x45)), Foreground = Brushes.Gainsboro, BorderThickness = new Thickness(0), Focusable = false };
        b.Click += (_, _) => action(); return b;
    }
    TerminalView AddPane(TabState state, PaneLayout node, bool demo = false)
    {
        var view = new TerminalView(settings, node.Profile!, demo, node.Directory); state.Views.Add(node.Id, view);
        view.Activated += v => { if (Current == state && !restoringFocus) { active = v; state.ActiveId = node.Id; UpdateStatus(v); } };
        view.TitleChanged += text => { if (state.ActiveId == node.Id) { UpdateTabTitle(state, text); if (Current == state) Title = (state.Name ?? text) + " — Ghostty Windows"; } };
        var border = new Border { Child = view, BorderBrush = new SolidColorBrush(Color.FromRgb(0x35, 0x43, 0x5c)), BorderThickness = new Thickness(1), Margin = new Thickness(1) };
        view.GotKeyboardFocus += (_, _) => { foreach (var v in states.SelectMany(s => s.Panes)) if (v.Parent is Border b) b.BorderBrush = v == view ? Brushes.CornflowerBlue : Brushes.DarkSlateGray; };
        return view;
    }
    public void NewTab(string profile, bool demo = false, string? directory = null)
    {
        if (states.Count >= Workspace.MaxTabs || states.Sum(s => s.Views.Count) >= Workspace.MaxPanes) { status.Text = "Workspace capacity reached (20 tabs / 64 panes)."; return; }
        demo |= demoMode;
        CreateTab(PaneLayout.Pane(profile, directory ?? settings.WorkingDirectory), null, null, demo);
    }
    void CreateTab(PaneLayout layout, string? name, string? activeId, bool demo = false)
    {
        var tab = new TabItem(); var grid = new Grid(); var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock { Text = name ?? layout.Leaves().First().Profile, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) });
        var state = new TabState { Tab = tab, Root = grid, Layout = layout, Name = name, ActiveId = activeId ?? layout.Leaves().First().Id }; states.Add(state);
        header.Children.Add(Button("×", () => CloseTab(state))); tab.Header = header; tab.Content = grid;
        header.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { tabs.SelectedItem = tab; RenameTab(); e.Handled = true; } };
        try { foreach (var node in layout.Leaves()) { var view = AddPane(state, node, demo || demoMode); if (demo || demoMode) Demo(view); } }
        catch { foreach (var view in state.Views.Values) view.Dispose(); states.Remove(state); throw; }
        Rebuild(state); tabs.Items.Add(tab); tabs.SelectedItem = tab; active = state.Views[state.ActiveId];
    }
    void UpdateTabTitle(TabState state, string text)
    {
        string title = state.Name ?? text;
        ((TextBlock)((StackPanel)state.Tab.Header).Children[0]).Text = title.Length > 40 ? title[..40] + "…" : title;
    }
    void UpdateStatus(TerminalView view) => status.Text = $"{view.Profile}   ·   libghostty-vt   ·   {settings.FontFamily} {settings.FontSize:0}   ·   Ctrl+Shift+P commands";
    void QueueFocus(TabState state, TerminalView desired)
    {
        int request = ++selectionVersion; restoringFocus = true;
        active = desired; state.ActiveId = state.Views.First(p => p.Value == desired).Key;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, (Action)(() =>
        {
            if (request != selectionVersion) return;
            try { if (Current == state && state.Views.ContainsValue(desired)) { active = desired; desired.Focus(); UpdateStatus(desired); } }
            finally { restoringFocus = false; }
        }));
    }
    void SyncFractions(TabState state)
    {
        void Read(PaneLayout node)
        {
            if (node.Kind == "pane") return;
            if (state.Splits.TryGetValue(node.Id, out var split))
            {
                double a = node.Kind == "horizontal" ? split.ColumnDefinitions[0].ActualWidth : split.RowDefinitions[0].ActualHeight;
                double b = node.Kind == "horizontal" ? split.ColumnDefinitions[2].ActualWidth : split.RowDefinitions[2].ActualHeight;
                if (a + b > 0) node.Fraction = a / (a + b);
            }
            Read(node.First!); Read(node.Second!);
        }
        Read(state.Layout);
    }
    void Rebuild(TabState state)
    {
        foreach (var view in state.Views.Values) if (view.Parent is Border border && border.Parent is Grid parent) parent.Children.Remove(border);
        state.Root.Children.Clear(); state.Splits.Clear();
        UIElement Build(PaneLayout node)
        {
            if (node.Kind == "pane") return (Border)state.Views[node.Id].Parent;
            var split = new Grid(); state.Splits.Add(node.Id, split);
            var a = Build(node.First!); var b = Build(node.Second!);
            var bar = new GridSplitter { Background = Brushes.DarkSlateGray, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            double fraction = node.Fraction!.Value;
            if (node.Kind == "horizontal")
            {
                split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(fraction, GridUnitType.Star) }); split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) }); split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - fraction, GridUnitType.Star) });
                Grid.SetRow(a,0); Grid.SetColumn(a,0); Grid.SetColumn(bar,1); Grid.SetRow(b,0); Grid.SetColumn(b,2);
            }
            else
            {
                split.RowDefinitions.Add(new RowDefinition { Height = new GridLength(fraction, GridUnitType.Star) }); split.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) }); split.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - fraction, GridUnitType.Star) });
                Grid.SetColumn(a,0); Grid.SetRow(a,0); Grid.SetRow(bar,1); Grid.SetColumn(b,0); Grid.SetRow(b,2);
            }
            split.Children.Add(a); split.Children.Add(bar); split.Children.Add(b); return split;
        }
        var root = Build(state.Layout); Grid.SetRow(root,0); Grid.SetColumn(root,0); state.Root.Children.Add(root);
    }
    internal Workspace CaptureWorkspace()
    {
        var snapshots = new List<WorkspaceTab>();
        foreach (var state in states)
        {
            SyncFractions(state);
            foreach (var node in state.Layout.Leaves()) node.Directory = state.Views[node.Id].DirectoryForNewSession;
            snapshots.Add(new WorkspaceTab { Root = state.Layout.Copy(), Name = state.Name, ActivePaneId = state.ActiveId });
        }
        var bounds = WindowState == WindowState.Normal ? new Rect(0,0,Width,Height) : RestoreBounds;
        var result = new Workspace { Tabs = snapshots.ToArray(), SelectedTab = Math.Max(0,tabs.SelectedIndex), Width = Math.Max(MinWidth,bounds.Width), Height = Math.Max(MinHeight,bounds.Height), Maximized = WindowState == WindowState.Maximized };
        result.Validate(); return result;
    }
    internal TerminalView? Active => active;
    static void Demo(TerminalView v)
    {
        v.Engine.Feed("\x1b[1;36mGhostty Windows\x1b[0m\r\n\x1b[90mNative Windows frontend · real libghostty-vt engine\x1b[0m\r\n\r\n" +
        "\x1b[32m✓\x1b[0m  ConPTY sessions: PowerShell, Command Prompt, WSL\r\n\x1b[32m✓\x1b[0m  Tabs, split panes, scrollback and search\r\n\x1b[32m✓\x1b[0m  Unicode: café Ελληνικά 日本語 🙂\r\n\r\n" +
        "\x1b[1mTrue color palette\x1b[0m\r\n" + string.Concat(Enumerable.Range(0, 40).Select(i => $"\x1b[48;2;{30 + i * 4};{90 + i * 2};{220 - i * 3}m ")) + "\x1b[0m\r\n\r\nInline Kitty PNG image\r\n");
        var bitmap = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/ghostty.png"));
        var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(bitmap);
        using var buffer = new MemoryStream(); png.Save(buffer);
        v.Engine.Feed("\x1b_Ga=T,f=100,i=1,c=12,r=6,q=2;" + Convert.ToBase64String(buffer.ToArray()) + "\x1b\\");
        v.Engine.Feed("\r\nPS C:\\Users\\menta> ");
    }
    void ShowProfiles()
    {
        var menu = new ContextMenu(); foreach (string name in Settings.Profiles.Keys) { var item = new MenuItem { Header = name }; item.Click += (_, _) => NewTab(name); menu.Items.Add(item); }
        menu.Items.Add(new Separator()); var folder = new MenuItem { Header = "New tab in folder…" }; folder.Click += (_, _) => NewTabInFolder(); menu.Items.Add(folder); menu.IsOpen = true;
    }
    internal void Split(bool horizontal)
    {
        var state = Current; if (active == null || state == null) return;
        if (states.Sum(s => s.Views.Count) >= Workspace.MaxPanes) { status.Text = "Workspace capacity reached (64 panes)."; return; }
        SyncFractions(state);
        var original = state.Layout.Leaves().First(p => state.Views[p.Id] == active);
        if (state.Layout.DepthOf(original.Id) >= Workspace.MaxSplitDepth) { status.Text = "Split nesting limit reached (24 levels)."; return; }
        original.Directory = active.DirectoryForNewSession;
        var nextNode = PaneLayout.Pane(active.Profile, original.Directory);
        var next = AddPane(state, nextNode, demoMode);
        if (demoMode) Demo(next);
        state.Layout = state.Layout.Replace(original.Id, PaneLayout.Split(horizontal, original, nextNode));
        restoringFocus = true; Rebuild(state); QueueFocus(state,next);
    }
    internal void ExerciseLayout()
    {
        Split(true); Split(false);
        if (Current?.Panes.Count != 3) throw new Exception("Nested split construction failed");
        ClosePane(); if (Current?.Panes.Count != 2) throw new Exception("Split promotion on close failed");
        NewTab("Command Prompt", true); if (states.Count != 2) throw new Exception("New tab failed");
        var second = Current!; CloseTab(second); if (states.Count != 1) throw new Exception("Close tab failed");
        active = Current!.Panes[0]; active.Focus();
    }
    internal void ClosePane()
    {
        var state = Current; if (active == null || state == null) return;
        if (state.Panes.Count == 1) { CloseTab(state); return; }
        if (settings.ConfirmClose && Dialogs.Show(this, "Close this pane and its running processes?", "Close pane", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        SyncFractions(state);
        string id = state.Views.First(p => p.Value == active).Key;
        var removed = active; state.Layout = state.Layout.Remove(id)!; state.Views.Remove(id);
        restoringFocus = true; Rebuild(state); removed.Dispose(); QueueFocus(state,state.Panes[0]);
    }
    void CloseTab(TabState state)
    {
        if (settings.ConfirmClose && Dialogs.Show(this, "Close this tab and its running processes?", "Close tab", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        foreach (var pane in state.Panes) pane.Dispose(); states.Remove(state); tabs.Items.Remove(state.Tab);
        if (states.Count == 0) { closing = true; Close(); }
    }
    void ShowFind() { searchBar.Visibility = Visibility.Visible; search.Focus(); search.SelectAll(); }
    void NewTabInFolder()
    {
        var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Launch terminal in folder", InitialDirectory = settings.WorkingDirectory };
        if (picker.ShowDialog(this) == true) NewTab(settings.DefaultProfile, directory: picker.FolderName);
    }
    void SaveWorkspace()
    {
        settings.Workspace = CaptureWorkspace(); settings.Save(); status.Text = "Workspace saved.";
    }
    void RenameTab()
    {
        var state = Current; if (state == null) return;
        var dialog = new Window { Title = "Rename tab", Owner = this, Width = 400, Height = 160, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        SystemTitleBar.Attach(dialog);
        var panel = new StackPanel { Margin = new Thickness(16) }; var name = new TextBox { Text = state.Name ?? "", Margin = new Thickness(0,0,0,12), ToolTip = "Leave blank to follow the shell title" };
        void Save() { state.Name = string.IsNullOrWhiteSpace(name.Text) ? null : name.Text.Trim(); UpdateTabTitle(state, active?.Engine.Title is string text && text.Length > 0 ? text : active?.Profile ?? settings.DefaultProfile); dialog.Close(); }
        panel.Children.Add(name); panel.Children.Add(Button("Save", Save)); dialog.Content = panel;
        name.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Save(); e.Handled = true; } };
        dialog.Loaded += (_, _) => { name.Focus(); name.SelectAll(); }; dialog.ShowDialog();
    }
    void Find(bool next) { if (active != null) matchCount.Text = active.Find(search.Text, next) + " matches"; }
    void HandleShortcut(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.OemComma) { SettingsDialog(); e.Handled = true; return; }
        var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && actualKey == Key.Tab)
        {
            if (tabs.Items.Count > 0) tabs.SelectedIndex = (tabs.SelectedIndex + tabs.Items.Count - 1) % tabs.Items.Count;
            e.Handled = true; return;
        }
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt) && actualKey is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            var state = Current;
            if (state != null && active != null) { int delta = actualKey is Key.Left or Key.Up ? -1 : 1; active = state.Panes[(state.Panes.IndexOf(active) + delta + state.Panes.Count) % state.Panes.Count]; active.Focus(); }
            e.Handled = true; return;
        }
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            switch (e.Key) {
                case Key.T: NewTab(settings.DefaultProfile); break;
                case Key.W: ClosePane(); break;
                case Key.D: Split(true); break;
                case Key.E: Split(false); break;
                case Key.F: ShowFind(); break;
                case Key.P: Palette(); break;
                case Key.N: new MainWindow(settings).Show(); break;
                case Key.O: NewTabInFolder(); break;
                case Key.R: RenameTab(); break;
                case Key.OemPlus: active?.Zoom(1); break;
                case Key.OemMinus: active?.Zoom(-1); break;
                default: return;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.F11) { if (WindowStyle == WindowStyle.None) { WindowStyle = WindowStyle.SingleBorderWindow; WindowState = WindowState.Normal; } else { WindowStyle = WindowStyle.None; WindowState = WindowState.Maximized; } e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Tab) { if (tabs.Items.Count > 0) tabs.SelectedIndex = (tabs.SelectedIndex + 1) % tabs.Items.Count; e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && actualKey >= Key.D1 && actualKey <= Key.D9) { int i = actualKey - Key.D1; if (i < tabs.Items.Count) tabs.SelectedIndex = i; e.Handled = true; }
        else if (e.Key == Key.Escape && searchBar.Visibility == Visibility.Visible) { searchBar.Visibility = Visibility.Collapsed; active?.Find(""); active?.Focus(); e.Handled = true; }
    }
    void Palette()
    {
        var actions = new Dictionary<string, Action> { ["New tab — PowerShell"] = () => NewTab("PowerShell"), ["New tab — Command Prompt"] = () => NewTab("Command Prompt"), ["New tab — WSL"] = () => NewTab("WSL"), ["New tab in folder…"] = NewTabInFolder, ["Rename tab…"] = RenameTab, ["Save workspace now"] = SaveWorkspace, ["Split right"] = () => Split(true), ["Split down"] = () => Split(false), ["Close pane"] = ClosePane, ["Find in terminal"] = ShowFind, ["Copy selection"] = () => active?.CopySelection(), ["Paste"] = () => active?.Paste(), ["Increase font size"] = () => active?.Zoom(1), ["Decrease font size"] = () => active?.Zoom(-1), ["Export scrollback…"] = Export, ["Settings"] = SettingsDialog };
        var dialog = new Window { Title = "Commands", Owner = this, Width = 440, Height = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        SystemTitleBar.Attach(dialog);
        var panel = new DockPanel { Margin = new Thickness(12) }; var query = new TextBox { Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(query, Dock.Top); panel.Children.Add(query);
        var list = new ListBox(); panel.Children.Add(list); dialog.Content = panel;
        void Filter() { list.ItemsSource = actions.Keys.Where(k => k.Contains(query.Text, StringComparison.OrdinalIgnoreCase)).ToArray(); list.SelectedIndex = 0; }
        void Run() { if (list.SelectedItem is string key) { dialog.Close(); actions[key](); } }
        query.TextChanged += (_, _) => Filter(); query.KeyDown += (_, e) => { if (e.Key == Key.Enter) Run(); if (e.Key == Key.Down) list.Focus(); };
        list.MouseDoubleClick += (_, _) => Run(); list.KeyDown += (_, e) => { if (e.Key == Key.Enter) Run(); };
        Filter(); dialog.Loaded += (_, _) => query.Focus(); dialog.ShowDialog();
    }
    void Export()
    {
        if (active == null) return; var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Text file|*.txt", FileName = "terminal.txt" };
        if (dialog.ShowDialog(this) == true) File.WriteAllText(dialog.FileName, active.Engine.Text());
    }
    void SettingsDialog()
    {
        if (settingsWindow != null) { settingsWindow.Activate(); return; }
        settingsWindow = new SettingsWindow(settings, draft =>
        {
            draft.Workspace = CaptureWorkspace(); settings.Commit(draft);
            foreach (var window in Application.Current.Windows.OfType<MainWindow>().Where(w => ReferenceEquals(w.settings, settings)))
            {
                window.status.Visibility = settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
                foreach (var view in window.states.SelectMany(s => s.Panes)) view.RefreshSettings();
            }
        }) { Owner = this, Icon = Icon };
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
    }
}
