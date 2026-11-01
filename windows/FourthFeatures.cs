using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GhosttyWindows;

sealed partial class Settings
{
    public bool CopyTrimTrailing { get; set; } = true;
    public string CopyNewlines { get; set; } = "Windows";
    public bool OpenExportsInEditor { get; set; }
    public string TabVisibility { get; set; } = "Automatic";
    public string TabPlacement { get; set; } = "Top";
    public int TabTitleLength { get; set; } = 40;
    public bool ShowTabCloseButtons { get; set; } = true;
    public bool MiddleClickClosesTab { get; set; }
    public string BoldItalicFontFamily { get; set; } = "";
    void ValidateFourthPreferences()
    {
        if (CopyNewlines is not ("Windows" or "LF") || TabVisibility is not ("Automatic" or "Always" or "Never") || TabPlacement is not ("Top" or "Bottom") || TabTitleLength is < 8 or > 200)
            throw new ArgumentException("Invalid clipboard or tab preference.");
        if (BoldItalicFontFamily == null || BoldItalicFontFamily.Length > 0 && !Fonts.SystemFontFamilies.Any(f => f.Source.Equals(BoldItalicFontFamily, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Choose an installed bold-italic font family, or leave blank.");
    }
}

static class ClipboardContent
{
    internal static string Plain(string text, Settings settings)
    {
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (settings.CopyTrimTrailing) lines = lines.Select(s => s.TrimEnd(' ', '\t')).ToArray();
        return string.Join(settings.CopyNewlines == "LF" ? "\n" : "\r\n", lines);
    }
    internal static string Html(string fragment)
    {
        const string header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        const string start = "<html><body><!--StartFragment-->", end = "<!--EndFragment--></body></html>";
        int html = Encoding.UTF8.GetByteCount(string.Format(CultureInfo.InvariantCulture, header, 0, 0, 0, 0));
        int fragmentStart = html + Encoding.UTF8.GetByteCount(start), fragmentEnd = fragmentStart + Encoding.UTF8.GetByteCount(fragment);
        return string.Format(CultureInfo.InvariantCulture, header, html, fragmentEnd + Encoding.UTF8.GetByteCount(end), fragmentStart, fragmentEnd) + start + fragment + end;
    }
}

sealed partial class MainWindow
{
    internal string TabDisplayTitle(string title) { var elements = new StringInfo(title); return elements.LengthInTextElements > settings.TabTitleLength ? elements.SubstringByTextElements(0, settings.TabTitleLength) + "…" : title; }
    readonly List<WorkspaceTab> closedTabs = new();
    void ApplyTabPreferences()
    {
        tabs.HeaderVisibility = settings.TabVisibility;
        tabs.TabStripPlacement = settings.TabPlacement == "Bottom" ? Dock.Bottom : Dock.Top;
        tabs.UpdateHeaders();
        foreach (var state in states)
        {
            var header = (StackPanel)state.Tab.Header;
            ((Button)header.Children[1]).Visibility = settings.ShowTabCloseButtons ? Visibility.Visible : Visibility.Collapsed;
            var view = state.Views[state.ActiveId!]; UpdateTabTitle(state, view.Engine.Title.Length > 0 ? view.Engine.Title : view.Profile);
        }
    }
    internal WorkspaceTab TabSnapshot(TabState state)
    {
        SyncFractions(state);
        foreach (var node in state.Layout.Leaves()) node.Directory = state.Views[node.Id].DirectoryForNewSession;
        return new() { Root = state.Layout.Copy(), Name = state.Name, ActivePaneId = state.ActiveId };
    }
    void RememberClosedTab(TabState state)
    {
        closedTabs.Add(TabSnapshot(state)); if (closedTabs.Count > 10) closedTabs.RemoveAt(0);
    }
    bool CanOpenTab(WorkspaceTab snapshot) => states.Count < settings.MaxOpenTabs && states.Sum(s => s.Views.Count) + snapshot.Root.Leaves().Count() <= settings.MaxOpenPanes;
    internal void OpenFreshTab(WorkspaceTab snapshot)
    {
        if (!CanOpenTab(snapshot)) { Dialogs.Show(this, "Workspace capacity reached. Close a tab or pane first.", "Could not open tab"); return; }
        var layout = snapshot.Root.Copy(); string? activeId = null;
        void Identify(PaneLayout node)
        {
            string oldId = node.Id; node.Id = Guid.NewGuid().ToString("N"); if (oldId == snapshot.ActivePaneId) activeId = node.Id;
            if (node.First != null) Identify(node.First); if (node.Second != null) Identify(node.Second);
        }
        Identify(layout); CreateTab(layout, snapshot.Name, activeId, demoMode); ApplyTabPreferences();
    }
    internal void ReopenTab()
    {
        if (closedTabs.Count == 0 || !CanOpenTab(closedTabs[^1])) return;
        OpenFreshTab(closedTabs[^1]); closedTabs.RemoveAt(closedTabs.Count - 1);
    }
    void TabOverview()
    {
        var dialog = new Window { Title = "Tab overview", Owner = this, Width = 560, Height = 400, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        SystemTitleBar.Attach(dialog);
        var list = new ListBox { Margin = new Thickness(12) };
        foreach (var state in states) list.Items.Add(new ListBoxItem { Content = $"{states.IndexOf(state) + 1}. {state.Name ?? (state.Panes[0].Engine.Title.Length > 0 ? state.Panes[0].Engine.Title : state.Panes[0].Profile)}  ({state.Views.Count} panes)", Tag = state });
        list.SelectedIndex = tabs.SelectedIndex;
        void Activate() { if (list.SelectedItem is ListBoxItem { Tag: TabState state }) { tabs.SelectedItem = state.Tab; dialog.Close(); } }
        list.MouseDoubleClick += (_, _) => Activate(); list.KeyDown += (_, e) => { if (e.Key == Key.Enter) Activate(); if (e.Key == Key.Escape) dialog.Close(); };
        dialog.Content = list; dialog.Loaded += (_, _) => list.Focus(); dialog.ShowDialog();
    }
    void CopyFormattedSelection()
    {
        if (active == null) return;
        try
        {
            var snapshot = active.Engine.SelectionClipboard(settings.CopyTrimTrailing);
            var data = new DataObject(); data.SetText(ClipboardContent.Plain(snapshot.Plain, settings));
            data.SetData(DataFormats.Html, ClipboardContent.Html(snapshot.Html));
            Clipboard.SetDataObject(data, true);
            if (settings.ClearSelectionOnCopy) active.ClearLocalSelection();
        }
        catch (COMException e) { Dialogs.Show(this, e.Message, "Could not copy formatted selection"); }
    }
    internal string RichExport(int region, int format) => active?.Engine.FormatText(region, format) ?? "";
    void ExportRich(int region, int format)
    {
        var picker = new Microsoft.Win32.SaveFileDialog { Filter = format == 2 ? "HTML file|*.html" : "ANSI text file|*.ansi", FileName = format == 2 ? "terminal.html" : "terminal.ansi" };
        if (picker.ShowDialog(this) != true) return;
        try { File.WriteAllText(picker.FileName, RichExport(region, format)); OpenExport(picker.FileName); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { Dialogs.Show(this, e.Message, "Could not export terminal"); }
    }
    void OpenExport(string path)
    {
        if (settings.OpenExportsInEditor) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }
    void AddFourthBatch(List<WindowCommand> commands)
    {
        void Add(string id, string label, Action run, Func<bool>? enabled = null, KeyChord? gesture = null) => commands.Add(new(id, label, run, enabled ?? (() => active != null), gesture));
        Add("export-selection-html", "Export selection as HTML…", () => ExportRich(1, 2), () => active?.Engine.HasSelection == true);
        Add("export-screen-html", "Export visible screen as HTML…", () => ExportRich(2, 2));
        foreach (var (id, region) in new[] { ("scrollback", 0), ("selection", 1), ("screen", 2) })
            Add("export-" + id + "-ansi", "Export " + id + " as ANSI…", () => ExportRich(region, 1), () => active != null && (region != 1 || active.Engine.HasSelection));
        Add("copy-formatted", "Copy selection with HTML formatting", CopyFormattedSelection, () => active?.Engine.HasSelection == true);
        Add("copy-screen", "Copy visible screen", () => TerminalView.CopyText(ClipboardContent.Plain(active!.Engine.FormatText(2, 0, settings.CopyTrimTrailing), settings)));
        Add("copy-scrollback", "Copy all scrollback", () => TerminalView.CopyText(ClipboardContent.Plain(active!.Engine.FormatText(0, 0, settings.CopyTrimTrailing), settings)));
        Add("paste-single-line", "Paste as a single line", () => active!.PasteSingleLine(), () => active != null && !active.ReadOnly);
        Add("tab-overview", "Tab overview…", TabOverview, () => states.Count > 0);
        Add("duplicate-tab", "Duplicate tab layout (new shells)", () => OpenFreshTab(TabSnapshot(Current!)), () => Current != null && states.Count < settings.MaxOpenTabs && states.Sum(s => s.Views.Count) + Current.Views.Count <= settings.MaxOpenPanes);
        Add("reopen-tab", "Reopen closed tab (new shells)", ReopenTab, () => closedTabs.Count > 0 && CanOpenTab(closedTabs[^1]), new(Key.T, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt));
    }
}
