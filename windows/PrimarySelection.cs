using System.Text;

namespace GhosttyWindows;

// A process-local selection clipboard; never written to disk or the Windows clipboard.
static class PrimarySelection
{
    internal const int MaxBytes = 8 * 1024 * 1024;
    static readonly object gate = new();
    static string? text;
    internal static bool HasValue { get { lock (gate) return text != null; } }
    internal static string? Read() { lock (gate) return text; }
    internal static bool Store(string value)
    {
        if (Encoding.UTF8.GetByteCount(value) > MaxBytes) return false;
        lock (gate) text = value; return true;
    }
    internal static void Clear() { lock (gate) text = null; }
}
