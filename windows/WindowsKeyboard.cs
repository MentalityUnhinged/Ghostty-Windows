using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Interop;

namespace GhosttyWindows;

// Windows owns translation. Observe scan codes without consuming messages or
// mutating the keyboard's dead-key buffer; WPF TextInput supplies committed text.
sealed class WindowsKeyboard : IDisposable
{
    internal readonly record struct Stroke(int Vk, int Scan, int Mods, uint Unshifted, bool AltGr = false, string LayoutText = "")
    {
        public int Identity => Scan != 0 ? Scan : 0x10000 | Vk;
        public bool Modifier => Vk is 0x10 or 0x11 or 0x12 or >= 0xa0 and <= 0xa5 or 0x5b or 0x5c;
    }
    readonly Func<bool> focused;
    int messageVk, messageScan, messageId;
    public WindowsKeyboard(Func<bool> focused) { this.focused = focused; ComponentDispatcher.ThreadFilterMessage += Observe; }
    void Observe(ref MSG message, ref bool handled)
    {
        if (!focused() || message.message is not (0x100 or 0x101 or 0x104 or 0x105)) return;
        messageId = message.message; messageVk = (int)message.wParam;
        long bits = message.lParam.ToInt64();
        messageScan = (int)((bits >> 16) & 255) | ((bits & (1L << 24)) != 0 ? 0xe000 : 0);
    }
    public Stroke Describe(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;
        int vk = KeyInterop.VirtualKeyFromKey(key);
        bool down = e.RoutedEvent == Keyboard.PreviewKeyDownEvent || e.RoutedEvent == Keyboard.KeyDownEvent, observedDown = messageId is 0x100 or 0x104;
        bool sameKey = messageVk == vk || messageVk == 0x10 && vk is 0xa0 or 0xa1 || messageVk == 0x11 && vk is 0xa2 or 0xa3 || messageVk == 0x12 && vk is 0xa4 or 0xa5;
        IntPtr layout = GetKeyboardLayout(0);
        int scan = down == observedDown && sameKey ? messageScan : (int)MapVirtualKeyExW((uint)vk, 4, layout);
        var state = new byte[256]; var text = new StringBuilder(8);
        int n = ToUnicodeEx((uint)vk, (uint)(scan & 255), state, text, text.Capacity, 4, layout);
        uint unshifted = n > 0 && Rune.TryGetRuneAt(text.ToString(0, Math.Min(n, text.Length)), 0, out var rune) && !Rune.IsControl(rune) ? (uint)rune.Value : 0;
        int mods = Modifiers();
        // Ctrl shortcuts are encoded before TextInput. Supply their shifted
        // layout text without asking Windows to turn it into a C0 character.
        state[0x10] = (byte)((mods & 1) != 0 ? 0x80 : 0); state[0x14] = (byte)((mods & 16) != 0 ? 1 : 0); state[0x90] = (byte)((mods & 32) != 0 ? 1 : 0);
        text.Clear(); n = ToUnicodeEx((uint)vk, (uint)(scan & 255), state, text, text.Capacity, 4, layout);
        string layoutText = n > 0 ? text.ToString(0, Math.Min(n, text.Length)) : "";
        if (layoutText.EnumerateRunes().Any(Rune.IsControl)) layoutText = "";
        return new(vk, scan, mods, unshifted, Keyboard.IsKeyDown(Key.RightAlt) && (Keyboard.Modifiers & ModifierKeys.Control) != 0, layoutText);
    }
    public static int Modifiers()
    {
        var mods = Keyboard.Modifiers;
        return ((mods & ModifierKeys.Shift) != 0 ? 1 | (Keyboard.IsKeyDown(Key.RightShift) ? 64 : 0) : 0)
            | ((mods & ModifierKeys.Control) != 0 ? 2 | (Keyboard.IsKeyDown(Key.RightCtrl) ? 128 : 0) : 0)
            | ((mods & ModifierKeys.Alt) != 0 ? 4 | (Keyboard.IsKeyDown(Key.RightAlt) ? 256 : 0) : 0)
            | ((mods & ModifierKeys.Windows) != 0 ? 8 | (Keyboard.IsKeyDown(Key.RWin) ? 512 : 0) : 0)
            | ((GetKeyState(0x14) & 1) != 0 ? 16 : 0) | ((GetKeyState(0x90) & 1) != 0 ? 32 : 0);
    }
    public void Dispose() => ComponentDispatcher.ThreadFilterMessage -= Observe;
    [DllImport("user32.dll")] static extern short GetKeyState(int key);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);
    [DllImport("user32.dll")] static extern uint MapVirtualKeyExW(uint code, uint map, IntPtr layout);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int ToUnicodeEx(uint vk, uint scan, byte[] state, StringBuilder text, int capacity, uint flags, IntPtr layout);
}

