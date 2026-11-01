using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GhosttyWindows;

static class SettingsTests
{
    static void Check(bool value, string name) { if (!value) throw new Exception("Settings test failed: " + name); SelfTest.Passed.Add(name); }
    static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child is T result) yield return result;
            foreach (var result2 in Descendants<T>(child)) yield return result2;
        }
    }
    static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    public static async Task Run(string artifacts)
    {
        string folder = Path.Combine(artifacts, "settings-startup-folder"); Directory.CreateDirectory(folder);
        string path = Path.Combine(artifacts, "settings-editor.json");
        File.WriteAllText(path, "{\"FontSize\":15,\"VendorOptions\":{\"Keep\":42}}");
        var settings = Settings.LoadFile(path); var main = new MainWindow(settings, true); main.Show(); await Task.Delay(100);
        try
        {
            var tabControl = Descendants<WorkspaceTabs>(main).Single();
            var headers = (System.Windows.FrameworkElement)tabControl.Template.FindName("Headers", tabControl);
            Check(headers.Visibility == Visibility.Collapsed, "Single tab hides the header strip");
            main.NewTab("PowerShell", true); main.UpdateLayout();
            Check(headers.Visibility == Visibility.Visible && headers.ActualHeight <= 24, $"Multiple tabs show a compact readable header strip ({headers.Visibility}, {headers.ActualHeight})");
            settings.ConfirmClose = false; main.ClosePane(); settings.ConfirmClose = true; main.UpdateLayout();
            Check(headers.Visibility == Visibility.Collapsed, "Closing to one tab reclaims the header space");
            var toolbar = Descendants<Button>(main).Single(b => b.Content as string == "Settings"); Click(toolbar); await Task.Delay(80);
            var editor = Application.Current.Windows.OfType<SettingsWindow>().Single();
            Check(editor.IsVisible && editor.Owner == main, "Real Settings toolbar button opens an owned settings editor");
            Click(toolbar); Check(Application.Current.Windows.OfType<SettingsWindow>().Count() == 1, "Repeated Settings clicks reuse the open editor");
            editor.Width = 480; editor.Height = 320; editor.UpdateLayout();
            var bottom = editor.SaveButton.TransformToAncestor(editor).Transform(new Point(editor.SaveButton.ActualWidth, editor.SaveButton.ActualHeight));
            Check(bottom.Y < editor.ActualHeight && bottom.X < editor.ActualWidth && editor.SaveButton.IsVisible && Descendants<ScrollViewer>(editor).Any(s => s.ScrollableHeight > 0), "Small settings window scrolls fields while Save remains inside the window");
            string before = File.ReadAllText(path); editor.Size.Text = "NaN"; Click(editor.ApplyButton);
            Check(editor.IsVisible && editor.Feedback.Text.Contains("finite") && settings.FontSize == 15 && File.ReadAllText(path) == before, "Non-finite font input reports an inline error without changing live or saved settings");
            editor.Size.Text = "18"; editor.Folder.Text = "relative-folder"; Click(editor.ApplyButton);
            Check(editor.Feedback.Text.Contains("absolute") && settings.FontSize == 15, "Invalid startup folder cannot partially apply appearance");
            editor.Folder.Text = folder; editor.Theme.SelectedItem = "Paper"; editor.Profile.SelectedItem = "Command Prompt";
            editor.ShowStatus.IsChecked = false; editor.CopyOnSelect.IsChecked = true; editor.ConfirmPaste.IsChecked = false;
            editor.BackgroundEffect.SelectedItem = "Acrylic"; editor.BackgroundOpacity.Value = 37;
            editor.ShowToolbar.IsChecked = false;
            editor.ShowWelcome.IsChecked = false;
            editor.ConfirmClose.IsChecked = false; Click(editor.ApplyButton); await Task.Delay(60);
            Check(settings.BackgroundEffect == "Acrylic" && settings.BackgroundOpacity == 37 && Settings.LoadFile(path).BackgroundOpacity == 37, "Background effect and slider persist through Settings Apply");
            Check(main.Opacity == 1 && main.Active!.Opacity == 1, "Backdrop changes preserve foreground opacity");
            Check(editor.IsVisible && editor.Feedback.Text == "Settings saved." && settings.FontSize == 18 && settings.Theme == "Paper", "Apply persists valid settings and keeps the editor open");
            Check(main.Active!.Engine.Capture().Cells[0].Bg == settings.Colors.Bg, "Settings Apply changes the existing terminal engine theme");
            Check(((DockPanel)main.Content).Children.OfType<TextBlock>().Single(t => DockPanel.GetDock(t) == Dock.Bottom).Visibility == Visibility.Collapsed, "Status-bar setting applies immediately to the existing window");
            Check(!toolbar.IsVisible && !Settings.LoadFile(path).ShowToolbar, "Toolbar visibility applies immediately and survives reload");
            var loaded = Settings.LoadFile(path);
            Check(loaded.CopyOnSelect && !loaded.ConfirmMultilinePaste && !loaded.ConfirmClose && !loaded.ShowStatusBar && !loaded.ShowWelcome && loaded.WorkingDirectory == folder && loaded.DefaultProfile == "Command Prompt", "New behavior and launch settings survive file reload");
            Check(File.ReadAllText(path + ".backup") == before && loaded.Extra!["VendorOptions"].GetProperty("Keep").GetInt32() == 42, "Settings editor keeps the prior file backup and unknown settings");
            main.ExecuteCommand("show-toolbar");
            Check(toolbar.IsVisible && Settings.LoadFile(path).ShowToolbar, "Palette toolbar toggle restores hidden controls and persists the preference");
            var opaque = settings.Edit(); opaque.BackgroundEffect = "Solid"; settings.Commit(opaque);
            Check(!main.BackdropActive && main.Background != Brushes.Transparent, "Switching Acrylic off restores an opaque client background without restarting panes");
            editor.Size.Text = "22"; Click(editor.CancelButton);
            Check(settings.FontSize == 18 && Settings.LoadFile(path).FontSize == 18, "Cancel discards edits made after Apply");
            Click(toolbar); editor = Application.Current.Windows.OfType<SettingsWindow>().Single(); editor.Size.Text = "19"; Click(editor.SaveButton);
            Check(!editor.IsVisible && settings.FontSize == 19 && Settings.LoadFile(path).FontSize == 19, "Save commits settings and closes the editor");
            main.NewTab(settings.DefaultProfile, true); Check(main.Active!.Profile == "Command Prompt" && main.Active.LaunchDirectory == folder, "New tabs consume the saved default shell and startup folder");
            Click(toolbar); editor = Application.Current.Windows.OfType<SettingsWindow>().Single(); editor.UpdateLayout();
            var image = new RenderTargetBitmap((int)editor.ActualWidth, (int)editor.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(editor);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using (var file = File.Create(Path.Combine(artifacts, "settings-preview.png"))) png.Save(file);
            editor.Close();
        }
        finally { main.Close(); }

        string blocked = Path.Combine(artifacts, "settings-write-blocked"); Directory.CreateDirectory(blocked);
        var failedSettings = Settings.LoadFile(blocked); var failed = new SettingsWindow(failedSettings, failedSettings.Commit); failed.Show();
        try
        {
            failed.Size.Text = "25"; Click(failed.SaveButton);
            Check(failed.IsVisible && failed.Feedback.Text.Contains("could not be saved") && failedSettings.FontSize == 15, "Real write failure leaves the editor open and live settings unchanged");
            Check(!Directory.EnumerateFiles(artifacts, "settings-write-blocked.tmp-*").Any(), "Failed settings save cleans its owned temporary file");
        }
        finally { failed.Close(); }
    }
}
