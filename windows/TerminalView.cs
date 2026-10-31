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
    readonly DispatcherTimer timer;
    readonly Dictionary<uint, Brush> brushes = new();
    readonly Dictionary<(string, uint, uint), FormattedText> glyphs = new();
    int dirty = 1;
    bool started, disposed, selecting, composing;
    readonly WindowsKeyboard windowsKeyboard;
    readonly TerminalKeyboard keyboard;
    bool linkClicked;
    long lastAutoScroll;
    readonly Dictionary<int, string> detectedLinks = new();
    string lastTitle = "", find = "";
    double cellWidth = 9, cellHeight = 20;
    Engine.Frame? frame;
    public TerminalView(Settings settings, string profile, bool demo = false, string? directory = null)
    {
        this.settings = settings; Profile = profile; LaunchDirectory = directory ?? settings.WorkingDirectory;
        windowsKeyboard = new(() => IsKeyboardFocusWithin); keyboard = new(Engine);
        Focusable = true; ClipToBounds = true; SnapsToDevicePixels = true;
        Cursor = Cursors.IBeam;
        Engine.Theme(settings.Colors.Fg, settings.Colors.Bg);
        Engine.Changed += () => Interlocked.Exchange(ref dirty, 1);
        Console.Data += Engine.Feed; Engine.Reply += Console.Send;
        Console.Exited += code => Dispatcher.BeginInvoke(() => { Engine.Feed($"\r\n\x1b[90m[process exited: {code}]\x1b[0m\r\n"); TitleChanged?.Invoke(Profile + " · exited"); });
        timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => { if (!disposed && (Interlocked.Exchange(ref dirty, 0) != 0 || Engine.Held)) { RefreshFrame(); var title = Engine.Title; if (title != lastTitle && title.Length > 0) { lastTitle = title; TitleChanged?.Invoke(title); } } };
        timer.Tick += (_, _) =>
        {
            if (!selecting || !IsMouseCaptured || Mouse.LeftButton != MouseButtonState.Pressed || Environment.TickCount64 - lastAutoScroll < 50) return;
            var p = Mouse.GetPosition(this); int direction = p.Y < 8 ? -1 : p.Y >= 8 + Engine.Rows * cellHeight ? 1 : 0;
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
                try { Console.Start(Settings.Profiles[profile], LaunchDirectory, Engine.Cols, Engine.Rows); }
                catch (Exception e) { Engine.Feed("\x1b[31mUnable to start shell: " + e.Message + "\x1b[0m\r\n"); }
            }
        };
        SizeChanged += (_, _) => ResizeGrid();
        GotKeyboardFocus += (_, _) => { Activated?.Invoke(this); Console.Send(Engine.Focus(true)); InvalidateVisual(); };
        LostKeyboardFocus += (_, _) => { Console.Send(keyboard.ReleaseAll()); composing = false; Console.Send(Engine.Focus(false)); InvalidateVisual(); };
        TextCompositionManager.AddPreviewTextInputStartHandler(this, (_, e) => { if (e.TextComposition.CompositionText.Length > 0) composing = true; });
        TextCompositionManager.AddPreviewTextInputUpdateHandler(this, (_, e) => { if (e.TextComposition.CompositionText.Length > 0) composing = true; });
        TextInput += (_, e) =>
        {
            bool commit = composing; composing = false;
            string text = e.Text.Length > 0 ? e.Text : e.SystemText.Length > 0 ? e.SystemText : e.ControlText;
            if (text.Length > 0) { Send(keyboard.Text(text, commit)); e.Handled = true; }
        };
        PreviewKeyDown += OnKeyDown;
        PreviewKeyUp += (_, e) => Console.Send(keyboard.Up(windowsKeyboard.Describe(e)));
        MouseLeftButtonDown += (_, e) =>
        {
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
            var p = e.GetPosition(this);
            if (selecting) SelectionPointer(2, p, (Keyboard.Modifiers & ModifierKeys.Alt) != 0);
            else { ReportMouse(2, e.LeftButton == MouseButtonState.Pressed ? 1 : 0, p); var link = LinkAt(p); Cursor = link != null && (Keyboard.Modifiers & ModifierKeys.Control) != 0 ? Cursors.Hand : Cursors.IBeam; ToolTip = link == null ? null : link + "\nCtrl+click to open"; }
        };
        MouseLeave += (_, _) => { Cursor = Cursors.IBeam; ToolTip = null; };
        MouseLeftButtonUp += (_, e) => { if (linkClicked) linkClicked = false; else if (!selecting) ReportMouse(1, 1, e.GetPosition(this)); else { SelectionPointer(1, e.GetPosition(this), false); if (settings.CopyOnSelect) CopySelection(); } selecting = false; ReleaseMouseCapture(); e.Handled = true; };
        LostMouseCapture += (_, _) => { if (selecting) { selecting = false; SelectionPointer(1, Mouse.GetPosition(this), false); } };
        MouseRightButtonDown += (_, e) => { Focus(); if (ReportMouse(0, 2, e.GetPosition(this))) e.Handled = true; };
        MouseRightButtonUp += (_, e) => { if (!ReportMouse(1, 2, e.GetPosition(this))) { RefreshFrame(); var menu = BuildContextMenu(e.GetPosition(this)); menu.PlacementTarget = this; menu.IsOpen = true; } e.Handled = true; };
        MouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { Focus(); if (!ReportMouse(0, 3, e.GetPosition(this))) Paste(); e.Handled = true; } };
        MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { ReportMouse(1, 3, e.GetPosition(this)); e.Handled = true; } };
        MouseWheel += (_, e) => { if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) Zoom(e.Delta > 0 ? 1 : -1); else if (!ReportMouse(0, e.Delta > 0 ? 4 : 5, e.GetPosition(this))) Engine.Scroll(e.Delta > 0 ? -4 : 4); e.Handled = true; };
        AllowDrop = true;
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] files) Console.Send(string.Join(" ", files.Select(p => "\"" + p.Replace("\"", "") + "\""))); };
    }
    bool ReportMouse(int action, int button, Point p)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) return false;
        var bytes = Engine.Mouse(action, button, Mods(), (float)Math.Max(0, (p.X - 10) / cellWidth * Engine.CellWidthPixels), (float)Math.Max(0, (p.Y - 8) / cellHeight * Engine.CellHeightPixels), Engine.CellWidthPixels, Engine.CellHeightPixels, Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed);
        if (bytes == null) return false; Console.Send(bytes); return true;
    }
    internal void RefreshFrame() { frame = Engine.Capture(); detectedLinks.Clear(); foreach (var link in TerminalLinks.Detect(frame, Engine.Cols)) foreach (int cell in link.Cells) detectedLinks[cell] = link.Target; InvalidateVisual(); }
    internal void SelectionPointer(int action, Point p, bool rectangle)
    {
        var cell = CellAt(p); Engine.Gesture(action, (int)cell.X, (int)cell.Y, (p.X - 10) / cellWidth * Engine.CellWidthPixels, (p.Y - 8) / cellHeight * Engine.CellHeightPixels, rectangle);
    }
    internal string? LinkAt(Point p)
    {
        if (Engine.Held || p.X < 10 || p.Y < 8 || p.X >= 10 + Engine.Cols * cellWidth || p.Y >= 8 + Engine.Rows * cellHeight) return null;
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
        void Add(string text, Action action, bool enabled = true) { var item = new MenuItem { Header = text, IsEnabled = enabled }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        string? link = LinkAt(p);
        if (link != null) { Add("Open link", () => OpenLink(link)); Add("Copy link address", () => { try { Clipboard.SetText(link); } catch (COMException) { } }); menu.Items.Add(new Separator()); }
        Add("Copy", () => CopySelection(), Engine.Text(true).Length > 0);
        bool paste = false; try { paste = Clipboard.ContainsText(); } catch (COMException) { }
        Add("Paste", Paste, paste); Add("Select all", Engine.SelectAll); return menu;
    }
    public void RefreshSettings() { UpdateMetrics(); glyphs.Clear(); brushes.Clear(); Engine.Theme(settings.Colors.Fg, settings.Colors.Bg); ResizeGrid(); }
    public void Zoom(int delta) { settings.FontSize = Math.Clamp(settings.FontSize + delta, 8, 48); RefreshSettings(); }
    void UpdateMetrics()
    {
        var text = MakeText("M", settings.Colors.Fg, 0);
        cellWidth = Math.Max(4, text.WidthIncludingTrailingWhitespace); cellHeight = Math.Ceiling(text.Height + 2);
    }
    void ResizeGrid()
    {
        if (disposed || ActualWidth < 20 || ActualHeight < 20) return;
        int cols = Math.Clamp((int)((ActualWidth - 20) / cellWidth), 2, 500), rows = Math.Clamp((int)((ActualHeight - 16) / cellHeight), 2, 250);
        var dpi = VisualTreeHelper.GetDpi(this);
        int cw = (int)Math.Ceiling(cellWidth * dpi.DpiScaleX), ch = (int)Math.Ceiling(cellHeight * dpi.DpiScaleY);
        bool gridChanged = cols != Engine.Cols || rows != Engine.Rows;
        if (!gridChanged && cw == Engine.CellWidthPixels && ch == Engine.CellHeightPixels) return;
        if (selecting) { selecting = false; ReleaseMouseCapture(); }
        Engine.Resize(cols, rows, cw, ch);
        if (started && gridChanged) Console.Resize(cols, rows);
        Interlocked.Exchange(ref dirty, 1);
    }
    Point CellAt(Point p) => new(Math.Clamp((int)((p.X - 10) / cellWidth), 0, Engine.Cols - 1), Math.Clamp((int)((p.Y - 8) / cellHeight), 0, Engine.Rows - 1));
    Brush Brush(uint rgb)
    {
        if (!brushes.TryGetValue(rgb, out var b)) { b = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)); b.Freeze(); brushes[rgb] = b; } return b;
    }
    FormattedText MakeText(string text, uint fg, uint flags) => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        new Typeface(new FontFamily(settings.FontFamily), (flags & 2) != 0 ? FontStyles.Italic : FontStyles.Normal, (flags & 1) != 0 ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
        settings.FontSize, Brush(fg), VisualTreeHelper.GetDpi(this).PixelsPerDip);
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) { base.OnDpiChanged(oldDpi, newDpi); glyphs.Clear(); UpdateMetrics(); ResizeGrid(); }
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brush(settings.Colors.Bg), null, new Rect(RenderSize));
        if (frame == null) return;
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
        TerminalImages.Draw(dc, frame.Images.Where(p => p.Info.Z < int.MinValue / 2), cellWidth, cellHeight, Engine.CellWidthPixels, Engine.CellHeightPixels);
        for (int i = 0; i < frame.Cells.Length; i++)
        {
            var c = frame.Cells[i]; int x = i % cols, y = i / cols;
            var rect = new Rect(10 + x * cellWidth, 8 + y * cellHeight, cellWidth + .2, cellHeight);
            uint bg = (c.Flags & 32) != 0 ? 0x344d73u : matches.Contains(i) ? 0x69551cu : c.Bg;
            if (bg != settings.Colors.Bg) dc.DrawRectangle(Brush(bg), null, rect);
        }
        TerminalImages.Draw(dc, frame.Images.Where(p => p.Info.Z >= int.MinValue / 2 && p.Info.Z < 0), cellWidth, cellHeight, Engine.CellWidthPixels, Engine.CellHeightPixels);
        for (int i = 0; i < frame.Cells.Length; i++)
        {
            var c = frame.Cells[i]; int x = i % cols, y = i / cols;
            var rect = new Rect(10 + x * cellWidth, 8 + y * cellHeight, cellWidth + .2, cellHeight);
            if (c.Length == 0 || (c.Flags & 16) != 0) continue;
            uint fg = c.Fg;
            var key = (frame.Text[i], fg, c.Flags & 3);
            if (!glyphs.TryGetValue(key, out var glyph)) { if (glyphs.Count > 8192) glyphs.Clear(); glyph = MakeText(key.Item1, fg, c.Flags); glyphs[key] = glyph; }
            if ((c.Flags & 64) != 0) dc.PushOpacity(.55);
            dc.DrawText(glyph, new Point(rect.X, rect.Y));
            if ((c.Flags & 4) != 0) dc.DrawLine(new Pen(Brush(fg), 1), new Point(rect.X, rect.Bottom - 2), new Point(rect.Right, rect.Bottom - 2));
            if ((c.Flags & 8) != 0) dc.DrawLine(new Pen(Brush(fg), 1), new Point(rect.X, rect.Y + cellHeight / 2), new Point(rect.Right, rect.Y + cellHeight / 2));
            if ((c.Flags & 64) != 0) dc.Pop();
        }
        TerminalImages.Draw(dc, frame.Images.Where(p => p.Info.Z >= 0), cellWidth, cellHeight, Engine.CellWidthPixels, Engine.CellHeightPixels);
        if (frame.CursorStyle != 0)
        {
            var r = new Rect(10 + frame.CursorX * cellWidth, 8 + frame.CursorY * cellHeight, cellWidth, cellHeight);
            if (!IsKeyboardFocusWithin || frame.CursorStyle == 4) dc.DrawRectangle(null, new Pen(Brush(settings.Colors.Fg), 1), r);
            else if (frame.CursorStyle == 1) dc.DrawRectangle(Brush(settings.Colors.Fg), null, new Rect(r.X, r.Y, 2, r.Height));
            else if (frame.CursorStyle == 3) dc.DrawRectangle(Brush(settings.Colors.Fg), null, new Rect(r.X, r.Bottom - 2, r.Width, 2));
            else { dc.PushOpacity(.45); dc.DrawRectangle(Brush(settings.Colors.Fg), null, r); dc.Pop(); }
        }
    }
    public int Find(string query, bool next = false) { find = query; int count = Engine.Find(query, next); Interlocked.Exchange(ref dirty, 1); return count; }
    public bool CopySelection()
    {
        var text = Engine.Text(true); if (text.Length == 0) return false;
        try { Clipboard.SetText(text); } catch (COMException) { } return true;
    }
    public void Paste()
    {
        if (!Clipboard.ContainsText()) return;
        string text = Clipboard.GetText();
        if (settings.ConfirmMultilinePaste && (text.Contains('\n') || text.Contains('\r')) && Dialogs.Show("Paste multiple lines into the shell? These lines may execute commands.", "Paste", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        // Use terminal's mode-aware bracketed paste via the native mode query.
        text = text.Replace("\x1b", "").Replace("\r\n", "\r").Replace("\n", "\r");
        Console.Send(Engine.Paste(text));
    }
    void Send(byte[] b) { if (b.Length == 0) return; Engine.Select(-1, 0, 0, 0); Engine.Scroll(0); Console.Send(b); }
    static int Mods() => ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 1 : 0) | ((Keyboard.Modifiers & ModifierKeys.Control) != 0 ? 2 : 0) | ((Keyboard.Modifiers & ModifierKeys.Alt) != 0 ? 4 : 0) | ((Keyboard.Modifiers & ModifierKeys.Windows) != 0 ? 8 : 0);
    void OnKeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.C) { CopySelection(); e.Handled = true; return; }
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.A) { Engine.SelectAll(); e.Handled = true; return; }
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.V) { Paste(); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Shift && key == Key.Insert) { Paste(); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Shift && (key == Key.PageUp || key == Key.PageDown)) { Engine.Scroll(key == Key.PageUp ? -Engine.Rows : Engine.Rows); e.Handled = true; return; }
        var stroke = windowsKeyboard.Describe(e);
        if (composing || e.Key == Key.ImeProcessed) { keyboard.Down(stroke, e.IsRepeat, true); return; }
        if (stroke.Modifier) { Console.Send(keyboard.Down(stroke, e.IsRepeat, false)); return; }
        bool special = key is Key.Enter or Key.Back or Key.Tab or Key.Escape or Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Insert or Key.Delete or Key.Clear || (key >= Key.F1 && key <= Key.F24);
        // AltGr text goes through TextInput, rather than being encoded as Ctrl+Alt.
        bool shortcut = (Keyboard.Modifiers & ModifierKeys.Control) != 0 && !stroke.AltGr;
        bool nonText = key is Key.CapsLock or Key.NumLock or Key.Scroll or Key.Pause or Key.PrintScreen or Key.Apps;
        bool immediate = special || shortcut || nonText;
        Send(keyboard.Down(stroke, e.IsRepeat, !immediate, shortcut ? stroke.LayoutText : ""));
        if (immediate) e.Handled = true;
    }
    public void Dispose() { if (disposed) return; disposed = true; timer.Stop(); windowsKeyboard.Dispose(); Console.Dispose(); Engine.Dispose(); }
}
