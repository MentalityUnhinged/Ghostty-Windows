using System.Windows.Input;

namespace GhosttyWindows;

sealed partial class MainWindow
{
    void AddThirdBatch(List<WindowCommand> commands)
    {
        foreach (int direction in new[] { -1, 1 })
        {
            int sign = direction; string label = sign < 0 ? "up" : "down";
            commands.Add(new("scroll-line-" + label, "Scroll " + label + " by configured lines", () => active!.Engine.Scroll(sign * settings.ScrollCommandLines), () => active != null));
            commands.Add(new("scroll-fraction-" + label, "Scroll " + label + " by configured page fraction", () => active!.Engine.Scroll(sign * Math.Max(1, (int)Math.Round(active.Engine.Rows * settings.ScrollPageFraction))), () => active != null));
        }
        var ctrlAlt = ModifierKeys.Control | ModifierKeys.Alt;
        foreach (var (id, label, action, key, mods) in new[] {
            ("selection-line-start", "Extend selection to line start", Engine.SelectionAdjustment.BeginningOfLine, Key.Home, ctrlAlt | ModifierKeys.Shift),
            ("selection-line-end", "Extend selection to line end", Engine.SelectionAdjustment.EndOfLine, Key.End, ctrlAlt | ModifierKeys.Shift),
            ("selection-screen-start", "Extend selection to screen start", Engine.SelectionAdjustment.Home, Key.Home, ctrlAlt),
            ("selection-screen-end", "Extend selection to last nonblank screen row", Engine.SelectionAdjustment.End, Key.End, ctrlAlt),
            ("selection-page-up", "Extend selection one page up", Engine.SelectionAdjustment.PageUp, Key.PageUp, ctrlAlt | ModifierKeys.Shift),
            ("selection-page-down", "Extend selection one page down", Engine.SelectionAdjustment.PageDown, Key.PageDown, ctrlAlt | ModifierKeys.Shift) })
            commands.Add(new(id, label, () => { active!.Engine.AdjustSelection(action); active.Engine.Navigate(Engine.NavigationTarget.SelectionEnd); }, () => active != null, new KeyChord(key, mods)));
    }
}
