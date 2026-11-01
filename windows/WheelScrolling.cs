using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace GhosttyWindows;

// WPF's default logical scrolling can turn tiny touchpad deltas into several
// complete rows. Normalize once per event, at the nearest scrollable owner.
static class WheelScrolling
{
    sealed class Remainder { public double Rows; }
    static readonly ConditionalWeakTable<ScrollViewer, Remainder> fractions = new();
    static bool registered;
    internal static void Register()
    {
        if (registered) return; registered = true;
        EventManager.RegisterClassHandler(typeof(ScrollViewer), UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(Scroll));
    }
    static DependencyObject? Parent(DependencyObject item) => item is Visual or Visual3D ? VisualTreeHelper.GetParent(item) : LogicalTreeHelper.GetParent(item);
    static void Scroll(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0 || Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        ScrollViewer? owner = null;
        for (var item = e.OriginalSource as DependencyObject; item != null; item = Parent(item))
            if (item is ScrollViewer candidate && candidate.ScrollableHeight > 0) { owner = candidate; break; }
        if (owner == null || !ReferenceEquals(owner, sender)) return;
        double amount = -(double)e.Delta / Mouse.MouseWheelDeltaForOneLine;
        if (owner.CanContentScroll)
        {
            var state = fractions.GetOrCreateValue(owner); state.Rows += amount;
            int rows = (int)state.Rows; state.Rows -= rows;
            if (rows != 0) owner.ScrollToVerticalOffset(owner.VerticalOffset + rows);
        }
        else owner.ScrollToVerticalOffset(owner.VerticalOffset + amount * 24);
        e.Handled = true;
    }
}
