using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GhosttyWindows;

sealed class SettingsWindow : Window
{
    readonly Settings source;
    readonly Action<Settings> commit;
    internal readonly ComboBox Font = new() { IsEditable = true, IsTextSearchEnabled = true };
    internal readonly TextBox Size = new();
    internal readonly ComboBox Theme = new() { ItemsSource = new[] { "Midnight", "Dracula", "Nord", "Paper" } };
    internal readonly ComboBox Profile = new() { ItemsSource = Settings.Profiles.Keys };
    internal readonly TextBox Folder = new();
    internal readonly CheckBox Restore = new() { Content = "Restore tabs and split layouts on next launch" };
    internal readonly CheckBox ConfirmClose = new() { Content = "Confirm before closing running sessions" };
    internal readonly CheckBox CopyOnSelect = new() { Content = "Copy selected text to the clipboard on mouse release" };
    internal readonly CheckBox ConfirmPaste = new() { Content = "Confirm before pasting multiple lines" };
    internal readonly CheckBox ShowStatus = new() { Content = "Show the terminal status bar" };
    internal readonly TextBlock Feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
    internal readonly Button SaveButton = new() { Content = "Save", IsDefault = true };
    internal readonly Button ApplyButton = new() { Content = "Apply" };
    internal readonly Button CancelButton = new() { Content = "Cancel" };
    public SettingsWindow(Settings settings, Action<Settings> commit)
    {
        source = settings; this.commit = commit;
        Title = "Ghostty Windows Settings"; Width = 620; Height = Math.Min(720, SystemParameters.WorkArea.Height);
        MinWidth = 380; MinHeight = 300; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0x1b, 0x21, 0x2d)); Foreground = Brushes.Gainsboro;
        SystemTitleBar.Attach(this);
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); Content = root;
        var fields = new StackPanel { Margin = new Thickness(22) };
        root.Children.Add(new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        void Field(string label, FrameworkElement control)
        {
            fields.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 10, 0, 5) }); fields.Children.Add(control);
            System.Windows.Automation.AutomationProperties.SetName(control, label);
        }
        Font.ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f).ToArray(); Font.Text = settings.FontFamily;
        Size.Text = settings.FontSize.ToString(CultureInfo.CurrentCulture); Theme.SelectedItem = settings.Theme; Profile.SelectedItem = settings.DefaultProfile; Folder.Text = settings.WorkingDirectory;
        Field("Font family", Font); Field("Font size (8–48)", Size); Field("Terminal theme", Theme); Field("Default shell for new sessions", Profile);
        var folderRow = new DockPanel(); var browse = new Button { Content = "Browse…", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(browse, Dock.Right); folderRow.Children.Add(browse); folderRow.Children.Add(Folder); Field("Startup folder for new sessions", folderRow);
        browse.Click += (_, _) => { var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Choose startup folder" }; if (Directory.Exists(Folder.Text)) picker.InitialDirectory = Folder.Text; if (picker.ShowDialog(this) == true) Folder.Text = picker.FolderName; };
        Restore.IsChecked = settings.RestoreTabs; ConfirmClose.IsChecked = settings.ConfirmClose; CopyOnSelect.IsChecked = settings.CopyOnSelect; ConfirmPaste.IsChecked = settings.ConfirmMultilinePaste; ShowStatus.IsChecked = settings.ShowStatusBar;
        foreach (var option in new[] { Restore, ConfirmClose, CopyOnSelect, ConfirmPaste, ShowStatus }) { option.Foreground = Brushes.Gainsboro; option.Margin = new Thickness(0, 14, 0, 0); fields.Children.Add(option); }
        fields.Children.Add(new TextBlock { Text = "Appearance applies to open panes. Shell and folder choices apply to new sessions; splitting inherits the active pane's folder.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 18, 0, 8), Foreground = Brushes.LightSteelBlue });
        var footer = new StackPanel { Margin = new Thickness(22, 0, 22, 16) }; Grid.SetRow(footer, 1); root.Children.Add(footer); footer.Children.Add(Feedback);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; footer.Children.Add(buttons);
        foreach (var button in new[] { CancelButton, ApplyButton, SaveButton }) { button.MinWidth = 82; button.Padding = new Thickness(12, 6, 12, 6); button.Margin = new Thickness(6, 0, 0, 0); buttons.Children.Add(button); }
        SaveButton.Click += (_, _) => Save(true); ApplyButton.Click += (_, _) => Save(false); CancelButton.Click += (_, _) => Close();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Loaded += (_, _) => Font.Focus();
    }
    void Save(bool close)
    {
        Feedback.Foreground = Brushes.LightSalmon;
        if (!double.TryParse(Size.Text, out var size) || !double.IsFinite(size) || size < 8 || size > 48) { Feedback.Text = "Enter a finite font size between 8 and 48."; Size.Focus(); return; }
        if (string.IsNullOrWhiteSpace(Font.Text)) { Feedback.Text = "Choose or enter a font family."; Font.Focus(); return; }
        if (!Path.IsPathFullyQualified(Folder.Text) || !Directory.Exists(Folder.Text)) { Feedback.Text = "Choose an existing absolute startup folder."; Folder.Focus(); return; }
        var draft = source.Edit(); draft.FontFamily = Font.Text.Trim(); draft.FontSize = size; draft.Theme = (string?)Theme.SelectedItem ?? "Midnight";
        draft.DefaultProfile = (string?)Profile.SelectedItem ?? "PowerShell"; draft.WorkingDirectory = Folder.Text;
        draft.RestoreTabs = Restore.IsChecked == true; draft.ConfirmClose = ConfirmClose.IsChecked == true; draft.CopyOnSelect = CopyOnSelect.IsChecked == true;
        draft.ConfirmMultilinePaste = ConfirmPaste.IsChecked == true; draft.ShowStatusBar = ShowStatus.IsChecked == true;
        try { commit(draft); Feedback.Foreground = Brushes.LightGreen; Feedback.Text = "Settings saved."; if (close) Close(); }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
        { Feedback.Text = "Settings could not be saved. Your changes have not been applied.\n" + failure.Message; }
    }
}
