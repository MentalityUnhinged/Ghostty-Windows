using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace GhosttyWindows;

static class KeyboardTests
{
    static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);
    static void Check(bool value, string name) { if (!value) throw new Exception("Keyboard test failed: " + name); SelfTest.Passed.Add(name); }
    static void Equal(byte[] bytes, string expected, string name)
    {
        if (Text(bytes) != expected) throw new Exception($"Keyboard test failed: {name}; expected {Convert.ToHexString(Encoding.UTF8.GetBytes(expected))}, got {Convert.ToHexString(bytes)}");
        SelfTest.Passed.Add(name);
    }
    public static async Task Run()
    {
        using var engine = new Engine(); var keys = new TerminalKeyboard(engine);
        var a = new WindowsKeyboard.Stroke('A', 0x1e, 0, 'a');
        Equal(keys.Down(a, false, true), "", "Printable key waits for the Windows text commit");
        Equal(keys.Text("a"), "a", "Legacy printable key encodes once");
        Equal(keys.Text("a"), "", "Duplicate text callback cannot duplicate a key press");
        keys.Down(a, true, true); Equal(keys.Text("a"), "a", "Legacy repeat preserves committed text");
        Equal(keys.Up(a), "", "Legacy shell receives no release sequences");
        Equal(keys.Up(a), "", "Unforwarded shortcut release is ignored");
        Equal(engine.Key(0x7c, 0, 1), "\x1b[25~", "F13 legacy sequence");
        Equal(engine.Key(0x87, 0, 1), "\x1b[45~", "F24 legacy sequence");
        Equal(engine.Key(13, 0, 1, scan: 0xe01c), "\r", "Keypad Enter remains compatible with a legacy shell");
        engine.Feed("\x1b=");
        Equal(engine.Key(0x61, 0, 1, scan: 0x4f), "1", "Default Ghostty mode keeps the keypad numeric");
        engine.Feed("\x1b[?1035l");
        Equal(engine.Key(0x61, 0, 1, scan: 0x4f), "\x1bOq", "Application keypad digit uses physical keypad identity");
        Equal(engine.Key(13, 0, 1, scan: 0xe01c), "\x1bOM", "Application keypad Enter differs from main Enter");
        Equal(engine.Key(35, 0, 1, scan: 0x4f), "\x1b[F", "NumLock-off keypad retains End navigation");
        Equal(engine.Key(35, 0, 1, scan: 0xe04f), "\x1b[F", "Extended navigation End stays a navigation key");
        engine.Feed("\x1b>\x1b[>11u"); // disambiguation, events, all keys
        Equal(keys.Down(a, false, true), "", "Kitty printable key waits for layout text");
        Equal(keys.Text("a"), "\x1b[97u", "Kitty report-all encodes a printable press");
        keys.Down(a, true, true); Equal(keys.Text("a"), "\x1b[97;1:2u", "Kitty repeat reports event type 2");
        Equal(keys.Up(a), "\x1b[97;1:3u", "Kitty release reports event type 3");
        var shift = new WindowsKeyboard.Stroke(0xa0, 0x2a, 1, 0);
        Equal(keys.Down(shift, false, false), "\x1b[57441;2u", "Kitty left modifier press");
        Equal(keys.Up(shift with { Mods = 0 }), "\x1b[57441;1:3u", "Kitty left modifier release");
        Equal(engine.Key(13, 0, 1, scan: 0xe01c), "\x1b[57414u", "Kitty keypad Enter has its own key code");
        keys.Down(a, false, true); keys.Text("a");
        Equal(keys.ReleaseAll(), "\x1b[97;1:3u", "Focus loss releases forwarded held keys");
        Equal(keys.Up(a), "", "Physical release after focus loss cannot release twice");
        keys.Down(a, false, true);
        Equal(keys.ReleaseAll(), "", "Pending dead key cannot produce an unmatched release");
        engine.Feed("\x1b[<u\x1b[>31u");
        var shifted = a with { Mods = 1 };
        keys.Down(shifted, false, true);
        Equal(keys.Text("A"), "\x1b[97:65;2;65u", "Kitty shifted alternate and associated text");
        keys.Up(shifted);
        // Logical Q on the physical US A location: layout identity must remain Q,
        // while the optional base-layout alternate is the physical A key.
        Equal(engine.Key('Q', 0, 1, "q", 0x1e, 'q'), "\x1b[113::97;;113u", "Kitty physical and layout key identities remain separate");
        var keypad = new WindowsKeyboard.Stroke(0x61, 0x4f, 32, '1');
        keys.Down(keypad, false, true);
        Equal(keys.Text("1"), "\x1b[57400;129;49u", "Kitty keypad carries associated text and NumLock state"); keys.Up(keypad);
        Equal(engine.Key(35, 0, 1, scan: 0x4f), "\x1b[57424u", "Kitty NumLock-off keypad End retains keypad identity");
        engine.Feed("\x1b[<u");
        var altGr = new WindowsKeyboard.Stroke('Q', 0x10, 2 | 4 | 256, 'q', true);
        keys.Down(altGr, false, true);
        Equal(keys.Text("@"), "@", "AltGr commit consumes Control and Alt without an Escape prefix");
        keys.Up(altGr);
        Equal(engine.Key('C', 2, 1), "\x03", "Reused native event resets layout and consumed modifiers");
        var ctrlShift2 = new WindowsKeyboard.Stroke('2', 3, 3, '2');
        Equal(keys.Down(ctrlShift2, false, false, "@"), "\x1b[64;5u", "Ctrl+Shift symbol uses shifted layout text instead of an accidental C0 byte"); keys.Up(ctrlShift2);
        Equal(engine.Key('A', 0, 1, "a", composing: true), "", "Native encoder suppresses composition preedit");
        Equal(engine.Key('A', 0, 1, "a"), "a", "Next native event resets composition state");
        var alt = a with { Mods = 4 };
        keys.Down(alt, false, true); Equal(keys.Text("a"), "\x1b" + "a", "Alt text preserves the legacy Escape prefix"); keys.Up(alt);
        engine.Feed("\x1b[>31u");
        keys.Down(altGr, false, true);
        Equal(keys.Text("@"), "\x1b[113;;64u", "Kitty AltGr preserves layout text without synthetic Ctrl+Alt"); keys.Up(altGr);
        Equal(keys.Text("日本語🙂", true), "日本語🙂", "IME commit preserves multi-scalar text under Kitty report-all");
        string large = new('界', 3000);
        Equal(keys.Text(large, true), large, "Committed input exceeding the old 4096-byte buffer is preserved");
        engine.Feed("\x1b[<u");
        keys.Down(a, false, true); Equal(keys.Text("a"), "a", "Popping Kitty mode restores legacy input"); keys.Up(a);
        await WindowsMessageInput();

        // A real WSL application opts into Kitty, reads raw input and prints its
        // hex bytes. This crosses both ConPTY and WSL rather than echoing text.
        using var terminal = new Engine(); using var pty = new PseudoConsole();
        var input = new TerminalKeyboard(terminal);
        byte[] expected = Encoding.ASCII.GetBytes("\x1b[97u\x1b[97;1:2u\x1b[97;1:3u");
        pty.Data += terminal.Feed; terminal.Reply += pty.Send;
        string script = $"stty raw -echo; printf '\\033[>11uKEYBOARD_READY\\r\\n'; dd bs=1 count={expected.Length} 2>/dev/null | od -An -tx1 -v; printf '\\033[<u\\r\\nKEYBOARD_DONE\\r\\n'";
        pty.Start("wsl.exe --exec /bin/sh -c \"" + script + "\"", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 100, 30);
        try
        {
            for (int i = 0; i < 200 && !terminal.Text().Contains("KEYBOARD_READY"); i++) await Task.Delay(50);
            Check(terminal.Text().Contains("KEYBOARD_READY"), "Real WSL raw-input probe negotiates Kitty mode");
            pty.Send(input.Down(a, false, true)); pty.Send(input.Text("a"));
            pty.Send(input.Down(a, true, true)); pty.Send(input.Text("a")); pty.Send(input.Up(a));
            for (int i = 0; i < 200 && !terminal.Text().Contains("KEYBOARD_DONE"); i++) await Task.Delay(50);
            string output = string.Concat(terminal.Text().Where(c => !char.IsWhiteSpace(c)));
            string hex = Convert.ToHexString(expected).ToLowerInvariant();
            Check(output.Contains(hex) && output.Contains("KEYBOARD_DONE"), "Real WSL app receives exact Kitty press, repeat and release bytes through ConPTY");
        }
        finally { pty.Dispose(); await pty.Cleanup.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
    static async Task WindowsMessageInput()
    {
        var target = new System.Windows.Controls.Border { Focusable = true };
        var window = new Window { Content = target, Width = 240, Height = 100, ShowInTaskbar = false, Title = "Keyboard message probe" };
        using var capture = new WindowsKeyboard(() => target.IsKeyboardFocusWithin);
        WindowsKeyboard.Stroke? down = null, up = null;
        target.PreviewKeyDown += (_, e) => { down = capture.Describe(e); e.Handled = true; };
        target.PreviewKeyUp += (_, e) => { up = capture.Describe(e); e.Handled = true; };
        try
        {
            window.Show(); window.Activate(); target.Focus(); await Task.Delay(60);
            var hwnd = new WindowInteropHelper(window).Handle;
            // Window-local messages exercise the WPF/Win32 boundary without
            // changing the user's keyboard state or injecting desktop input.
            const long scan = (0x1cL << 16) | (1L << 24) | 1;
            if (!PostMessageW(hwnd, 0x100, (IntPtr)13, (IntPtr)scan)) throw new System.ComponentModel.Win32Exception();
            for (int i = 0; i < 40 && down == null; i++) await Task.Delay(20);
            if (!PostMessageW(hwnd, 0x101, (IntPtr)13, new IntPtr(scan | (1L << 30) | (1L << 31)))) throw new System.ComponentModel.Win32Exception();
            for (int i = 0; i < 40 && up == null; i++) await Task.Delay(20);
            Check(down?.Scan == 0xe01c && up?.Scan == 0xe01c, "Real Windows message pump preserves extended keypad scan codes through WPF");
        }
        finally { window.Close(); }
    }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool PostMessageW(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
