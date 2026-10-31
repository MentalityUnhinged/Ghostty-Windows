using System.Windows.Controls;

namespace GhosttyWindows;

// Bracket WPF's built-in content-focus moves before they can overwrite pane history.
sealed class WorkspaceTabs : TabControl
{
    public Action? SelectionStarting;
    public Action? SelectionFinished;
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        SelectionStarting?.Invoke();
        try { base.OnSelectionChanged(e); }
        finally { SelectionFinished?.Invoke(); }
    }
}
