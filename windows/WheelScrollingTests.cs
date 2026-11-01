using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GhosttyWindows;

static class WheelScrollingTests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); SelfTest.Passed.Add(message); }
    static void Wheel(UIElement target, int delta) => target.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = UIElement.PreviewMouseWheelEvent });
    public static async Task Run()
    {
        var content = new StackPanel(); for (int i = 0; i < 100; i++) content.Children.Add(new TextBlock { Text = "Row " + i, Height = 24 });
        var scroll = new ScrollViewer { Content = content, CanContentScroll = false };
        var window = new Window { Content = scroll, Width = 400, Height = 300, ShowInTaskbar = false };
        try
        {
            window.Show(); scroll.ScrollToVerticalOffset(120); window.UpdateLayout(); await Task.Delay(30);
            Wheel((UIElement)content.Children[10], 120); window.UpdateLayout();
            Check(Math.Abs(scroll.VerticalOffset - 96) < .1, "UI wheel notch scrolls a controlled 24 DIPs");
            Wheel((UIElement)content.Children[10], 15); window.UpdateLayout();
            Check(Math.Abs(scroll.VerticalOffset - 93) < .1, "Small touchpad deltas remain proportional instead of becoming full notches");
            Wheel((UIElement)content.Children[10], -120); window.UpdateLayout();
            Check(Math.Abs(scroll.VerticalOffset - 117) < .1, "UI wheel direction reversal has no extra acceleration");
            Check(new Settings().WheelScrollLines == 2, "New terminal settings use two lines per wheel notch");
        }
        finally { window.Close(); }
    }
}
