using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GhosttyWindows;

static class FeatureBatchTests
{
    static void Check(bool condition, string name) { if (!condition) throw new Exception("Feature batch failed: " + name); SelfTest.Passed.Add(name); }
    public static async Task Run(string artifacts)
    {
        using (var engine = new Engine())
        {
            engine.Resize(30,4,9,20); for (int i=0;i<12;i++) engine.Feed($"history {i}\r\n");
            engine.ScrollEdge(false); string top = engine.ScreenText();
            Check(top.Contains("history 0") && !top.Contains("history 11"), "Scroll-top and viewport export expose history without exporting offscreen rows");
            engine.ScrollEdge(true); Check(engine.ScreenText().Contains("history 11") && !engine.ScreenText().Contains("history 0"), "Scroll-bottom returns to the active terminal output");
            engine.Feed("\x1b[?1h"); engine.Reset(false);
            Check(!engine.ScreenText().Contains("history") && engine.Text().Contains("history 0") && System.Text.Encoding.UTF8.GetString(engine.Key(38,0,1))=="\x1bOA", "Clear-screen preserves scrollback and application modes");
            engine.Reset(true); Check(!engine.Text().Contains("history") && System.Text.Encoding.UTF8.GetString(engine.Key(38,0,1))=="\x1b[A", "Full terminal reset clears history and restores default modes");
        }
        string path = Path.Combine(artifacts,"batch-settings.json"); File.WriteAllText(path,"{\"FontSize\":15,\"ShowWelcome\":false,\"ConfirmClose\":false}");
        var settings = Settings.LoadFile(path); var main = new MainWindow(settings,true); main.Show(); await Task.Delay(80);
        try
        {
            Check(main.Commands.Select(c=>c.Id).Distinct().Count()==main.Commands.Count, "Named command catalogue has unique operation identities");
            var original = main.Active!; original.Engine.Reset(true); original.Engine.Feed("batch selection\r\nother output"); original.Engine.Select(0,0,4,0);
            Check(main.ExportContent("selection")=="batch" && main.ExportContent("screen").Contains("other output"), "Selection and visible-screen export use distinct native source ranges");
            main.ExecuteCommand("search-selection"); Check(main.SearchQuery=="batch", "Search-selection captures the exact selected text");
            original.Engine.Feed("\x1b]2;Complete untruncated title\x07"); Check(main.EffectiveTitle()=="Complete untruncated title", "Copy-title source preserves the full terminal title");
            original.Engine.Select(-1,0,0,0); Check(!main.ExecuteCommand("export-selection") && !main.ExecuteCommand("search-selection"), "Selection commands are unavailable when the engine has no selection");
            Exception? paletteFailure=null;
            _ = Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                var dialog=Application.Current.Windows.OfType<Window>().Single(w=>w.Title=="Commands");
                try
                {
                    var panel=(DockPanel)dialog.Content;var query=panel.Children.OfType<TextBox>().Single();var list=panel.Children.OfType<ListBox>().Single();
                    query.Text="Export selection";dialog.UpdateLayout();
                    var selectedCommand = list.Items.Cast<WindowCommand>().Single(c => c.Id == "export-selection");
                    list.ScrollIntoView(selectedCommand); dialog.UpdateLayout();
                    Check(!((ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(selectedCommand)).IsEnabled,"Real command palette renders unavailable actions as disabled");
                    query.Text="__no_such_command__";Check(list.Items.Count==0&&list.SelectedIndex==-1,"Command palette handles an empty filter without selecting an invalid result");
                    query.Text="Always on top";list.SelectedIndex=0;
                    list.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(list)!,Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent});
                }
                catch(Exception failure){paletteFailure=failure;dialog.Close();}
            }));
            main.Palette();if(paletteFailure!=null)throw paletteFailure;
            Check(main.Topmost,"Command palette Enter invokes the named selected action");main.ExecuteCommand("always-on-top");
            main.ExecuteCommand("read-only"); Check(original.ReadOnly && !main.Commands.Single(c=>c.Id=="paste").Enabled, "Read-only mode disables user paste commands"); main.ExecuteCommand("read-only");
            main.ExecuteCommand("mouse-reporting"); Check(!original.MouseReportingEnabled, "Mouse-reporting command enables local mouse interaction"); main.ExecuteCommand("mouse-reporting");
            main.Split(true); await Task.Delay(60); var upper = main.Active!; main.Split(false); await Task.Delay(60); var lower = main.Active!;
            main.NavigatePane(Key.Up); await Task.Delay(50); Check(ReferenceEquals(main.Active,upper), "Spatial navigation selects the pane physically above");
            main.NavigatePane(Key.Down); await Task.Delay(50); Check(ReferenceEquals(main.Active,lower), "Spatial navigation selects the pane physically below");
            main.NavigatePane(Key.Left); await Task.Delay(50); Check(ReferenceEquals(main.Active,original), "Spatial navigation selects the neighboring left pane");
            var before = main.CaptureWorkspace(); double? ratio = before.Tabs[0].Root.Fraction;
            main.ExecuteCommand("split-zoom"); await Task.Delay(50); Check(original.ActualWidth > upper.ActualWidth && main.CaptureWorkspace().Tabs[0].Root.Leaves().Count()==3, "Split zoom expands the active pane without changing the saved tree");
            main.ExecuteCommand("split-zoom"); await Task.Delay(50); Check(Math.Abs(main.CaptureWorkspace().Tabs[0].Root.Fraction!.Value-ratio!.Value)<.03, "Unzoom restores the existing split proportions");
            main.ExecuteCommand("equalize-splits"); await Task.Delay(50); var balanced = main.CaptureWorkspace().Tabs[0].Root;
            Check(Math.Abs(balanced.Fraction!.Value-1d/3)<.03 && Math.Abs(balanced.Second!.Fraction!.Value-.5)<.03, "Equalize gives equal area to three panes across nested orientations");
            double oldWidth = original.Engine.CellWidthPixels;
            upper.Zoom(3); await Task.Delay(30); Check(settings.FontSize==18 && original.Engine.CellWidthPixels==upper.Engine.CellWidthPixels && lower.Engine.CellWidthPixels==upper.Engine.CellWidthPixels && original.Engine.CellWidthPixels>oldWidth, "Font zoom refreshes all panes sharing the settings owner");
            main.ExecuteCommand("reset-font-size"); Check(settings.FontSize==15, "Reset font size restores the loaded configuration size");
            _ = Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                var dialog=Application.Current.Windows.OfType<Window>().Single(w=>w.Title=="Set font size");
                var panel=(StackPanel)dialog.Content; panel.Children.OfType<TextBox>().Single().Text="20";
                panel.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
            main.ExecuteCommand("set-font-size"); Check(settings.FontSize==20, "Set-font-size command applies a validated value through the real dialog");
            main.NewTab("Command Prompt",true); await Task.Delay(30); var second=main.Active!;
            main.ExecuteCommand("move-tab-left"); await Task.Delay(30);
            Check(main.CaptureWorkspace().Tabs[0].Root.Profile=="Command Prompt" && ReferenceEquals(main.Active,second), "Tab reordering preserves the selected terminal owner");
            main.ExecuteCommand("move-tab-right"); await Task.Delay(30); Check(main.CaptureWorkspace().Tabs[1].Root.Profile=="Command Prompt", "Tab reorder works in both directions");
            main.ExecuteCommand("always-on-top"); Check(main.Topmost,"Always-on-top command changes the native window state"); main.ExecuteCommand("always-on-top");
            var style=main.WindowStyle; main.ExecuteCommand("decorations"); Check(main.WindowStyle==WindowStyle.None,"Decoration toggle hides the native frame"); main.ExecuteCommand("decorations"); Check(main.WindowStyle==style,"Decoration toggle restores the native frame");
            main.Width=920;main.Height=610;var position=new Rect(main.Left,main.Top,main.Width,main.Height); main.ToggleFullscreen();main.ToggleFullscreen();
            Check(main.WindowStyle==style && main.WindowState==WindowState.Normal && Math.Abs(main.Width-position.Width)<1 && Math.Abs(main.Height-position.Height)<1, "Fullscreen restores preexisting bounds, frame and window state");
            main.ExecuteCommand("reset-window-size"); Check(main.Width==1160 && main.Height==760,"Reset-window-size restores the documented default geometry");
            main.ExecuteCommand("focus-follows-mouse"); Check(settings.FocusFollowsMouse && Settings.LoadFile(path).FocusFollowsMouse,"Focus-follows-mouse preference persists through the command action");
            main.Split(true); await Task.Delay(30); var hoverTarget=second;main.Activate(); await Task.Delay(30);
            await main.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
            hoverTarget.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseEnterEvent});
            // Observe the synthetic event synchronously: a later real pointer
            // event may legitimately focus another pane under the physical mouse.
            Check(ReferenceEquals(main.Active,hoverTarget)&&ReferenceEquals(Keyboard.FocusedElement,hoverTarget),$"Focus-follows-mouse activates a hovered pane inside the active window (active={main.IsActive}, keyboard={ReferenceEquals(Keyboard.FocusedElement,hoverTarget)}, captured={Mouse.Captured?.GetType().Name ?? "none"})");
        }
        finally { main.Close(); }

        // Verify ownership-preserving layout edits and input blocking against a
        // real child, not only demo frames or command metadata.
        var liveSettings=Settings.LoadFile(path);liveSettings.DefaultProfile="Command Prompt";liveSettings.RestoreTabs=false;liveSettings.ShowWelcome=false;liveSettings.FocusFollowsMouse=false;
        var live=new MainWindow(liveSettings);live.Show();var child=live.Active!;
        try
        {
            for(int i=0;i<100&&child.Console.ProcessId==0;i++)await Task.Delay(30);uint pid=child.Console.ProcessId;
            live.Split(true);await Task.Delay(60);live.ExecuteCommand("split-zoom");await Task.Delay(30);live.ExecuteCommand("split-zoom");await Task.Delay(30);live.ExecuteCommand("equalize-splits");
            Check(pid>0&&child.Console.ProcessId==pid,"Split zoom and equalization preserve a real ConPTY shell process");
            child.Focus();await Task.Delay(30);child.ToggleReadOnly();
            child.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,new TextComposition(InputManager.Current,child,"set /a 936*23\r")){RoutedEvent=TextCompositionManager.TextInputEvent});
            await Task.Delay(120);Check(!child.Engine.Text().Contains("21528"),"Read-only blocks committed text before it reaches a real shell");
            child.ToggleReadOnly();child.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,new TextComposition(InputManager.Current,child,"set /a 936*23\r")){RoutedEvent=TextCompositionManager.TextInputEvent});
            for(int i=0;i<100&&!child.Engine.Text().Contains("21528");i++)await Task.Delay(30);
            Check(child.Engine.Text().Contains("21528"),"Disabling read-only restores real shell input without replacing the session");
            child.ResetTerminal(false);child.Console.Send("set /a 936*29\r");
            for(int i=0;i<100&&!child.Engine.Text().Contains("27144");i++)await Task.Delay(30);
            Check(child.Engine.Text().Contains("27144")&&child.Console.ProcessId==pid,"Real shell remains interactive after local clear-screen");
            child.ResetTerminal(true);child.Console.Send("set /a 936*31\r");
            for(int i=0;i<100&&!child.Engine.Text().Contains("29016");i++)await Task.Delay(30);
            Check(child.Engine.Text().Contains("29016")&&child.Console.ProcessId==pid,"Real shell remains interactive after full terminal reset");
        }
        finally { live.Close();await child.Console.Cleanup.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
}
