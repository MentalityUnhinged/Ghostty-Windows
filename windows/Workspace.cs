using System.IO;

namespace GhosttyWindows;

// Canonical layout owner for UI construction, split edits and persistence.
sealed class PaneLayout
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "pane";
    public string? Profile { get; set; }
    public string? Directory { get; set; }
    public double? Fraction { get; set; }
    public PaneLayout? First { get; set; }
    public PaneLayout? Second { get; set; }
    public static PaneLayout Pane(string profile, string directory) => new() { Profile = profile, Directory = directory };
    public static PaneLayout Split(bool horizontal, PaneLayout first, PaneLayout second) => new() { Kind = horizontal ? "horizontal" : "vertical", Fraction = .5, First = first, Second = second };
    public IEnumerable<PaneLayout> Leaves() => Kind == "pane" ? new[] { this } : First!.Leaves().Concat(Second!.Leaves());
    public PaneLayout Copy() => new() { Id = Id, Kind = Kind, Profile = Profile, Directory = Directory, Fraction = Fraction, First = First?.Copy(), Second = Second?.Copy() };
    public int DepthOf(string id)
    {
        if (Id == id) return 0;
        if (Kind == "pane") return -1;
        int child = Math.Max(First!.DepthOf(id), Second!.DepthOf(id));
        return child >= 0 ? child + 1 : -1;
    }
    public PaneLayout Replace(string id, PaneLayout replacement)
    {
        if (Id == id) return replacement;
        if (First != null) First = First.Replace(id, replacement);
        if (Second != null) Second = Second.Replace(id, replacement);
        return this;
    }
    public PaneLayout? Remove(string id)
    {
        if (Id == id) return null;
        if (Kind == "pane") return this;
        First = First!.Remove(id); Second = Second!.Remove(id);
        if (First == null) return Second;
        if (Second == null) return First;
        return this;
    }
}

sealed class WorkspaceTab
{
    public PaneLayout Root { get; set; } = null!;
    public string? ActivePaneId { get; set; }
    public string? Name { get; set; }
}

sealed class Workspace
{
    public const int MaxTabs = 20, MaxPanes = 64, MaxSplitDepth = 24;
    public int Version { get; set; } = 1;
    public WorkspaceTab[] Tabs { get; set; } = Array.Empty<WorkspaceTab>();
    public int SelectedTab { get; set; }
    public double Width { get; set; } = 1160;
    public double Height { get; set; } = 760;
    public bool Maximized { get; set; }
    internal Workspace Copy() { Validate(); return new Workspace { Version = Version, Tabs = Tabs.Select(t => new WorkspaceTab { Root = t.Root.Copy(), ActivePaneId = t.ActivePaneId, Name = t.Name }).ToArray(), SelectedTab = SelectedTab, Width = Width, Height = Height, Maximized = Maximized }; }
    public void Validate()
    {
        if (Version != 1) throw new InvalidDataException("Unsupported workspace version: " + Version);
        if (Tabs == null || Tabs.Length > MaxTabs) throw new InvalidDataException("Workspace supports at most 20 tabs");
        if (!double.IsFinite(Width) || !double.IsFinite(Height) || Width < 540 || Height < 320) throw new InvalidDataException("Invalid workspace window dimensions");
        if (SelectedTab < 0 || (Tabs.Length > 0 && SelectedTab >= Tabs.Length)) throw new InvalidDataException("Invalid selected workspace tab");
        var ids = new HashSet<string>(); var nodes = new HashSet<PaneLayout>(); int panes = 0;
        void Visit(PaneLayout? node, int depth)
        {
            if (node == null || depth > MaxSplitDepth || !nodes.Add(node)) throw new InvalidDataException("Invalid, cyclic or excessively nested workspace tree");
            if (string.IsNullOrWhiteSpace(node.Id) || !ids.Add(node.Id)) throw new InvalidDataException("Workspace pane identities must be unique");
            if (node.Kind == "pane")
            {
                if (node.First != null || node.Second != null || node.Fraction != null || node.Profile == null || !Settings.Profiles.ContainsKey(node.Profile) || string.IsNullOrWhiteSpace(node.Directory) || !Path.IsPathFullyQualified(node.Directory))
                    throw new InvalidDataException("Invalid workspace shell profile or launch directory");
                if (++panes > MaxPanes) throw new InvalidDataException("Workspace supports at most 64 panes");
            }
            else
            {
                if (node.Kind is not ("horizontal" or "vertical") || node.Profile != null || node.Directory != null || node.Fraction is not double f || !double.IsFinite(f) || f < 0 || f > 1)
                    throw new InvalidDataException("Invalid workspace split orientation or fraction");
                Visit(node.First, depth + 1); Visit(node.Second, depth + 1);
            }
        }
        foreach (var tab in Tabs)
        {
            if (tab == null) throw new InvalidDataException("Invalid workspace tab");
            Visit(tab.Root, 0);
            if (tab.ActivePaneId != null && !tab.Root.Leaves().Any(p => p.Id == tab.ActivePaneId)) throw new InvalidDataException("Workspace active pane is not in its tab");
        }
    }
}
