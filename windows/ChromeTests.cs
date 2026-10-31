using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GhosttyWindows;

static class ChromeTests
{
    static void Check(bool condition, string name) { if (!condition) throw new Exception("Chrome check failed: " + name); SelfTest.Passed.Add(name); }
    static bool Dark(Brush brush) => brush is SolidColorBrush b && b.Color.R < 100 && b.Color.G < 100 && b.Color.B < 130;
    static void Capture(FrameworkElement element, string path)
    {
        element.UpdateLayout(); var image = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32); image.Render(element);
        var pixels = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(pixels, image.PixelWidth * 4, 0);
        int light = 0; for (int i = 0; i < pixels.Length; i += 4) if (pixels[i] > 210 && pixels[i + 1] > 210 && pixels[i + 2] > 210) light++;
        Check(light < image.PixelWidth * image.PixelHeight / 8, "Rendered " + Path.GetFileNameWithoutExtension(path) + " has no broad light control surfaces");
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(path); png.Save(file);
    }
    public static async Task Run(string artifacts)
    {
        var settings = new Settings(); var editor = new SettingsWindow(settings, _ => { }); editor.Show(); await Task.Delay(80);
        try
        {
            Check(Dark(editor.Size.Background) && Dark(editor.Theme.Background) && Dark(editor.SaveButton.Background), "Settings fields, dropdowns and action buttons share dark chrome");
            editor.Font.ApplyTemplate(); var input = (TextBox)editor.Font.Template.FindName("PART_EditableTextBox", editor.Font);
            input.Text = "Consolas"; Check(editor.Font.Text == "Consolas", "Dark editable font dropdown preserves text input behavior");
            Capture(editor, Path.Combine(artifacts, "dark-settings.png"));
            editor.Height = 320; Capture(editor, Path.Combine(artifacts, "dark-settings-small.png")); editor.Height = 720; editor.UpdateLayout();
            editor.Theme.IsDropDownOpen = true; await Task.Delay(80);
            var popup = (Popup)editor.Theme.Template.FindName("PART_Popup", editor.Theme);
            Check(popup.IsOpen && popup.Child is Border border && Dark(border.Background), "Opened theme popup uses the shared dark surface");
            Capture((FrameworkElement)popup.Child, Path.Combine(artifacts, "dark-dropdown.png"));
            editor.Theme.SelectedItem = "Nord"; editor.Theme.IsDropDownOpen = false;
            Check((string)editor.Theme.SelectedItem == "Nord", "Dark dropdown retains selectable theme values");
            var menu = new ContextMenu { PlacementTarget = editor.Size }; menu.Items.Add(new MenuItem { Header = "Copy" }); menu.Items.Add(new MenuItem { Header = "Unavailable", IsEnabled = false }); menu.Items.Add(new Separator()); menu.Items.Add(new MenuItem { Header = "Paste" });
            menu.IsOpen = true; await Task.Delay(60); Check(Dark(menu.Background), "Context menus use a dark popup background");
            Capture(menu, Path.Combine(artifacts, "dark-menu.png")); menu.IsOpen = false;
        }
        finally { editor.Close(); }
        var notice = new NoticeWindow("Close the terminal and its running processes?", "Dark confirmation", MessageBoxButton.OKCancel); notice.Show(); await Task.Delay(60);
        try
        {
            Capture(notice, Path.Combine(artifacts, "dark-confirmation.png"));
            Check(notice.Result == MessageBoxResult.Cancel && notice.Cancel.IsDefault, "Themed confirmation defaults to Cancel");
            notice.Accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(notice.Result == MessageBoxResult.OK && !notice.IsVisible, "Themed confirmation accepts explicit OK");
        }
        finally { notice.Close(); }
        var host = new Window { Width = 300, Height = 180, Content = new ListBox { ItemsSource = new[] { "Commands", "Rename tab", "Settings" }, SelectedIndex = 0 } }; host.Show(); await Task.Delay(40);
        try { Check(Dark(host.Background) && Dark(((ListBox)host.Content).Background), "Command and rename window defaults use dark surfaces"); Capture(host, Path.Combine(artifacts, "dark-list.png")); }
        finally { host.Close(); }
    }
}