// One pane owns each forwarded key from press through release. App shortcuts
// never enter this set, so tab changes cannot leak their releases into a shell.
sealed class TerminalKeyboard
{
    sealed class Pressed(WindowsKeyboard.Stroke stroke)
    {
        public WindowsKeyboard.Stroke Stroke = stroke;
        public int Action;
        public bool AwaitingText, Sent;
        public string Text = "";
    }
    readonly Engine engine;
    readonly Dictionary<int, Pressed> pressed = new();
    Pressed? pending;
    public TerminalKeyboard(Engine engine) { this.engine = engine; }
    byte[] Encode(Pressed key, int action, int? mods = null)
    {
        var s = key.Stroke;
        int consumed = s.AltGr && key.Text.Length > 0 ? 2 | 4 : 0;
        int effective = mods ?? s.Mods;
        // Windows represents AltGr as Ctrl+RightAlt. Those synthetic modifier
        // bits are layout translation, not a Ctrl shortcut. Ghostty's legacy
        // C0 path intentionally uses raw mods, so normalize this pair here.
        if (consumed != 0) effective &= ~(2 | 4 | 128 | 256);
        return engine.Key(s.Vk, effective, action, key.Text, s.Scan, s.Unshifted, consumed);
    }
    public byte[] Down(WindowsKeyboard.Stroke stroke, bool repeat, bool deferText, string text = "")
    {
        bool existing = pressed.TryGetValue(stroke.Identity, out var key);
        if (!existing) pressed.Add(stroke.Identity, key = new(stroke));
        key!.Stroke = stroke; key.Action = repeat || existing ? 2 : 1;
        key.AwaitingText = deferText; key.Text = text; pending = key;
        if (deferText) return Array.Empty<byte>();
        var bytes = Encode(key, key.Action); key.Sent |= bytes.Length > 0; return bytes;
    }
    public byte[] Text(string text, bool composition = false)
    {
        if (text.Length == 0) return Array.Empty<byte>();
        if (composition || pending == null)
        {
            // Ghostty's Enter+UTF8 path represents an IME commit and bypasses
            // key protocol encoding, preserving an entire multi-scalar commit.
            if (pending != null) pending.AwaitingText = false;
            return engine.Key(13, 0, 1, text);
        }
        if (!pending.AwaitingText) return Array.Empty<byte>();
        pending.AwaitingText = false; pending.Text = text;
        var bytes = Encode(pending, pending.Action); pending.Sent |= bytes.Length > 0; return bytes;
    }
    public byte[] Up(WindowsKeyboard.Stroke stroke)
    {
        if (!pressed.Remove(stroke.Identity, out var key)) return Array.Empty<byte>();
        if (pending == key) pending = null;
        return key.Sent ? Encode(key, 0, stroke.Mods) : Array.Empty<byte>();
    }
    public byte[] ReleaseAll()
    {
        var bytes = new List<byte>();
        foreach (var key in pressed.Values.Reverse()) if (key.Sent) bytes.AddRange(Encode(key, 0, 0));
        pressed.Clear(); pending = null; return bytes.ToArray();
    }
}
