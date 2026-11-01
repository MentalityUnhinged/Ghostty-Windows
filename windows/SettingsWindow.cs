using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GhosttyWindows;

sealed partial class SettingsWindow : Window
{
    readonly Settings source;
    readonly Action<Settings> commit;
    internal readonly ComboBox Font = new() { IsEditable = true, IsTextSearchEnabled = true };
    internal readonly TextBox Size = new();
    internal readonly ComboBox BackgroundEffect = new() { ItemsSource = new[] { "Solid", "Acrylic" } };
    internal readonly Slider BackgroundOpacity = new() { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true, SmallChange = 1, LargeChange = 10 };
    internal readonly ComboBox Theme = new() { ItemsSource = new[] { "Midnight", "Dracula", "Nord", "Paper" } };
    internal readonly ComboBox Profile = new() { ItemsSource = Settings.Profiles.Keys };
    internal readonly TextBox Folder = new();
    internal readonly CheckBox Restore = new() { Content = "Restore tabs and split layouts on next launch" };
    internal readonly CheckBox ConfirmClose = new() { Content = "Confirm before closing running sessions" };
    internal readonly CheckBox CopyOnSelect = new() { Content = "Copy selected text to the clipboard on mouse release" };
    internal readonly CheckBox ConfirmPaste = new() { Content = "Confirm before pasting multiple lines" };
    internal readonly CheckBox ShowToolbar = new() { Content = "Show the top toolbar (Ctrl+Shift+P opens commands when hidden)" };
    internal readonly CheckBox ShowStatus = new() { Content = "Show the terminal status bar" };
    internal readonly CheckBox AnimateWelcome = new() { Content = "Animate welcome ghost before starting new shells (any key skips)" };
    internal readonly CheckBox ShowWelcome = new() { Content = "Show gradient Ghostty welcome art in new sessions" };
    internal readonly CheckBox FocusFollowsMouse = new() { Content = "Focus panes on hover within the active window" };
    internal readonly CheckBox HideMouse = new() { Content = "Hide the pointer while typing" };
    internal readonly CheckBox ClearOnCopy = new() { Content = "Clear selection after a successful copy" };
    internal readonly CheckBox ClearOnTyping = new() { Content = "Clear selection when typing or pasting" };
    internal readonly TextBox WheelLines = new();
    internal readonly TextBox ScrollbackBytes = new();
    internal readonly TextBox ImageBytes = new();
    internal readonly ComboBox RightClick = new() { ItemsSource=Settings.MouseActions, DisplayMemberPath="Label",SelectedValuePath="Id" };
    internal readonly ComboBox MiddleClick = new() { ItemsSource=Settings.MouseActions, DisplayMemberPath="Label",SelectedValuePath="Id" };
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
        BackgroundEffect.SelectedItem = settings.BackgroundEffect; BackgroundOpacity.Value = settings.BackgroundOpacity;
        Field("Background effect", BackgroundEffect);
        var opacityLabel = new TextBlock();
        void OpacityLabel() => opacityLabel.Text = $"Background opacity: {BackgroundOpacity.Value:0}% (Acrylic only)";
        BackgroundOpacity.ValueChanged += (_, _) => OpacityLabel(); OpacityLabel();
        fields.Children.Add(opacityLabel); fields.Children.Add(BackgroundOpacity);
        fields.Children.Add(new TextBlock { Text = "Acrylic uses Windows blur with adjustable background tint opacity. Text stays opaque. Requires Windows 11 22H2+ and Transparency effects; otherwise the background is solid. Windows controls blur intensity.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), Foreground = Brushes.LightSteelBlue });
        var folderRow = new DockPanel(); var browse = new Button { Content = "Browse…", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(browse, Dock.Right); folderRow.Children.Add(browse); folderRow.Children.Add(Folder); Field("Startup folder for new sessions", folderRow);
        browse.Click += (_, _) => { var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Choose startup folder" }; if (Directory.Exists(Folder.Text)) picker.InitialDirectory = Folder.Text; if (picker.ShowDialog(this) == true) Folder.Text = picker.FolderName; };
        Restore.IsChecked = settings.RestoreTabs; ConfirmClose.IsChecked = settings.ConfirmClose; CopyOnSelect.IsChecked = settings.CopyOnSelect; ConfirmPaste.IsChecked = settings.ConfirmMultilinePaste; ShowStatus.IsChecked = settings.ShowStatusBar;
        ShowToolbar.IsChecked = settings.ShowToolbar;
        ShowWelcome.IsChecked = settings.ShowWelcome;
        AnimateWelcome.IsChecked = settings.AnimateWelcome;
        var previewWelcome = new Button { Content = "Preview animated ASCII ghost...", Padding = new Thickness(10, 6, 10, 6), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) };
        previewWelcome.Click += (_, _) => WelcomeAnimation.Show(this, source);
        fields.Children.Add(previewWelcome);
        FocusFollowsMouse.IsChecked = settings.FocusFollowsMouse;
        HideMouse.IsChecked=settings.HideMouseWhileTyping;ClearOnCopy.IsChecked=settings.ClearSelectionOnCopy;ClearOnTyping.IsChecked=settings.ClearSelectionOnTyping;
        foreach (var option in new[] { Restore, ConfirmClose, CopyOnSelect, ConfirmPaste, ShowToolbar, ShowStatus, ShowWelcome, AnimateWelcome, FocusFollowsMouse,HideMouse,ClearOnCopy,ClearOnTyping }) { option.Foreground = Brushes.Gainsboro; option.Margin = new Thickness(0, 14, 0, 0); fields.Children.Add(option); }
        WheelLines.Text=settings.WheelScrollLines.ToString();ScrollbackBytes.Text=settings.ScrollbackLimitBytes?.ToString()??"";ImageBytes.Text=settings.ImageLimitBytes?.ToString()??"";
        RightClick.SelectedValue=settings.RightClickAction;MiddleClick.SelectedValue=settings.MiddleClickAction;
        Field("Wheel lines per notch (0–100)",WheelLines);Field("Local right-click action",RightClick);Field("Local middle-click action",MiddleClick);
        Field("Scrollback bytes (blank: unlimited; 0: disabled)",ScrollbackBytes);Field("Image storage bytes (blank: unlimited; 0: disabled)",ImageBytes);
        fields.Children.Add(new TextBlock { Text = "Appearance applies to open panes. Shell and folder choices apply to new sessions; splitting inherits the active pane's folder.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 18, 0, 8), Foreground = Brushes.LightSteelBlue });
        AddAppearanceFields(fields);
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
        if(!int.TryParse(WheelLines.Text,out int wheel)||wheel is < 0 or > 100){Feedback.Text="Enter wheel lines from 0 to 100.";return;}
        bool Limit(string text,out ulong? value){value=null;if(string.IsNullOrWhiteSpace(text))return true;if(!ulong.TryParse(text,out var bytes))return false;value=bytes;return true;}
        if(!Limit(ScrollbackBytes.Text,out var history)||!Limit(ImageBytes.Text,out var images)){Feedback.Text="Storage limits must be non-negative whole byte counts, or blank for unlimited.";return;}
        var draft = source.Edit(); draft.FontFamily = Font.Text.Trim(); draft.FontSize = size; draft.Theme = (string?)Theme.SelectedItem ?? "Midnight";
        draft.BackgroundEffect = (string?)BackgroundEffect.SelectedItem ?? "Solid"; draft.BackgroundOpacity = BackgroundOpacity.Value;
        draft.DefaultProfile = (string?)Profile.SelectedItem ?? "PowerShell"; draft.WorkingDirectory = Folder.Text;
        draft.RestoreTabs = Restore.IsChecked == true; draft.ConfirmClose = ConfirmClose.IsChecked == true; draft.CopyOnSelect = CopyOnSelect.IsChecked == true;
        draft.ConfirmMultilinePaste = ConfirmPaste.IsChecked == true; draft.ShowStatusBar = ShowStatus.IsChecked == true;
        draft.ShowToolbar = ShowToolbar.IsChecked == true;
        draft.ShowWelcome = ShowWelcome.IsChecked == true;
        draft.AnimateWelcome = AnimateWelcome.IsChecked == true;
        draft.FocusFollowsMouse = FocusFollowsMouse.IsChecked == true;
        draft.HideMouseWhileTyping=HideMouse.IsChecked==true;draft.ClearSelectionOnCopy=ClearOnCopy.IsChecked==true;draft.ClearSelectionOnTyping=ClearOnTyping.IsChecked==true;
        draft.WheelScrollLines=wheel;draft.ScrollbackLimitBytes=history;draft.ImageLimitBytes=images;draft.RightClickAction=(string?)RightClick.SelectedValue??"context-menu";draft.MiddleClickAction=(string?)MiddleClick.SelectedValue??"paste";
        if (!ReadAppearance(draft)) return;
        try { commit(draft); Feedback.Foreground = Brushes.LightGreen; Feedback.Text = "Settings saved."; if (close) Close(); }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
        { Feedback.Text = "Settings could not be saved. Your changes have not been applied.\n" + failure.Message; }
    }
}
