using System.Windows.Controls;
using System.Windows.Markup;

namespace GhosttyWindows;

// Bracket WPF's built-in content-focus moves before they can overwrite pane history.
sealed class WorkspaceTabs : TabControl
{
    public WorkspaceTabs()
    {
        Template = (ControlTemplate)XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="TabControl">
          <DockPanel KeyboardNavigation.TabNavigation="Local" LastChildFill="True">
            <TabPanel Name="Headers" DockPanel.Dock="{Binding TabStripPlacement, RelativeSource={RelativeSource TemplatedParent}}" IsItemsHost="True" KeyboardNavigation.TabIndex="1"/>
            <ContentPresenter Name="PART_SelectedContentHost" ContentSource="SelectedContent" KeyboardNavigation.TabIndex="2"/>
          </DockPanel>
        </ControlTemplate>
        """);
    }
    internal string HeaderVisibility { get; set; } = "Automatic";
    internal void UpdateHeaders()
    {
        if (Template.FindName("Headers", this) is System.Windows.FrameworkElement headers)
            headers.Visibility = (HeaderVisibility == "Always" || HeaderVisibility == "Automatic" && Items.Count >= 2) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }
    public override void OnApplyTemplate() { base.OnApplyTemplate(); UpdateHeaders(); }
    protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e); UpdateHeaders();
    }
    public Action? SelectionStarting;
    public Action? SelectionFinished;
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        SelectionStarting?.Invoke();
        try { base.OnSelectionChanged(e); }
        finally { SelectionFinished?.Invoke(); }
    }
}
