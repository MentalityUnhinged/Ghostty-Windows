using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GhosttyWindows;

static class Dialogs
{
    public static MessageBoxResult Show(string text, string title, MessageBoxButton buttons = MessageBoxButton.OK) => Show(Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive), text, title, buttons);
    public static MessageBoxResult Show(Window? owner, string text, string title, MessageBoxButton buttons = MessageBoxButton.OK)
    {
        var dialog = new NoticeWindow(text, title, buttons);
        if (owner?.IsVisible == true) dialog.Owner = owner;
        dialog.ShowDialog(); return dialog.Result;
    }
}

sealed class NoticeWindow : Window
{
    internal readonly Button Accept = new() { Content = "OK", MinWidth = 82, Margin = new Thickness(6) };
    internal readonly Button Cancel = new() { Content = "Cancel", MinWidth = 82, Margin = new Thickness(6), IsCancel = true };
    internal MessageBoxResult Result { get; private set; }
    public NoticeWindow(string text, string title, MessageBoxButton buttons)
    {
        Title = title; Width = 490; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "Panel"); SetResourceReference(ForegroundProperty, "Text");
        SystemTitleBar.Attach(this);
        Result = buttons == MessageBoxButton.OKCancel ? MessageBoxResult.Cancel : MessageBoxResult.OK;
        var panel = new StackPanel { Margin = new Thickness(20) }; Content = panel;
        panel.Children.Add(new ScrollViewer { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) }, MaxHeight = Math.Max(100, Math.Min(400, SystemParameters.WorkArea.Height - 150)), VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; panel.Children.Add(footer);
        footer.Children.Add(Accept); if (buttons == MessageBoxButton.OKCancel) footer.Children.Add(Cancel);
        Accept.IsDefault = buttons == MessageBoxButton.OK; Cancel.IsDefault = buttons == MessageBoxButton.OKCancel;
        Accept.Click += (_, _) => { Result = MessageBoxResult.OK; Close(); }; Cancel.Click += (_, _) => Close();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }
}
