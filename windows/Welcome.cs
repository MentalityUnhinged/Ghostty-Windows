using System.Text;

namespace GhosttyWindows;

static class Welcome
{
    static readonly string[] Ghost =
    {
        "     .----.",
        "   .'      '.",
        "  /          \\",
        " |   >   --   |",
        " |            |",
        " |            |",
        " |            |",
        " '._/ \\__/ \\_.'"
    };
    static readonly Dictionary<char, string[]> Letters = new()
    {
        ['G'] = new[] { "  ____ ", " / ___|", "| |  _ ", "| |_| |", " \\____|" },
        ['H'] = new[] { " _   _ ", "| | | |", "| |_| |", "|  _  |", "|_| |_|" },
        ['O'] = new[] { "  ___  ", " / _ \\ ", "| | | |", "| |_| |", " \\___/ " },
        ['S'] = new[] { " ____  ", "/ ___| ", "\\___ \\ ", " ___) |", "|____/ " },
        ['T'] = new[] { " _____ ", "|_   _|", "  | |  ", "  | |  ", "  |_|  " },
        ['Y'] = new[] { "__   __", "\\ \\ / /", " \\ V / ", "  | |  ", "  |_|  " }
    };
    static readonly string[] Wordmark = Enumerable.Range(0, 5).Select(row => string.Join(" ", "GHOSTTY".Select(letter => Letters[letter][row]))).ToArray();
    static readonly string[] Full = Ghost.Select((line, row) => (line.PadRight(18) + "   " + (row is >= 1 and <= 5 ? Wordmark[row - 1] : row == 7 ? " W I N D O W S   P R E V I E W" : "")).TrimEnd()).ToArray();
    static readonly string[] Compact = Ghost.Concat(new[] { " Ghostty Windows" }).ToArray();
    public static string Render(int columns)
    {
        if (columns < 16) return "";
        var lines = columns >= Full.Max(s => s.Length) + 1 ? Full : Compact; int width = lines.Max(s => s.Length);
        var output = new StringBuilder("\r\n");
        foreach (string line in lines)
        {
            for (int x = 0; x < line.Length; x += 12)
            {
                double t = width <= 12 ? 0 : (double)x / (width - 1);
                int r = (int)Math.Round(72 + 116 * t), g = (int)Math.Round(214 - 94 * t), b = (int)Math.Round(240 + 15 * t);
                output.Append($"\x1b[38;2;{r};{g};{b}m"); output.Append(line.AsSpan(x, Math.Min(12, line.Length - x)));
            }
            output.Append("\x1b[0m\r\n");
        }
        return output.Append("\r\n").ToString();
    }
    public static string Command(string profile, int columns, bool enabled)
    {
        string command = Settings.Profiles[profile], banner = enabled ? Render(columns) : "";
        if (banner.Length == 0) return command;
        if (profile == "WSL")
        {
            string escaped = banner.Replace("\\", "\\\\").Replace("\x1b", "\\033").Replace("\r", "\\r").Replace("\n", "\\n");
            string script = "printf '%b' '" + escaped.Replace("'", "'\\''") + "'; exec \"${SHELL:-/bin/sh}\" -l";
            return command + " --exec /bin/sh -c " + QuoteWindows(script);
        }
        // Print inside the actual console session, after its initialization.
        // Enable VT output before writing colors; no profile files are changed.
        string scriptPs = "Add-Type 'using System;using System.Runtime.InteropServices;public static class GhosttyWelcomeConsole{[DllImport(\"kernel32.dll\")]public static extern IntPtr GetStdHandle(int n);[DllImport(\"kernel32.dll\")]public static extern bool GetConsoleMode(IntPtr h,out uint m);[DllImport(\"kernel32.dll\")]public static extern bool SetConsoleMode(IntPtr h,uint m);}';" +
            "$h=[GhosttyWelcomeConsole]::GetStdHandle(-11);[uint32]$m=0;if([GhosttyWelcomeConsole]::GetConsoleMode($h,[ref]$m)){[GhosttyWelcomeConsole]::SetConsoleMode($h,($m -bor 4))|Out-Null};" +
            "[Console]::Write([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + Convert.ToBase64String(Encoding.UTF8.GetBytes(banner)) + "')))";
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(scriptPs));
        if (profile == "PowerShell") return command + " -NoExit -EncodedCommand " + encoded;
        return command + " /s /k \"" + Settings.Profiles["PowerShell"] + " -NoProfile -NonInteractive -EncodedCommand " + encoded + "\"";
    }
    static string QuoteWindows(string value)
    {
        var output = new StringBuilder("\""); int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            output.Append('\\', c == '"' ? slashes * 2 + 1 : slashes); output.Append(c); slashes = 0;
        }
        output.Append('\\', slashes * 2); return output.Append('"').ToString();
    }
}
