using System.Globalization;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace GhosttyWindows;

sealed class TerminalView : FrameworkElement, IDisposable
{
    public readonly Engine Engine = new();
    public readonly PseudoConsole Console = new();
    public readonly string Profile;
    public readonly string LaunchDirectory;
    public bool ReadOnly { get; private set; }
    public bool MouseReportingEnabled { get; private set; } = true;
    public event Action? ModeChanged;
    internal event Action<string>? Warning;
    internal string? BackgroundImageError => backgroundImage.Error;
    public void ToggleReadOnly() { ForwardInput(keyboard.ReleaseAll()); ReleaseMouseReports(); ReadOnly = !ReadOnly; ModeChanged?.Invoke(); }
    public void ToggleMouseReporting() { ReleaseMouseReports(); MouseReportingEnabled = !MouseReportingEnabled; ModeChanged?.Invoke(); }
    public void ResetTerminal(bool full)
    {
        ForwardInput(keyboard.ReleaseAll()); ReleaseMouseReports(); selecting = false; ReleaseMouseCapture();
        Engine.Reset(full); Engine.Theme(settings.Colors.Fg,settings.Colors.Bg);
    }
    public string DirectoryForNewSession
    {
        get
        {
            string candidate = Engine.Pwd;
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && uri.IsFile && (uri.Host.Length == 0 || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = Uri.UnescapeDataString(uri.AbsolutePath);
                if (candidate.Length > 3 && candidate[0] == '/' && candidate[2] == ':') candidate = candidate[1..];
            }
            if (candidate.Length > 2 && char.IsAsciiLetter(candidate[0]) && candidate[1] == ':' && (candidate[2] == '/' || candidate[2] == '\\') && Directory.Exists(candidate)) return candidate;
            return LaunchDirectory;
        }
    }
    public event Action<TerminalView>? Activated;
    public event Action<string>? TitleChanged;
    readonly Settings settings;
    readonly string? commandOverride;
    readonly CancellationTokenSource lifetime = new();
    readonly BackgroundImage backgroundImage = new();
    readonly DispatcherTimer timer;
    readonly Dictionary<uint, Brush> brushes = new();
    readonly Dictionary<(string, uint, uint), FormattedText> glyphs = new();
    int dirty = 1;
    bool started, disposed, selecting, composing;
    internal bool IntroActive { get; private set; }
    readonly System.Diagnostics.Stopwatch introClock = new();
    readonly WindowsKeyboard windowsKeyboard;
    readonly TerminalKeyboard keyboard;
    bool linkClicked;
    long lastAutoScroll;
    readonly Dictionary<int, string> detectedLinks = new();
    readonly HashSet<int> forwardedMouseButtons = new();
    Point lastMouseReport;
    double wheelRemainder;
    string lastTitle = "", find = "";
    double cellWidth = 9, cellHeight = 20;
    internal double OriginX => settings.PaddingHorizontal + (settings.BalancePadding ? Math.Max(0, ActualWidth - 2 * settings.PaddingHorizontal - Engine.Cols * cellWidth) / 2 : 0);
    internal double OriginY => settings.PaddingVertical + (settings.BalancePadding ? Math.Max(0, ActualHeight - 2 * settings.PaddingVertical - Engine.Rows * cellHeight) / 2 : 0);
    Point? hoverPoint;
    string? hoveredTarget;
    ModifierKeys hoverModifiers;
    readonly Dictionary<(uint, uint), uint> contrastColors = new();
    Engine.Frame? frame;
    public TerminalView(Settings settings, string profile, bool demo = false, string? directory = null, string? commandOverride = null)
    {
        this.commandOverride = commandOverride;
        this.settings = settings; backgroundImage.Load(settings.BackgroundImagePath); Profile = profile; LaunchDirectory = directory ?? settings.WorkingDirectory;
        windowsKeyboard = new(() => IsKeyboardFocusWithin); keyboard = new(Engine);
        Focusable = true; ClipToBounds = true; SnapsToDevicePixels = true;
        Cursor = Cursors.IBeam;
        Engine.CaptureUnknown(settings.CaptureProtocolDiagnostics);
        Engine.Theme(settings.Colors.Fg, settings.Colors.Bg);
        Engine.SelectionOptions(settings.UseCustomWordBoundaries ? settings.WordBoundaryCharacters : null, settings.RepeatClickMilliseconds, settings.RepeatClickDistance);
        Engine.Limits(settings.ScrollbackLimitBytes,settings.ImageLimitBytes);
        settings.Changed += RefreshSettings;
        Engine.Changed += () => Interlocked.Exchange(ref dirty, 1);
        Console.Data += ReceiveOutput; Engine.Reply += ForwardInput;
        Console.Failed += message => Dispatcher.BeginInvoke((Action)(() => { if (!disposed) Warning?.Invoke(message); }));
        Console.Exited += code => Dispatcher.BeginInvoke(() => { Engine.Feed($"\r\n\x1b[90m[process exited: {code}]\x1b[0m\r\n"); TitleChanged?.Invoke(Profile + " · exited"); });
        timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => { if (IntroActive) AdvanceIntro(); if (hoverModifiers != Keyboard.Modifiers) { hoverModifiers = Keyboard.Modifiers; UpdateLinkHover(); } if ((settings.CursorBlink || settings.FollowApplicationCursorBlink && frame?.CursorBlink == true) && IsKeyboardFocusWithin) InvalidateVisual(); };
        timer.Tick += (_, _) => { if (!disposed && (Interlocked.Exchange(ref dirty, 0) != 0 || Engine.Held)) { RefreshFrame(); var title = Engine.Title; if (title != lastTitle && title.Length > 0) { lastTitle = title; TitleChanged?.Invoke(title); } } };
        timer.Tick += (_, _) =>
        {
            if (!selecting || !IsMouseCaptured || Mouse.LeftButton != MouseButtonState.Pressed || Environment.TickCount64 - lastAutoScroll < 50) return;
            var p = Mouse.GetPosition(this); int direction = p.Y < OriginY ? -1 : p.Y >= OriginY + Engine.Rows * cellHeight ? 1 : 0;
            if (direction == 0) return; lastAutoScroll = Environment.TickCount64;
            Engine.Scroll(direction); SelectionPointer(2, p, (Keyboard.Modifiers & ModifierKeys.Alt) != 0);
        };
        timer.Start();
        Loaded += (_, _) =>
        {
            UpdateMetrics(); ResizeGrid();
            if (!started && !demo)
            {
                started = true;
                if (commandOverride == null && settings.ShowWelcome && settings.AnimateWelcome && Engine.Cols >= 48 && WelcomeAnimation.Render(0, Engine.Cols, Engine.Rows).Length > 0)
                { IntroActive = true; introClock.Restart(); AdvanceIntro(); }
                else StartShell();
            }
        };
        SizeChanged += (_, _) => ResizeGrid();
        GotKeyboardFocus += (_, _) => { Activated?.Invoke(this); ForwardInput(Engine.Focus(true)); InvalidateVisual(); };
        LostKeyboardFocus += (_, _) => { ForwardInput(keyboard.ReleaseAll()); composing = false; ForwardInput(Engine.Focus(false)); InvalidateVisual(); };
        TextCompositionManager.AddPreviewTextInputStartHandler(this, (_, e) => { if (e.TextComposition.CompositionText.Length > 0) composing = true; });
        TextCompositionManager.AddPreviewTextInputUpdateHandler(this, (_, e) => { if (e.TextComposition.CompositionText.Length > 0) composing = true; });
        TextInput += (_, e) =>
        {
            bool commit = composing; composing = false;
            string text = e.Text.Length > 0 ? e.Text : e.SystemText.Length > 0 ? e.SystemText : e.ControlText;
            if (text.Length > 0) { Send(keyboard.Text(text, commit)); e.Handled = true; }
        };
        PreviewKeyDown += OnKeyDown;
        PreviewKeyUp += (_, e) => { ForwardInput(keyboard.Up(windowsKeyboard.Describe(e))); Dispatcher.BeginInvoke(UpdateLinkHover); };
        MouseLeftButtonDown += (_, e) =>
        {
            Cursor = Cursors.IBeam;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                RefreshFrame(); var link = LinkAt(e.GetPosition(this));
                if (link != null) { OpenLink(link); linkClicked = true; e.Handled = true; return; }
            }
            Focus(); if (ReportMouse(0, 1, e.GetPosition(this))) { CaptureMouse(); e.Handled = true; return; }
            selecting = true; CaptureMouse(); SelectionPointer(0, e.GetPosition(this), false);
            e.Handled = true;
        };
        MouseMove += (_, e) =>
        {
            if(Cursor==Cursors.None)Cursor=Cursors.IBeam;
            var p = e.GetPosition(this); hoverPoint = p;
            if (selecting) SelectionPointer(2, p, (Keyboard.Modifiers & ModifierKeys.Alt) != 0);
            else { ReportMouse(2, e.LeftButton == MouseButtonState.Pressed ? 1 : 0, p); UpdateLinkHover(); }
        };
        MouseLeave += (_, _) => { Cursor = Cursors.IBeam; ToolTip = null; };
        MouseEnter += (_, _) => { FocusOnHover(); UpdateLinkHover(); };
        MouseLeftButtonUp += (_, e) => { if (linkClicked) linkClicked = false; else if (!selecting) ReportMouse(1, 1, e.GetPosition(this)); else { SelectionPointer(1, e.GetPosition(this), false); if (settings.UsePrimarySelection && Engine.HasSelection) CopyPrimarySelection(); if (settings.CopyOnSelect) CopySelection(true); } selecting = false; ReleaseMouseCapture(); e.Handled = true; };
        LostMouseCapture += (_, _) => { if (selecting) { selecting = false; SelectionPointer(1, Mouse.GetPosition(this), false); } };
        MouseRightButtonDown += (_, e) => { Cursor=Cursors.IBeam; Focus(); if (ReportMouse(0, 2, e.GetPosition(this))) e.Handled = true; };
        MouseRightButtonUp += (_, e) => { if (!ReportMouse(1, 2, e.GetPosition(this))) RunMouseAction(settings.RightClickAction,e.GetPosition(this)); e.Handled = true; };
        MouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { Cursor=Cursors.IBeam;Focus();ReportMouse(0, 3, e.GetPosition(this)); e.Handled = true; } };
        MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { if(!ReportMouse(1, 3, e.GetPosition(this)))RunMouseAction(settings.MiddleClickAction,e.GetPosition(this)); e.Handled = true; } };
        MouseWheel += (_, e) => { if(e.Delta!=0){if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) Zoom(e.Delta > 0 ? 1 : -1); else if (!ReportMouse(0, e.Delta > 0 ? 4 : 5, e.GetPosition(this))) ScrollWheel(e.Delta);} e.Handled = true; };
        AllowDrop = true;
        Drop += async (_, e) => { if (!ReadOnly && e.Data.GetData(DataFormats.FileDrop) is string[] files) { e.Handled = true; try { if (commandOverride != null) throw new ArgumentException("File drops need a known shell profile; this tab uses a custom command."); string quoted = await ShellFiles.QuoteDrop(Profile, files, lifetime.Token); if (!disposed) Send(Encoding.UTF8.GetBytes(quoted)); } catch (Exception failure) when (failure is ArgumentException or IOException or System.ComponentModel.Win32Exception or OperationCanceledException) { if (!disposed) Warning?.Invoke("Files were not inserted: " + failure.Message); } } };
    }
    internal void ScrollWheel(int delta)
    {
        if(settings.WheelScrollLines==0){wheelRemainder=0;return;}
        wheelRemainder += -(double)delta/120*settings.WheelScrollLines;
        int lines=(int)wheelRemainder;wheelRemainder-=lines;if(lines!=0)Engine.Scroll(lines);
    }
    internal void RunMouseAction(string action, Point point)
    {
        switch(action)
        {
            case "context-menu":RefreshFrame();var menu=BuildContextMenu(point);menu.PlacementTarget=this;menu.IsOpen=true;break;
            case "paste":Paste();break;
            case "primary-paste":PastePrimarySelection();break;
            case "copy":CopySelection();break;
            case "copy-or-paste":if(Engine.HasSelection)CopySelection();else Paste();break;
            case "none":break;
            default:throw new ArgumentException("Unknown mouse action");
        }
    }
    bool ReportMouse(int action, int button, Point p)
    {
        if (action == 1 && !forwardedMouseButtons.Remove(button)) return false;
        if (action != 1 && (ReadOnly || !MouseReportingEnabled || (settings.ShiftMouseOverride && (Keyboard.Modifiers & ModifierKeys.Shift) != 0))) return false;
        var bytes = Engine.Mouse(action, button, Mods(), (float)Math.Max(0, (p.X - OriginX) / cellWidth * Engine.CellWidthPixels), (float)Math.Max(0, (p.Y - OriginY) / cellHeight * Engine.CellHeightPixels), Engine.CellWidthPixels, Engine.CellHeightPixels, Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed);
        if (bytes == null) return false;
        lastMouseReport = p; if (action == 0 && button is >= 1 and <= 3) forwardedMouseButtons.Add(button);
        ForwardInput(bytes); return true;
    }
    internal bool FocusOnHover()
    {
        if (!settings.FocusFollowsMouse || Window.GetWindow(this)?.IsActive != true || Mouse.Captured != null || !Focus()) return false;
        // A native focus change is not raised when the pane already has keyboard
        // focus. A deliberate hover still needs to synchronize its UI owner.
        Activated?.Invoke(this); return true;
    }
    void ReleaseMouseReports()
    {
        foreach (int button in forwardedMouseButtons.ToArray())
        {
            forwardedMouseButtons.Remove(button);
            var bytes = Engine.Mouse(1,button,0,(float)Math.Max(0,(lastMouseReport.X-OriginX)/cellWidth*Engine.CellWidthPixels),(float)Math.Max(0,(lastMouseReport.Y-OriginY)/cellHeight*Engine.CellHeightPixels),Engine.CellWidthPixels,Engine.CellHeightPixels,forwardedMouseButtons.Count>0);
            if (bytes != null) ForwardInput(bytes);
        }
    }
    internal void RefreshFrame()
    {
        try { frame = Engine.Capture(); detectedLinks.Clear(); foreach (var link in TerminalLinks.Detect(frame, Engine.Cols)) foreach (int cell in link.Cells) detectedLinks[cell] = link.Target; UpdateLinkHover(); }
        catch (Exception e) when (e is not OutOfMemoryException and not AccessViolationException) { frame = null; detectedLinks.Clear(); Warning?.Invoke("This pane could not refresh: " + e.Message + ". Reset or close this pane to recover."); }
        InvalidateVisual();
    }
    internal void SelectionPointer(int action, Point p, bool rectangle)
    {
        var cell = CellAt(p); Engine.Gesture(action, (int)cell.X, (int)cell.Y, (p.X - OriginX) / cellWidth * Engine.CellWidthPixels, (p.Y - OriginY) / cellHeight * Engine.CellHeightPixels, rectangle);
    }
    internal string? HoveredLink => hoveredTarget;
    internal void CopyHoveredLink() { if (HoveredLink is string link) CopyText(link); }
    void UpdateLinkHover()
    {
        if (hoverPoint is not Point point || !IsMouseOver || Cursor == Cursors.None || selecting || Mouse.Captured != null) return;
        point = Mouse.GetPosition(this); hoverPoint = point;
        string? link = LinkAt(point); hoveredTarget = link; Cursor = link != null && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? Cursors.Hand : Cursors.IBeam;
        ToolTip = link == null ? null : link + "\nCtrl+click to open";
    }
    internal string? LinkAt(Point p)
    {
        if (Engine.Held || p.X < OriginX || p.Y < OriginY || p.X >= OriginX + Engine.Cols * cellWidth || p.Y >= OriginY + Engine.Rows * cellHeight) return null;
        var cell = CellAt(p); string explicitLink = Engine.Link((int)cell.X, (int)cell.Y);
        if (explicitLink.Length > 0) return TerminalLinks.IsWeb(explicitLink) ? explicitLink : null;
        return detectedLinks.GetValueOrDefault((int)cell.Y * Engine.Cols + (int)cell.X);
    }
    void OpenLink(string target)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception failure) { Dialogs.Show(Window.GetWindow(this), failure.Message, "Could not open link"); }
    }
    internal ContextMenu BuildContextMenu(Point p)
    {
        var menu = new ContextMenu();
        MenuItem Add(string text, Action action, bool enabled = true) { var item = new MenuItem { Header = text, IsEnabled = enabled }; item.Click += (_, _) => action(); menu.Items.Add(item); return item; }
        string? link = LinkAt(p);
        if (link != null) { Add("Open link", () => OpenLink(link)); Add("Copy link address", () => CopyText(link)); menu.Items.Add(new Separator()); }
        Add("Copy", () => CopySelection(), Engine.HasSelection);
        bool? paste = null; string? clipboardError = null; try { paste = Clipboard.ContainsText(); } catch (COMException e) { clipboardError = e.Message; }
        var pasteItem = Add("Paste", Paste, paste != false && !ReadOnly); pasteItem.ToolTip = clipboardError == null ? null : "Clipboard availability is unknown. Retry Paste: " + clipboardError; Add("Select all", Engine.SelectAll); return menu;
    }
    public void RefreshSettings() { Engine.CaptureUnknown(settings.CaptureProtocolDiagnostics); contrastColors.Clear(); Engine.SelectionOptions(settings.UseCustomWordBoundaries ? settings.WordBoundaryCharacters : null, settings.RepeatClickMilliseconds, settings.RepeatClickDistance); backgroundImage.Load(settings.BackgroundImagePath); if (backgroundImage.Error != null) Warning?.Invoke("Background image: " + backgroundImage.Error); UpdateMetrics(); glyphs.Clear(); brushes.Clear(); Engine.Theme(settings.Colors.Fg, settings.Colors.Bg); Engine.Limits(settings.ScrollbackLimitBytes,settings.ImageLimitBytes); if(!settings.HideMouseWhileTyping)Cursor=Cursors.IBeam; ResizeGrid(); }
    public void Zoom(int delta) => settings.SetRuntimeFontSize(Math.Clamp(settings.FontSize + delta, 8, 48));
    void UpdateMetrics()
    {
        try
        {
            var text = MakeText("M", settings.Colors.Fg, 0);
            double width = Math.Max(4, text.WidthIncludingTrailingWhitespace * settings.CellWidthScale), height = Math.Ceiling((text.Height + 2) * settings.CellHeightScale);
            if (!double.IsFinite(width) || !double.IsFinite(height) || height <= 0) throw new ArgumentException("Invalid computed cell metrics.");
            cellWidth = width; cellHeight = height;
        }
        catch (Exception e) when (e is not OutOfMemoryException and not AccessViolationException)
        { Warning?.Invoke("This pane could not measure its font: " + e.Message + ". Correct the font settings to recover."); }
    }
    void ResizeGrid()
    {
        if (disposed || ActualWidth < 20 || ActualHeight < 20) return;
        int cols = Math.Clamp((int)((ActualWidth - 2 * settings.PaddingHorizontal) / cellWidth), 2, 500), rows = Math.Clamp((int)((ActualHeight - 2 * settings.PaddingVertical) / cellHeight), 2, 250);
        var dpi = VisualTreeHelper.GetDpi(this);
        int cw = (int)Math.Ceiling(cellWidth * dpi.DpiScaleX), ch = (int)Math.Ceiling(cellHeight * dpi.DpiScaleY);
        bool gridChanged = cols != Engine.Cols || rows != Engine.Rows;
        if (!gridChanged && cw == Engine.CellWidthPixels && ch == Engine.CellHeightPixels) return;
        if (selecting) { selecting = false; ReleaseMouseCapture(); }
        Engine.Resize(cols, rows, cw, ch);
        if (started && !IntroActive && gridChanged) Console.Resize(cols, rows);
        Interlocked.Exchange(ref dirty, 1);
    }
    internal Point CellAt(Point p) => new(Math.Clamp((int)((p.X - OriginX) / cellWidth), 0, Engine.Cols - 1), Math.Clamp((int)((p.Y - OriginY) / cellHeight), 0, Engine.Rows - 1));
    Brush Brush(uint rgb)
    {
        if (!brushes.TryGetValue(rgb, out var b)) { b = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)); b.Freeze(); brushes[rgb] = b; } return b;
    }
    FormattedText MakeText(string text, uint fg, uint flags) => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        new Typeface(new FontFamily(settings.StyledFont(flags)), (flags & 2) != 0 ? FontStyles.Italic : FontStyles.Normal, (flags & 1) != 0 ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
        settings.FontSize, Brush(fg), VisualTreeHelper.GetDpi(this).PixelsPerDip);
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) { base.OnDpiChanged(oldDpi, newDpi); glyphs.Clear(); UpdateMetrics(); ResizeGrid(); }
    protected override void OnRender(DrawingContext target)
    {
        try
        {
            var drawing = new DrawingGroup(); using (var context = drawing.Open()) DrawTerminal(context);
            target.DrawDrawing(drawing);
        }
        catch (Exception e) when (e is not OutOfMemoryException and not AccessViolationException)
        {
            var message = new FormattedText("This pane could not render. Change its font/image settings or reset the terminal.\n" + e.Message, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(SystemFonts.MessageFontFamily.Source), 12, Brushes.IndianRed, VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth = Math.Max(1, ActualWidth - 20) };
            target.DrawText(message, new Point(10, 10));
        }
    }
    void DrawTerminal(DrawingContext dc)
    {
        dc.PushOpacity((Window.GetWindow(this) as MainWindow)?.BackdropActive == true ? settings.BackgroundOpacity / 100 : 1);
        dc.DrawRectangle(Brush(settings.Colors.Bg), null, new Rect(RenderSize));
        dc.Pop();
        backgroundImage.Draw(dc, RenderSize, settings);
        if (frame == null) return;
        dc.PushOpacity(IsKeyboardFocusWithin ? 1 : settings.InactivePaneOpacity);
        int cols = Engine.Cols;
        var matches = new HashSet<int>();
        if (find.Length > 0)
        {
            for (int y = 0; y < Engine.Rows; y++)
            {
                var row = new StringBuilder(); var mapping = new List<int>();
                for (int x = 0; x < cols && y * cols + x < frame.Text.Length; x++) { string s = frame.Text[y * cols + x]; if (s.Length == 0) s = " "; row.Append(s); for (int j = 0; j < s.Length; j++) mapping.Add(y * cols + x); }
                var line = row.ToString(); for (int start = 0; start < line.Length;)
                {
                    int pos = line.IndexOf(find, start, StringComparison.OrdinalIgnoreCase); if (pos < 0) break;
                    for (int k = pos; k < pos + find.Length; k++) matches.Add(mapping[k]);
                    start = pos + Math.Max(1, find.Length);
                }
            }
        }
        TerminalImages.Draw(dc, frame.Images.Where(p => p.Info.Z < int.MinValue / 2), cellWidth, cellHeight, Engine.CellWidthPixels, Engine.CellHeightPixels, OriginX, OriginY);
        for (int i = 0; i < frame.Cells.Length; i++)
        {
            var c = frame.Cells[i]; int x = i % cols, y = i / cols;
            var rect = new Rect(OriginX + x * cellWidth, OriginY + y * cellHeight, cellWidth + .2, cellHeight);
            uint bg = (c.Flags & 32) != 0 ? Settings.ParseColor(settings.SelectionBackground) : matches.Contains(i) ? Settings.ParseColor(settings.SearchBackground) : c.Bg;
            if (bg != settings.Colors.Bg) dc.DrawRectangle(Brush(bg), null, rect);
        }
        TerminalImages.Draw(dc, frame.Images.Where(p => p.Info.Z >= int.MinValue / 2 && p.Info.Z < 0), cellWidth, cellHeight, Engine.CellWidthPixels, Engine.CellHeightPixels, OriginX, OriginY);
        for (int i = 0; i < frame.Cells.Length; i++)
        {
            var c = frame.Cells[i]; int x = i % cols, y = i / cols;
            var rect = new Rect(OriginX + x * cellWidth, OriginY + y * cellHeight, cellWidth + .2, cellHeight);
            if (c.Length == 0 || (c.Flags & 16) != 0) continue;
            uint renderedBg = (c.Flags & 32) != 0 ? Settings.ParseColor(settings.SelectionBackground) : matches.Contains(i) ? Settings.ParseColor(settings.SearchBackground) : c.Bg;
            var colors = (c.Fg, renderedBg); if (!contrastColors.TryGetValue(colors, out uint fg)) { if (contrastColors.Count > 8192) contrastColors.Clear(); fg = TextContrast.Adjust(c.Fg, renderedBg, settings.MinimumTextContrast); contrastColors[colors] = fg; }
            var key = (frame.Text[i], fg, c.Flags & 3);
            if (!glyphs.TryGetValue(key, out var glyph)) { if (glyphs.Count > 8192) glyphs.Clear(); glyph = MakeText(key.Item1, fg, c.Flags); glyphs[key] = glyph; }
            if ((c.Flags & 64) != 0) dc.PushOpacity(.55);
            dc.DrawText(glyph, new Point(rect.X, rect.Y + settings.TextBaselineOffset));
            if ((c.Flags & 4) != 0) dc.DrawLine(new Pen(Brush(fg), settings.DecorationThickness), new Point(rect.X, rect.Bottom - 2), new Point(rect.Right, rect.Bottom - 2));
            if ((c.Flags & 8) != 0) dc.DrawLine(new Pen(Brush(fg), settings.DecorationThickness), new Point(rect.X, rect.Y + cellHeight / 2), new Point(rect.Right, rect.Y + cellHeight / 2));
            if ((c.Flags & 64) != 0) dc.Pop();
        }
        TerminalImages.Draw(dc, frame.Images.Where(p => p.Info.Z >= 0), cellWidth, cellHeight, Engine.CellWidthPixels, Engine.CellHeightPixels, OriginX, OriginY);
        bool blinkVisible = !(settings.FollowApplicationCursorBlink ? frame.CursorBlink : settings.CursorBlink) || !IsKeyboardFocusWithin || (Environment.TickCount64 / settings.CursorBlinkMilliseconds) % 2 == 0;
        if (frame.CursorStyle != 0 && blinkVisible)
        {
            var r = new Rect(OriginX + frame.CursorX * cellWidth, OriginY + frame.CursorY * cellHeight, cellWidth, cellHeight);
            string shape = IsKeyboardFocusWithin ? settings.CursorShape : settings.UnfocusedCursor;
            if (shape == "Application") shape = frame.CursorStyle switch { 1 => "Bar", 3 => "Underline", 4 => "Hollow", _ => "Block" };
            uint cursorRgb = settings.CursorColor.Length == 0 ? settings.Colors.Fg : Settings.ParseColor(settings.CursorColor);
            var color = Brush(cursorRgb);
            dc.PushOpacity(settings.CursorOpacity);
            if (shape == "Hollow") dc.DrawRectangle(null, new Pen(color, 1), r);
            else if (shape == "Bar") dc.DrawRectangle(color, null, new Rect(r.X, r.Y, 2, r.Height));
            else if (shape == "Underline") dc.DrawRectangle(color, null, new Rect(r.X, r.Bottom - 2, r.Width, 2));
            else if (shape != "Hidden") dc.DrawRectangle(color, null, r);
            dc.Pop();
            int index = frame.CursorY * Engine.Cols + frame.CursorX;
            if (shape == "Block" && IsKeyboardFocusWithin && index >= 0 && index < frame.Text.Length && frame.Text[index].Length > 0 && (frame.Cells[index].Flags & 16) == 0)
            {
                uint Blend(int shift) => (uint)Math.Round(((cursorRgb >> shift) & 255) * settings.CursorOpacity + ((frame.Cells[index].Bg >> shift) & 255) * (1 - settings.CursorOpacity));
                uint background = Blend(16) << 16 | Blend(8) << 8 | Blend(0);
                uint textColor = settings.CursorTextColor.Length > 0 ? Settings.ParseColor(settings.CursorTextColor) : TextContrast.Adjust(frame.Cells[index].Fg, background, 4.5);
                dc.PushClip(new RectangleGeometry(r));
                dc.DrawText(MakeText(frame.Text[index], textColor, frame.Cells[index].Flags), new Point(r.X, r.Y + settings.TextBaselineOffset)); dc.Pop();
            }
        }
        dc.Pop();
    }

    public int Find(string query, bool next = false) { find = query; int count = Engine.Find(query, next); Interlocked.Exchange(ref dirty, 1); return count; }
    public bool CopySelection(bool preserveClickSequence = false)
    {
        bool copied=CopyText(ClipboardContent.Plain(Engine.FormatText(1, 0, settings.CopyTrimTrailing), settings));if(copied&&settings.ClearSelectionOnCopy)ClearLocalSelection(preserveClickSequence);return copied;
    }
    internal void ClearLocalSelection(bool preserveClickSequence = false){selecting=false;ReleaseMouseCapture();Engine.ClearSelection(preserveClickSequence);}
    internal static bool CopyText(string text)
    {
        if (text.Length == 0) return false;
        try { Clipboard.SetText(text); return true; }
        catch (COMException failure) { Dialogs.Show(failure.Message, "Could not copy text"); return false; }
    }
    internal void CopyPrimarySelection()
    {
        string value = ClipboardContent.Plain(Engine.FormatText(1, 0, settings.CopyTrimTrailing), settings);
        if (!PrimarySelection.Store(value)) Warning?.Invoke("Primary selection exceeds the 8 MiB buffer limit; the previous selection was kept.");
    }
    internal void PastePrimarySelection()
    {
        if (PrimarySelection.Read() is string value) PasteText(false, value);
        else Warning?.Invoke("No primary selection has been copied in this application session.");
    }
    public void Paste() => PasteText(false);
    internal void PasteSingleLine() => PasteText(true);
    void PasteText(bool singleLine, string? supplied = null)
    {
        if (ReadOnly) return;
        string text;
        try { if (supplied != null) text = supplied; else { if (!Clipboard.ContainsText()) return; text = Clipboard.GetText(); } }
        catch (COMException e) { Dialogs.Show(e.Message, "Could not read clipboard"); return; }
        int removed;
        try { text = PastePolicy.Prepare(text, singleLine, settings.PasteControlPolicy, out removed); }
        catch (ArgumentException e) { Warning?.Invoke(e.Message); return; }
        bool bracketed = Engine.Paste("") != "";
        if (settings.ConfirmMultilinePaste && (!bracketed || settings.ConfirmBracketedMultilinePaste) && (text.Contains('\n') || text.Contains('\r')) && Dialogs.Show("Paste multiple lines into the shell? These lines may execute commands.", "Paste", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        // Use terminal's mode-aware bracketed paste via the native mode query.
        if (removed > 0) Warning?.Invoke($"Paste policy removed {removed} control characters.");
        Send(Encoding.UTF8.GetBytes(Engine.Paste(text)));
    }
    void ReceiveOutput(byte[] bytes, int count)
    {
        try { Engine.Feed(bytes, count); }
        catch (Exception e) when (e is not OutOfMemoryException and not AccessViolationException)
        { Dispatcher.BeginInvoke((Action)(() => { if (!disposed) Warning?.Invoke("Terminal output could not be processed: " + e.Message + ". Reset or close this pane to recover."); })); }
    }
    internal string[] UnsupportedPreferenceKeys => settings.Extra?.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
    long acceptedInputBytes, acceptedInputMessages;
    internal object TransportDiagnostics => new { acceptedInputBytes = Interlocked.Read(ref acceptedInputBytes), acceptedInputMessages = Interlocked.Read(ref acceptedInputMessages), queuedInputBytes = Console.QueuedInputBytes, glyphCacheEntries = glyphs.Count, brushCacheEntries = brushes.Count };
    void ForwardInput(byte[] bytes)
    {
        try { Console.Send(bytes); if (bytes.Length > 0) { Interlocked.Add(ref acceptedInputBytes, bytes.Length); Interlocked.Increment(ref acceptedInputMessages); } }
        catch (IOException e) { Dispatcher.BeginInvoke((Action)(() => { if (!disposed) Warning?.Invoke("Input was not sent: " + e.Message); })); }
    }
    internal void SendAction(byte[] bytes) => Send(bytes);
    void Send(byte[] b) { if (IntroActive) { FinishIntro(); return; } if (ReadOnly || b.Length == 0) return; if(settings.ClearSelectionOnTyping)ClearLocalSelection(); Engine.Scroll(0); ForwardInput(b);if(settings.HideMouseWhileTyping)Cursor=Cursors.None; }
    static int Mods() => ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 1 : 0) | ((Keyboard.Modifiers & ModifierKeys.Control) != 0 ? 2 : 0) | ((Keyboard.Modifiers & ModifierKeys.Alt) != 0 ? 4 : 0) | ((Keyboard.Modifiers & ModifierKeys.Windows) != 0 ? 8 : 0);
    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (IntroActive) { FinishIntro(); e.Handled = true; return; }
        Key key = e.Key == Key.System ? e.SystemKey : e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;
        bool appShortcuts = Window.GetWindow(this) is MainWindow owner ? owner.UseApplicationShortcuts(e) : settings.ShortcutPrecedence == "Application";
        if (appShortcuts && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.C) { CopySelection(); e.Handled = true; return; }
        if (appShortcuts && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.A) { Engine.SelectAll(); e.Handled = true; return; }
        if (appShortcuts && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.V) { Paste(); e.Handled = true; return; }
        if (appShortcuts && Keyboard.Modifiers == ModifierKeys.Shift && key == Key.Insert) { Paste(); e.Handled = true; return; }
        if (appShortcuts && Keyboard.Modifiers == ModifierKeys.Shift && (key == Key.PageUp || key == Key.PageDown)) { Engine.Scroll(key == Key.PageUp ? -Engine.Rows : Engine.Rows); e.Handled = true; return; }
        if (ReadOnly) { e.Handled = true; return; }
        UpdateLinkHover();
        var stroke = windowsKeyboard.Describe(e);
        if (composing || e.Key == Key.ImeProcessed) { keyboard.Down(stroke, e.IsRepeat, true); return; }
        if (stroke.Modifier) { ForwardInput(keyboard.Down(stroke, e.IsRepeat, false)); return; }
        bool special = key is Key.Enter or Key.Back or Key.Tab or Key.Escape or Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Insert or Key.Delete or Key.Clear || (key >= Key.F1 && key <= Key.F24);
        // AltGr text goes through TextInput, rather than being encoded as Ctrl+Alt.
        bool shortcut = (Keyboard.Modifiers & ModifierKeys.Control) != 0 && !stroke.AltGr;
        bool nonText = key is Key.CapsLock or Key.NumLock or Key.Scroll or Key.Pause or Key.PrintScreen or Key.Apps;
        bool immediate = special || shortcut || nonText;
        Send(keyboard.Down(stroke, e.IsRepeat, !immediate, shortcut ? stroke.LayoutText : ""));
        if (immediate) e.Handled = true;
    }
    void StartShell()
    {
        if (disposed) return;
        try { Console.Start(commandOverride ?? Welcome.Command(Profile, Engine.Cols, settings.ShowWelcome), LaunchDirectory, Engine.Cols, Engine.Rows); }
        catch (Exception e) { Engine.Feed("\x1b[31mUnable to start shell: " + e.Message + "\x1b[0m\r\n"); }
    }
    void AdvanceIntro()
    {
        if (introClock.Elapsed.TotalSeconds >= 5.35) { FinishIntro(); return; }
        var state = WelcomeAnimation.At(introClock.Elapsed.TotalSeconds);
        string picture = WelcomeAnimation.Render(state.Turn, Engine.Cols, Engine.Rows, state.Expression, WelcomeAnimation.Bounce(introClock.Elapsed.TotalSeconds));
        if (picture.Length == 0) { FinishIntro(); return; }
        Engine.Feed(picture.Replace("animation preview", "welcome"));
    }
    internal void FinishIntro()
    {
        if (!IntroActive || disposed) return;
        IntroActive = false; introClock.Stop();
        Engine.Reset(true); Engine.Theme(settings.Colors.Fg, settings.Colors.Bg);
        StartShell();
    }
    public void Dispose() { IntroActive = false; introClock.Stop(); if (disposed) return; disposed = true; lifetime.Cancel(); lifetime.Dispose(); settings.Changed -= RefreshSettings; timer.Stop(); windowsKeyboard.Dispose(); Console.Dispose(); Engine.Dispose(); }
}
