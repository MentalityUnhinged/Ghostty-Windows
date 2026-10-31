using System.Runtime.InteropServices;
using System.Text;

namespace GhosttyWindows;

// One lock owns every access to a libghostty instance, including frame buffers.
sealed class Engine : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] internal struct Cell { public uint Fg, Bg, Flags, Offset, Length; }
    internal record Frame(Cell[] Cells, string[] Text, int CursorX, int CursorY, int CursorStyle, TerminalImages.Placement[] Images);
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
        holdSnapshot = _ => { try { heldImages = TerminalImages.Capture(handle, imageCache); } catch (Exception e) { imageSnapshotError = e; } };
        TerminalImages.gw_hold_snapshot(handle, holdSnapshot, IntPtr.Zero);
        gw_resize(handle, Cols, Rows, 9, 20);
    }
    public void Feed(byte[] bytes, int count)
    {
        List<byte[]> replies = new();
        lock (gate)
        {
            if (handle == IntPtr.Zero) return;
            gw_write(handle, bytes, count);
            var buffer = new byte[8192]; int n;
            while ((n = gw_replies(handle, buffer, buffer.Length)) > 0) replies.Add(buffer[..n]);
        }
        foreach (var reply in replies) Reply?.Invoke(reply);
        Changed?.Invoke();
    }
    public void Feed(string text) { var b = Encoding.UTF8.GetBytes(text); Feed(b, b.Length); }
    public bool Held { get { lock (gate) return handle != IntPtr.Zero && gw_held(handle) != 0; } }
    public string Link(int x, int y) { lock (gate) { if (handle == IntPtr.Zero) return ""; int n = gw_link(handle, x, y, null, 0); if (n == 0) return ""; var b = new byte[n]; gw_link(handle, x, y, b, b.Length); return Encoding.UTF8.GetString(b); } }
    public void Resize(int cols, int rows, int cw, int ch)
    {
        lock (gate) { if (handle == IntPtr.Zero) return; if (gw_resize(handle, cols, rows, cw, ch) != 0) throw new Exception("Terminal resize failed"); Cols = cols; Rows = rows; CellWidthPixels = cw; CellHeightPixels = ch; }
    }
    public void Theme(uint fg, uint bg) { lock (gate) { if (handle != IntPtr.Zero) gw_theme(handle, fg, bg); } Changed?.Invoke(); }
    public void Scroll(int delta) { lock (gate) { if (handle != IntPtr.Zero) gw_scroll(handle, delta); } Changed?.Invoke(); }
    public void Select(int x1, int y1, int x2, int y2) { lock (gate) { if (handle != IntPtr.Zero) gw_select(handle, x1, y1, x2, y2); } Changed?.Invoke(); }
    public void SelectAll() { lock (gate) { if (handle != IntPtr.Zero && gw_select_all(handle) < 0) throw new InvalidOperationException("Select all failed"); } Changed?.Invoke(); }
    public void Gesture(int action, int x, int y, double px, double py, bool rectangle = false, ulong time = 0)
    {
        lock (gate) { if (handle != IntPtr.Zero && gw_gesture(handle, action, x, y, px, py, CellWidthPixels, Rows * CellHeightPixels, rectangle ? 1 : 0, time) != 0) throw new InvalidOperationException("Selection gesture failed"); }
        Changed?.Invoke();
    }
    public string Title { get { lock (gate) { if (handle == IntPtr.Zero) return "Closed"; var b = new byte[8192]; return Encoding.UTF8.GetString(b, 0, gw_title(handle, b, b.Length)); } } }
    public string Pwd { get { lock (gate) { if (handle == IntPtr.Zero) return ""; int n = gw_pwd(handle, null, 0); if (n < 0) throw new InvalidOperationException("Terminal working directory exceeds the managed string limit"); var b = new byte[n]; gw_pwd(handle, b, b.Length); return Encoding.UTF8.GetString(b); } } }
    public string Text(bool selection = false)
    {
        lock (gate)
        {
            if (handle == IntPtr.Zero) return "";
            int n = gw_text(handle, selection ? 1 : 0, null, 0);
            if (n < 0 || n > 128 * 1024 * 1024) throw new Exception("Terminal export exceeds 128 MiB");
            if (n == 0) return "";
            byte[] b = new byte[n]; int actual = gw_text(handle, selection ? 1 : 0, b, b.Length);
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
            return new(cells, strings, x, y, cursor, images);
        }
    }
    public void Dispose() { lock (gate) { if (handle != IntPtr.Zero) { gw_free(handle); handle = IntPtr.Zero; imageCache.Clear(); heldImages = Array.Empty<TerminalImages.Placement>(); } } GC.KeepAlive(holdSnapshot); }
    const string Dll = "ghostty-win.dll";
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_select_all(IntPtr h);
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
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void gw_theme(IntPtr h, uint fg, uint bg);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_frame(IntPtr h, out IntPtr cells, out IntPtr text, out int x, out int y, out int cursor);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void gw_scroll(IntPtr h, int delta);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_title(IntPtr h, byte[] b, int cap);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void gw_select(IntPtr h, int x1, int y1, int x2, int y2);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_text(IntPtr h, int selection, byte[]? b, int cap);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_key(IntPtr h, int vk, int scan, int mods, int action, uint unshifted, int consumed, int composing, byte[] text, int len, byte[] b, int cap);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_replies(IntPtr h, byte[] b, int cap);
}
