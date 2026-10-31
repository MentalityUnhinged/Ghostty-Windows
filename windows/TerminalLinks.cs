using System.Text;
using System.Text.RegularExpressions;

namespace GhosttyWindows;

static class TerminalLinks
{
    internal record Link(string Target, int[] Cells);
    static readonly Regex Pattern = new("https?://[^\\s<>\"\\u0000-\\u001f]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    public static bool IsWeb(string target) => Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" && uri.Host.Length > 0;
    public static Link[] Detect(Engine.Frame frame, int columns)
    {
        var text = new StringBuilder(); var cells = new List<int>();
        for (int i = 0; i < frame.Cells.Length; i++)
        {
            uint flags = frame.Cells[i].Flags;
            if ((flags & (256 | 512)) == 0)
            {
                string value = (flags & 16) != 0 ? " " : frame.Text[i].Length > 0 ? frame.Text[i] : " ";
                text.Append(value); for (int j = 0; j < value.Length; j++) cells.Add(i);
            }
            if ((i + 1) % columns == 0 && (flags & 128) == 0) { text.Append('\n'); cells.Add(-1); }
        }
        var links = new List<Link>();
        foreach (Match match in Pattern.Matches(text.ToString()))
        {
            // A viewport can cut through a logical line. Do not offer a
            // destination assembled from an incomplete URL at either edge.
            if (frame.Cells.Length > 0 && (match.Index == 0 && (frame.Cells[0].Flags & 1024) != 0 || match.Index + match.Length == text.Length && (frame.Cells[^1].Flags & 128) != 0)) continue;
            string target = Trim(match.Value); if (!IsWeb(target)) continue;
            var hitCells = new HashSet<int>();
            for (int j = match.Index; j < match.Index + target.Length; j++)
            {
                int cell = cells[j]; if (cell < 0) continue; hitCells.Add(cell);
                if (cell + 1 < frame.Cells.Length && (frame.Cells[cell + 1].Flags & 256) != 0) hitCells.Add(cell + 1);
            }
            links.Add(new(target, hitCells.Order().ToArray()));
        }
        return links.ToArray();
    }
    static string Trim(string text)
    {
        int round = 0, square = 0, curly = 0;
        foreach (char c in text) { if (c == '(') round++; if (c == ')') round--; if (c == '[') square++; if (c == ']') square--; if (c == '{') curly++; if (c == '}') curly--; }
        int n = text.Length;
        while (n > 0)
        {
            char c = text[n - 1];
            if (c is '.' or ',' or ';' or '!' || c == ')' && round < 0 || c == ']' && square < 0 || c == '}' && curly < 0)
            { n--; if (c == ')') round++; if (c == ']') square++; if (c == '}') curly++; }
            else break;
        }
        return text[..n];
    }
}
