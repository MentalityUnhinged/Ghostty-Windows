using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GhosttyWindows;

static class SecondBatchTests
{
    static void Check(bool value,string name){if(!value)throw new Exception("Second batch failed: "+name);SelfTest.Passed.Add(name);}
    static void CommitText(TerminalView view,string text)=>view.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,new TextComposition(InputManager.Current,view,text)){RoutedEvent=TextCompositionManager.TextInputEvent});
    static IEnumerable<TextBlock> Labels(DependencyObject root)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is TextBlock label)yield return label;foreach(var nested in Labels(child))yield return nested;}
    }
    public static async Task Run(string artifacts)
    {
        using(var engine=new Engine())
        {
            engine.Resize(24,4,9,20);for(int i=0;i<20;i++)engine.Feed($"row {i}\r\n");
            engine.Navigate(Engine.NavigationTarget.Row,3);Check(engine.ScreenText().StartsWith("row 3"),"Absolute row navigation uses native scrollback coordinates");
            engine.Select(0,0,4,0);engine.ScrollEdge(true);engine.Navigate(Engine.NavigationTarget.SelectionStart);
            Check(engine.ScreenText().StartsWith("row 3"),"Scroll-to-selection follows the terminal-owned tracked anchor");
            engine.Reset(true);engine.Feed("alpha beta");engine.Select(0,0,4,0);engine.AdjustSelection(Engine.SelectionAdjustment.Right);engine.AdjustSelection(Engine.SelectionAdjustment.Right);
            Check(engine.Text(true)=="alpha b","Keyboard selection adjustment extends the logical end of a native selection");
            engine.Select(-1,0,0,0);engine.AdjustSelection(Engine.SelectionAdjustment.Left);Check(engine.HasSelection&&engine.Text(true)=="a","Keyboard selection can start from the terminal caret");
            engine.Select(-1,0,0,0);engine.Gesture(0,1,0,10,1,time:1_000_000_000);engine.Gesture(1,1,0,10,1);
            engine.Gesture(0,1,0,10,1,time:1_100_000_000);engine.Gesture(1,1,0,10,1);engine.ClearSelection(true);engine.Gesture(0,1,0,10,1,time:1_200_000_000);
            Check(engine.Text(true)=="alpha beta","Clearing an automatic copy preserves triple-click line selection");
            engine.Reset(true);engine.Feed("\x1b[31mred<&>\x1b[0m");string html=engine.StyledText();
            Check(html.Contains("style=")&&html.Contains("red&lt;&amp;&gt;")&&html.Contains("color"),"Native HTML export preserves styles and escapes terminal text");
            File.WriteAllText(Path.Combine(artifacts,"styled-export.html"),html);
            engine.Reset(true);for(int i=0;i<4;i++){engine.Feed($"\x1b]133;A\u0007prompt{i}>\x1b]133;B\u0007cmd\r\n");for(int j=0;j<6;j++)engine.Feed($"output {i}/{j}\r\n");engine.Feed("\x1b]133;D;0\u0007");}
            engine.Navigate(Engine.NavigationTarget.Prompt,delta:-1);string first=engine.ScreenText();engine.Navigate(Engine.NavigationTarget.Prompt,delta:-1);string previous=engine.ScreenText();
            Check(first.StartsWith("prompt3>")&&previous.StartsWith("prompt2>"),"Native prompt navigation moves backward through OSC 133 shell markers");
            engine.Navigate(Engine.NavigationTarget.Prompt,delta:1);Check(engine.ScreenText().StartsWith("prompt3>"),"Native prompt navigation moves forward through shell markers");
            engine.Limits(0,0);Check(!engine.Text().Contains("prompt0>"),"Zero scrollback storage removes retained history");
            engine.Feed("\x1b_Ga=T,f=32,s=1,v=1,i=77,c=1,r=1,q=2;/wAA/w==\x1b\\");Check(engine.Capture().Images.Length==0,"Zero image storage disables image placement");
            engine.Limits(null,null);engine.Feed("\x1b_Ga=T,f=32,s=1,v=1,i=78,c=1,r=1,q=2;/wAA/w==\x1b\\");Check(engine.Capture().Images.Length>0,"Unlimited image storage can re-enable graphics after disabling it");
        }
        string path=Path.Combine(artifacts,"batch-two-settings.json"),export=Path.Combine(artifacts,"exported-preferences.json"),import=Path.Combine(artifacts,"imported-preferences.json");
        const string original="{\"FontSize\":16,\"ShowWelcome\":false,\"ConfirmClose\":false,\"ScrollbackLimitBytes\":null,\"ImageLimitBytes\":0,\"VendorA\":{\"keep\":true}}";
        File.WriteAllText(path,original);var settings=Settings.LoadFile(path);settings.ExportPreferences(export);
        var roundtrip=Settings.LoadFile(export);Check(roundtrip.ScrollbackLimitBytes==null&&roundtrip.ImageLimitBytes==0&&roundtrip.Workspace==null,"Preference export preserves explicit unlimited/disabled limits and excludes workspace state");
        Check(settings.StoragePath==Path.GetFullPath(path),"Preference export cannot replace the live settings destination");
        File.WriteAllText(path,"{\"FontSize\":19,\"WheelScrollLines\":7,\"ShowWelcome\":false,\"ConfirmClose\":false,\"VendorA\":{\"keep\":true}}");settings.Reload();
        Check(settings.FontSize==19&&settings.WheelScrollLines==7,"Reload uses the settings owner's current on-disk configuration");
        File.WriteAllText(path,"{\"WheelScrollLines\":-1}");bool rejected=false;try{settings.Reload();}catch(InvalidDataException){rejected=true;}
        Check(rejected&&settings.FontSize==19&&settings.WheelScrollLines==7,"Invalid reload leaves live preferences unchanged");
        File.WriteAllText(path,original);settings.Reload();var workspace=new Workspace{Tabs=new[]{new WorkspaceTab{Root=PaneLayout.Pane("Command Prompt",artifacts)}}};
        File.WriteAllText(import,"{\"FontSize\":20,\"HideMouseWhileTyping\":true,\"RightClickAction\":\"none\",\"MiddleClickAction\":\"copy\",\"VendorB\":42}");settings.ImportPreferences(import,workspace);
        Check(settings.FontSize==20&&settings.HideMouseWhileTyping&&settings.RightClickAction=="none"&&ReferenceEquals(settings.Workspace,workspace),"Preference import applies supported options while keeping the supplied live workspace");
        Check(settings.Extra!.ContainsKey("VendorA")&&settings.Extra.ContainsKey("VendorB")&&File.Exists(path+".backup")&&settings.StoragePath==Path.GetFullPath(path),"Import preserves unknown values, backs up the prior file and retains the destination owner");
        settings.ResetPreferences(workspace);Check(settings.FontSize==15&&settings.WheelScrollLines==2&&settings.ImageLimitBytes==64UL*1024*1024&&ReferenceEquals(settings.Workspace,workspace)&&settings.Extra!.ContainsKey("VendorB"),"Reset restores known defaults while preserving workspace and unknown options");
        settings.ConfirmClose=false;settings.ShowWelcome=false;var main=new MainWindow(settings,true);main.Show();await Task.Delay(60);
        try
        {
            var originalView=main.Active!;main.Split(true);await Task.Delay(40);main.Split(false);await Task.Delay(40);
            var before=main.CaptureWorkspace().Tabs[0].Root;double root=before.Fraction!.Value,nested=before.Second!.Fraction!.Value;
            main.ExecuteCommand("resize-split-left");await Task.Delay(40);main.ExecuteCommand("resize-split-down");await Task.Delay(40);var after=main.CaptureWorkspace().Tabs[0].Root;
            Check(after.Fraction<root&&after.Second!.Fraction>nested,"Keyboard split resizing moves the nearest dividers on both axes");
            main.ExecuteCommand("split-zoom");Check(!main.Commands.Single(c=>c.Id=="resize-split-left").Enabled,"Divider resizing is disabled while a split is zoomed");main.ExecuteCommand("split-zoom");await Task.Delay(40);
            Exception? error=null;
            _=Application.Current.Dispatcher.BeginInvoke((Action)(()=>{var dialog=Application.Current.Windows.OfType<Window>().Single(w=>w.Title=="Scroll to row");try{var panel=(StackPanel)dialog.Content;panel.Children.OfType<TextBox>().Single().Text="0";panel.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}catch(Exception failure){error=failure;dialog.Close();}}));
            main.ExecuteCommand("scroll-row");if(error!=null)throw error;Check(main.Active!=null,"Scroll-to-row command runs through its validated input dialog");
            var editor=new SettingsWindow(settings,settings.Commit){Owner=main};editor.Show();await Task.Delay(30);
            editor.WheelLines.Text="2";editor.ScrollbackBytes.Text="";editor.ImageBytes.Text="0";editor.RightClick.SelectedValue="none";editor.MiddleClick.SelectedValue="copy";editor.HideMouse.IsChecked=true;editor.ClearOnTyping.IsChecked=false;editor.ClearOnCopy.IsChecked=true;
            ((ScrollViewer)((Grid)editor.Content).Children[0]).ScrollToEnd();editor.UpdateLayout();ChromeTests.Capture(editor,Path.Combine(artifacts,"advanced-settings.png"));
            var display=(ContentPresenter)editor.RightClick.Template.FindName("Selection",editor.RightClick);
            Check(Labels(display).Any(label=>label.Text=="Do nothing"),"Structured mouse-action choices display labels rather than internal records");
            editor.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!editor.IsVisible&&settings.WheelScrollLines==2&&settings.ScrollbackLimitBytes==null&&settings.ImageLimitBytes==0&&settings.ClearSelectionOnCopy&&settings.MiddleClickAction=="copy","Settings editor commits all new preference controls through the shared owner");
            var view=main.Active!;view.Engine.Reset(true);view.Engine.Feed("selected text");view.Engine.Select(0,0,7,0);CommitText(view,"x");
            Check(view.Engine.HasSelection&&view.Cursor==Cursors.None,"Typing can preserve selection and hide the pointer");
            view.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseMoveEvent});Check(view.Cursor!=Cursors.None,"Pointer movement restores the hidden mouse cursor");
            settings.ClearSelectionOnTyping=true;CommitText(view,"y");Check(!view.Engine.HasSelection,"Typing clears selection when the configured policy enables it");
            view.Engine.Reset(true);for(int i=0;i<80;i++)view.Engine.Feed($"wheel {i}\r\n");view.Engine.ScrollEdge(false);string top=view.Engine.ScreenText();settings.WheelScrollLines=1;
            view.ScrollWheel(-30);Check(view.Engine.ScreenText()==top,"Sub-notch wheel motion does not accidentally jump to the bottom");view.ScrollWheel(-30);view.ScrollWheel(-30);view.ScrollWheel(-30);
            Check(view.Engine.ScreenText()!=top,"Precision wheel input accumulates into a configurable line scroll");settings.WheelScrollLines=0;string stopped=view.Engine.ScreenText();view.ScrollWheel(-120);Check(view.Engine.ScreenText()==stopped,"Zero wheel speed disables local wheel scrolling");
            view.Engine.Select(0,0,3,0);view.RunMouseAction("none",new Point(15,15));Check(view.Engine.HasSelection,"The configurable no-action mouse policy leaves terminal selection unchanged");
            var other=new MainWindow(settings,true);other.Show();await Task.Delay(40);
            try{other.WindowState=WindowState.Maximized;await Task.Delay(30);other.WindowState=WindowState.Minimized;await Task.Delay(30);Check(ReferenceEquals(main.CycleWindow(1),other),"Window cycling targets another terminal window");await Task.Delay(40);Check(other.WindowState==WindowState.Maximized,"Window cycling restores a minimized window's previous maximized state");}
            finally{other.Close();}
        }
        finally{main.Close();}
        // Import/reload/reset must not reconstruct live terminal processes.
        settings.DefaultProfile="Command Prompt";settings.RestoreTabs=false;settings.ShowWelcome=false;settings.ConfirmClose=false;var live=new MainWindow(settings);live.Show();var child=live.Active!;
        try
        {
            for(int i=0;i<100&&child.Console.ProcessId==0;i++)await Task.Delay(30);uint pid=child.Console.ProcessId;
            settings.ImportPreferences(import,live.CaptureWorkspace());settings.Reload();settings.ResetPreferences(live.CaptureWorkspace());
            child.Console.Send("set /a 936*37\r");for(int i=0;i<100&&!child.Engine.Text().Contains("34632");i++)await Task.Delay(30);
            Check(pid>0&&child.Console.ProcessId==pid&&child.Engine.Text().Contains("34632"),"Import, reload and defaults reset preserve a real running ConPTY session");
        }
        finally{settings.ConfirmClose=false;live.Close();await child.Console.Cleanup.WaitAsync(TimeSpan.FromSeconds(10));}
    }
}
