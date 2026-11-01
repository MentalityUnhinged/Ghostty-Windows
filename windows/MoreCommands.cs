using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GhosttyWindows;

sealed partial class MainWindow
{
    WindowState lastVisibleState;
    void AddSecondBatch(List<WindowCommand> commands)
    {
        void Add(string id,string label,Action action,Func<bool>? enabled=null,KeyChord? gesture=null) => commands.Add(new(id,label,action,enabled??(()=>active!=null),gesture));
        var resizeKeys=new[] { ("left",Key.H), ("down",Key.J), ("up",Key.K), ("right",Key.L) };
        foreach(var (direction,key) in resizeKeys)Add("resize-split-"+direction,"Move split divider "+direction,()=>ResizeSplit(direction),()=>FindDivider(direction)!=null&&Current?.ZoomedId==null,new(key,ModifierKeys.Control|ModifierKeys.Alt|ModifierKeys.Shift));
        Add("next-window","Next terminal window",()=>CycleWindow(1),()=>OpenWindows().Count>1,new(Key.PageDown,ModifierKeys.Control|ModifierKeys.Alt));
        Add("previous-window","Previous terminal window",()=>CycleWindow(-1),()=>OpenWindows().Count>1,new(Key.PageUp,ModifierKeys.Control|ModifierKeys.Alt));
        Add("scroll-selection","Scroll to selection",()=>active!.Engine.Navigate(Engine.NavigationTarget.SelectionStart),()=>active?.Engine.HasSelection==true);
        Add("scroll-row","Scroll to row…",ScrollRowDialog);
        Add("previous-prompt","Previous shell prompt",()=>active!.Engine.Navigate(Engine.NavigationTarget.Prompt,delta:-1));
        Add("next-prompt","Next shell prompt",()=>active!.Engine.Navigate(Engine.NavigationTarget.Prompt,delta:1));
        foreach(var (key,adjustment) in new[] { (Key.Left,Engine.SelectionAdjustment.Left),(Key.Right,Engine.SelectionAdjustment.Right),(Key.Up,Engine.SelectionAdjustment.Up),(Key.Down,Engine.SelectionAdjustment.Down) })
            Add("extend-selection-"+key.ToString().ToLowerInvariant(),"Extend selection "+key.ToString().ToLowerInvariant(),()=>{active!.Engine.AdjustSelection(adjustment);active.Engine.Navigate(Engine.NavigationTarget.SelectionEnd);},gesture:new(key,ModifierKeys.Control|ModifierKeys.Alt|ModifierKeys.Shift));
        Add("export-html","Export styled scrollback as HTML…",()=>ExportRegion("html"));
        Add("open-config","Open settings JSON",()=>PreferenceOperation(()=>{if(!File.Exists(settings.StoragePath))settings.Save();System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", "\""+settings.StoragePath+"\""){UseShellExecute=true});}),()=>true);
        Add("reload-settings","Reload settings from disk",()=>PreferenceOperation(settings.Reload),()=>File.Exists(settings.StoragePath));
        Add("reset-settings","Reset preferences to defaults…",()=>{if(Dialogs.Show(this,"Reset preferences to defaults? Running sessions and the current workspace are kept. The previous settings file is backed up.","Reset preferences",MessageBoxButton.OKCancel)==MessageBoxResult.OK)PreferenceOperation(()=>settings.ResetPreferences(CaptureWorkspace()));},()=>true);
        Add("export-settings","Export preferences…",ExportPreferences,()=>true);
        Add("import-settings","Import preferences (keep sessions)…",ImportPreferences,()=>true);
    }
    void PreferenceOperation(Action action)
    {
        try{action();}
        catch(Exception error)when(error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or System.ComponentModel.Win32Exception){Dialogs.Show(this,error.Message,"Preference operation failed");}
    }
    void ExportPreferences()
    {
        var dialog=new Microsoft.Win32.SaveFileDialog { Filter="JSON preferences|*.json",FileName="ghostty-preferences.json" };
        if(dialog.ShowDialog(this)==true)PreferenceOperation(()=>settings.ExportPreferences(dialog.FileName));
    }
    void ImportPreferences()
    {
        var dialog=new Microsoft.Win32.OpenFileDialog { Filter="JSON preferences|*.json" };
        if(dialog.ShowDialog(this)==true)PreferenceOperation(()=>settings.ImportPreferences(dialog.FileName,CaptureWorkspace()));
    }
    void ScrollRowDialog()
    {
        var dialog=new Window { Title="Scroll to row",Owner=this,Width=380,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner };SystemTitleBar.Attach(dialog);
        var panel=new StackPanel {Margin=new Thickness(20)};dialog.Content=panel;panel.Children.Add(new TextBlock {Text="Row offset (0 is the oldest retained row)"});
        var input=new TextBox {Text="0",Margin=new Thickness(0,8,0,8)};panel.Children.Add(input);var feedback=new TextBlock {TextWrapping=TextWrapping.Wrap};panel.Children.Add(feedback);
        var go=new Button {Content="Go",IsDefault=true,Margin=new Thickness(0,8,0,0)};panel.Children.Add(go);
        go.Click+=(_,_)=>{if(!ulong.TryParse(input.Text,out var row)){feedback.Text="Enter a non-negative whole row number.";return;}active?.Engine.Navigate(Engine.NavigationTarget.Row,row);dialog.Close();};
        dialog.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape)dialog.Close();};dialog.Loaded+=(_,_)=>{input.Focus();input.SelectAll();};dialog.ShowDialog();
    }
    PaneLayout? FindDivider(string direction)
    {
        var state=Current;if(state?.ActiveId==null)return null;string orientation=direction is "left" or "right"?"horizontal":"vertical";
        PaneLayout? Find(PaneLayout node)
        {
            if(node.Kind=="pane")return null;var next=node.First!.DepthOf(state.ActiveId)>=0?node.First:node.Second!;
            return Find(next)??(node.Kind==orientation?node:null);
        }
        return Find(state.Layout);
    }
    internal bool ResizeSplit(string direction)
    {
        if(direction is not("left" or "right" or "up" or "down"))throw new ArgumentException("Unknown resize direction");
        var state=Current;if(state==null||active==null||state.ZoomedId!=null)return false;var divider=FindDivider(direction);if(divider==null)return false;
        SyncFractions(state);divider.Fraction=Math.Clamp(divider.Fraction!.Value+(direction is "left" or "up"?-.05:.05),.05,.95);
        var desired=active;restoringFocus=true;Rebuild(state);QueueFocus(state,desired);return true;
    }
    List<MainWindow> OpenWindows()=>Application.Current.Windows.OfType<MainWindow>().Where(w=>w.IsVisible).ToList();
    internal MainWindow? CycleWindow(int delta)
    {
        var windows=OpenWindows();if(windows.Count<2)return null;int from=windows.IndexOf(this),index=(from+(delta<0?-1:1)+windows.Count)%windows.Count;var target=windows[index];
        if(target.WindowState==WindowState.Minimized)target.WindowState=target.lastVisibleState;target.Activate();return target;
    }
}
