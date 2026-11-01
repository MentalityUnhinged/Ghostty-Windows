using System.Runtime.InteropServices;
using System.Text;
using System.IO;

namespace GhosttyWindows;

// One lock owns every access to a libghostty instance, including frame buffers.
sealed partial class Engine : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] internal struct Cell { public uint Fg, Bg, Flags, Offset, Length; }
    internal record Frame(Cell[] Cells, string[] Text, int CursorX, int CursorY, int CursorStyle, TerminalImages.Placement[] Images) { internal bool CursorBlink { get; init; } }
    readonly object gate = new();
    IntPtr handle;
    readonly Dictionary<ulong, System.Windows.Media.Imaging.BitmapSource> imageCache = new();
    readonly TerminalImages.HoldSnapshot holdSnapshot;
    TerminalImages.Placement[] heldImages = Array.Empty<TerminalImages.Placement>();
    Exception? imageSnapshotError;
    static Engine() { TerminalImages.Initialize(); }
    public int Cols { get; private set; } = 100;
    public int Rows { get; private set; } = 30;
    public int CellWidthPixels { get; private set; } = 9;
    public int CellHeightPixels { get; private set; } = 20;
    public event Action<byte[]>? Reply;
    public event Action? Changed;
    public Engine()
    {
        handle = gw_new(Cols, Rows); if (handle == IntPtr.Zero) throw new Exception("libghostty terminal allocation failed");
        var identity = Encoding.ASCII.GetBytes(HostIdentity.Text);
        if (gw_host_identity(handle, identity, identity.Length) != 0) { gw_free(handle); handle = IntPtr.Zero; throw new InvalidOperationException("Could not configure terminal identity"); }
        holdSnapshot = _ => { try { heldImages = TerminalImages.Capture(handle, imageCache); } catch (Exception e) { imageSnapshotError = e; } };
        TerminalImages.gw_hold_snapshot(handle, holdSnapshot, IntPtr.Zero);
        if (gw_resize(handle, Cols, Rows, 9, 20) != 0) { gw_free(handle); handle = IntPtr.Zero; throw new InvalidOperationException("Could not initialize terminal geometry"); }
    }
    public void Feed(byte[] bytes, int count)
    {
        if (count < 0 || count > bytes.Length) throw new ArgumentOutOfRangeException(nameof(count));
        List<byte[]> replies = new();
        lock (gate)
        {
            if (handle == IntPtr.Zero) return;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            gw_write(handle, bytes, count);
            outputBytes += count; feedCalls++; lastFeedMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            DrainReplies(replies);
        }
        foreach (var reply in replies) Reply?.Invoke(reply);
        Changed?.Invoke();
    }
    public void Feed(string text) { var b = Encoding.UTF8.GetBytes(text); Feed(b, b.Length); }
    public bool Held { get { lock (gate) return handle != IntPtr.Zero && gw_held(handle) != 0; } }
    public string Link(int x, int y) { lock (gate) return handle == IntPtr.Zero ? "" : NativeString((b, n) => gw_link(handle, x, y, b, n), "hyperlink"); }
    public void Resize(int cols, int rows, int cw, int ch)
    {
        lock (gate) { if (handle == IntPtr.Zero) return; if (gw_resize(handle, cols, rows, cw, ch) != 0) throw new Exception("Terminal resize failed"); Cols = cols; Rows = rows; CellWidthPixels = cw; CellHeightPixels = ch; }
    }
    void DrainReplies(List<byte[]> replies) { var buffer = new byte[8192]; int n; while ((n = gw_replies(handle, buffer, buffer.Length)) > 0) replies.Add(buffer[..n]); }
    public void Theme(uint fg, uint bg)
    {
        List<byte[]> replies = new();
        lock (gate)
        {
            if (handle == IntPtr.Zero) return;
            if (gw_theme(handle, fg, bg) != 0 || gw_host_scheme(handle, TextContrast.Light(bg) < .5 ? 1 : 0) != 0) throw new InvalidOperationException("Could not apply terminal colors");
            DrainReplies(replies);
        }
        foreach (var reply in replies) Reply?.Invoke(reply); Changed?.Invoke();
    }
    public void Scroll(int delta) { lock (gate) { if (handle != IntPtr.Zero) gw_scroll(handle, delta); } Changed?.Invoke(); }
    public void ScrollEdge(bool bottom) { lock (gate) { if (handle != IntPtr.Zero) gw_scroll_edge(handle, bottom ? 1 : 0); } Changed?.Invoke(); }
    public void Reset(bool full)
    {
        lock (gate) { if (handle == IntPtr.Zero) return; gw_reset_terminal(handle, full ? 1 : 0); if (full) { imageCache.Clear(); heldImages = Array.Empty<TerminalImages.Placement>(); imageSnapshotError = null; } }
        Changed?.Invoke();
    }
    public void Select(int x1, int y1, int x2, int y2) { lock (gate) { if (handle != IntPtr.Zero) gw_select(handle, x1, y1, x2, y2); } Changed?.Invoke(); }
    public void ClearSelection(bool preserveClickSequence = false) { lock(gate){if(handle!=IntPtr.Zero&&gw_clear_selection(handle,preserveClickSequence?1:0)!=0)throw new InvalidOperationException("Could not clear selection");} Changed?.Invoke(); }
    public void SelectAll() { lock (gate) { if (handle != IntPtr.Zero && gw_select_all(handle) < 0) throw new InvalidOperationException("Select all failed"); } Changed?.Invoke(); }
    public bool HasSelection { get { lock (gate) { if (handle == IntPtr.Zero) return false; int result = gw_has_selection(handle); if (result < 0) throw new InvalidOperationException("Could not query selection"); return result != 0; } } }
    public void Gesture(int action, int x, int y, double px, double py, bool rectangle = false, ulong time = 0)
    {
        lock (gate) { if (handle != IntPtr.Zero && gw_gesture(handle, action, x, y, px, py, CellWidthPixels, Rows * CellHeightPixels, rectangle ? 1 : 0, time) != 0) throw new InvalidOperationException("Selection gesture failed"); }
        Changed?.Invoke();
    }
    string NativeString(Func<byte[]?, int, int> read, string name)
    {
        int length = read(null, 0);
        if (length < 0 || length > 128 * 1024 * 1024) throw new InvalidOperationException("Could not read native " + name);
        if (length == 0) return "";
        var bytes = new byte[length]; int actual = read(bytes, bytes.Length);
        if (actual < 0 || actual > bytes.Length) throw new InvalidOperationException("Native " + name + " changed size during read");
        return Encoding.UTF8.GetString(bytes, 0, actual);
    }
    public string Title { get { lock (gate) return handle == IntPtr.Zero ? "Closed" : NativeString((b, n) => gw_title(handle, b, n), "title"); } }
    public string Pwd { get { lock (gate) return handle == IntPtr.Zero ? "" : NativeString((b, n) => gw_pwd(handle, b, n), "working directory"); } }
    internal void SelectionOptions(string? boundaries, int milliseconds, double distance)
    {
        uint[] points = boundaries?.EnumerateRunes().Select(r => (uint)r.Value).ToArray() ?? Array.Empty<uint>();
        lock (gate) { if (handle != IntPtr.Zero && gw_selection_options(handle, points, points.Length, boundaries == null ? 1 : 0, milliseconds, distance) != 0) throw new InvalidOperationException("Could not configure selection gestures"); }
    }
    public string Text(bool selection = false) => ReadText(selection ? 1 : 0);
    public string ScreenText() => ReadText(2);
    public string StyledText() => ReadText(0,2);
    internal (string Plain, string Html) SelectionClipboard(bool trim) { lock (gate) return (ReadText(1, 0, trim), ReadText(1, 2, trim)); }
    internal string FormatText(int region, int format, bool trim = true) => ReadText(region, format, trim);
    internal enum NavigationTarget { Row, Prompt, SelectionStart, SelectionEnd }
    internal enum SelectionAdjustment { Left, Right, Up, Down, Home, End, PageUp, PageDown, BeginningOfLine, EndOfLine }
    public void Navigate(NavigationTarget target, ulong row = 0, int delta = 0)
    {
        lock(gate){if(handle!=IntPtr.Zero&&gw_navigate(handle,(int)target,row,delta)!=0)throw new InvalidOperationException("Terminal navigation failed");} Changed?.Invoke();
    }
    public void AdjustSelection(SelectionAdjustment adjustment)
    {
        lock(gate){if(handle!=IntPtr.Zero&&gw_adjust_selection(handle,(int)adjustment)!=0)throw new InvalidOperationException("Selection adjustment failed");} Changed?.Invoke();
    }
    public void Limits(ulong? history, ulong? images)
    {
        lock(gate){if(handle==IntPtr.Zero)return;if(gw_limits(handle,history??0,images??ulong.MaxValue,history==null?1:0)!=0)throw new InvalidOperationException("Terminal limit update failed");if(images==0){imageCache.Clear();heldImages=Array.Empty<TerminalImages.Placement>();}} Changed?.Invoke();
    }
    string ReadText(int region, int format = 0, bool trim = true)
    {
        lock (gate)
        {
            if (handle == IntPtr.Zero) return "";
            int n = gw_text(handle, region, format, trim ? 1 : 0, null, 0);
            if (n < 0) throw new NativeOperationException("Terminal export", n);
            if (n > 128 * 1024 * 1024) throw new IOException("Terminal export exceeds the 128 MiB managed capacity.");
            if (n == 0) return "";
            byte[] b = new byte[n]; int actual = gw_text(handle, region, format, trim ? 1 : 0, b, b.Length);
            if (actual < 0 || actual > n) throw new Exception("Terminal export failed");
            return Encoding.UTF8.GetString(b, 0, actual);
        }
    }
    public byte[] Key(int vk, int mods, int action, string text = "", int scan = 0, uint unshifted = 0, int consumed = 0, bool composing = false)
    {
        lock (gate)
        {
            if (handle == IntPtr.Zero) return Array.Empty<byte>();
            var b = new byte[4096]; var utf8 = Encoding.UTF8.GetBytes(text);
            int Encode() => gw_key(handle, vk, scan, mods, action, unshifted, consumed, composing ? 1 : 0, utf8, utf8.Length, b, b.Length);
            int n = Encode();
            if (n > b.Length) { b = new byte[n]; n = Encode(); }
            if (n < 0 || n > b.Length) throw new InvalidOperationException("Ghostty key encoding failed");
            return b[..n];
        }
    }
    public string Paste(string text) { lock (gate) return handle != IntPtr.Zero && gw_bracketed(handle) != 0 ? "\x1b[200~" + text + "\x1b[201~" : text; }
    public int Find(string query, bool next) { lock (gate) { if (handle == IntPtr.Zero) return 0; var b = Encoding.UTF8.GetBytes(query); return gw_find(handle, b, b.Length, next ? 1 : 0); } }
    public byte[] Focus(bool gained) { lock (gate) return handle != IntPtr.Zero && gw_focus(handle, gained ? 1 : 0) != 0 ? Encoding.UTF8.GetBytes(gained ? "\x1b[I" : "\x1b[O") : Array.Empty<byte>(); }
    public byte[]? Mouse(int action, int button, int mods, float x, float y, int cw, int ch, bool pressed)
    {
        lock (gate) { if (handle == IntPtr.Zero) return null; var b = new byte[256]; int n = gw_mouse(handle, action, button, mods, x, y, cw, ch, pressed ? 1 : 0, b, b.Length); return n < 0 ? null : b[..n]; }
    }
    public Frame Capture()
    {
        lock (gate)
        {
            if (handle == IntPtr.Zero) return new(Array.Empty<Cell>(), Array.Empty<string>(), 0, 0, 0, Array.Empty<TerminalImages.Placement>());
            if (imageSnapshotError != null) throw new InvalidOperationException("Unable to snapshot synchronized image state", imageSnapshotError);
            int n = gw_frame(handle, out var p, out var text, out var x, out var y, out var cursor);
            if (n < 0) throw new Exception("libghostty render snapshot failed");
            var cells = new Cell[n]; var strings = new string[n];
            unsafe { new ReadOnlySpan<Cell>((void*)p, n).CopyTo(cells); }
            for (int i = 0; i < n; i++) strings[i] = cells[i].Length == 0 ? "" : Marshal.PtrToStringUTF8(IntPtr.Add(text, checked((int)cells[i].Offset)), checked((int)cells[i].Length)) ?? "";
            bool held = gw_held(handle) != 0;
            var images = held ? heldImages : TerminalImages.Capture(handle, imageCache);
            if (!held) heldImages = Array.Empty<TerminalImages.Placement>();
            return new(cells, strings, x, y, cursor & 255, images) { CursorBlink = (cursor & 256) != 0 };
        }
    }
    public void Dispose() { lock (gate) { if (handle != IntPtr.Zero) { gw_free(handle); handle = IntPtr.Zero; imageCache.Clear(); heldImages = Array.Empty<TerminalImages.Placement>(); } } GC.KeepAlive(holdSnapshot); }
    const string Dll = "ghostty-win.dll";
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_selection_options(IntPtr h, uint[] points, int count, int useDefault, int milliseconds, double distance);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_clear_selection(IntPtr h,int preserveClickSequence);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void gw_scroll_edge(IntPtr h, int bottom);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void gw_reset_terminal(IntPtr h, int full);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_select_all(IntPtr h);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_has_selection(IntPtr h);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_gesture(IntPtr h, int action, int x, int y, double px, double py, int cw, int height, int rectangle, ulong time);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_pwd(IntPtr h, byte[]? b, int cap);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_link(IntPtr h, int x, int y, byte[]? b, int cap);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_held(IntPtr h);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_focus(IntPtr h, int gained);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_mouse(IntPtr h, int action, int button, int mods, float x, float y, int cw, int ch, int pressed, byte[] b, int cap);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_bracketed(IntPtr h);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_find(IntPtr h, byte[] text, int len, int next);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr gw_new(int cols, int rows);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void gw_free(IntPtr h);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void gw_write(IntPtr h, byte[] b, int n);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_resize(IntPtr h, int c, int r, int cw, int ch);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_theme(IntPtr h, uint fg, uint bg);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_frame(IntPtr h, out IntPtr cells, out IntPtr text, out int x, out int y, out int cursor);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void gw_scroll(IntPtr h, int delta);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_title(IntPtr h, byte[]? b, int cap);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void gw_select(IntPtr h, int x1, int y1, int x2, int y2);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_text(IntPtr h, int selection, int format, int trim, byte[]? b, int cap);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_navigate(IntPtr h, int operation, ulong row, int delta);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_adjust_selection(IntPtr h, int adjustment);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_limits(IntPtr h, ulong history, ulong images, int unlimitedHistory);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_key(IntPtr h, int vk, int scan, int mods, int action, uint unshifted, int consumed, int composing, byte[] text, int len, byte[] b, int cap);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_replies(IntPtr h, byte[] b, int cap);
}
