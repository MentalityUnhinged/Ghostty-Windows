using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace GhosttyWindows;

sealed partial class Settings { public bool CaptureProtocolDiagnostics { get; set; } }

sealed partial class Engine
{
    bool capturingUnknown;
    long outputBytes, feedCalls; double lastFeedMilliseconds;
    internal void CaptureUnknown(bool enabled)
    {
        lock (gate)
        {
            if (handle == IntPtr.Zero || capturingUnknown == enabled) return;
            if (gw_unknown_capture(handle, enabled ? 1 : 0) != 0) throw new InvalidOperationException("Could not configure protocol diagnostics");
            capturingUnknown = enabled;
        }
    }
    internal record UnknownSample(string Kind, int CapturedBytes, bool Truncated, string? Base64);
    internal record ProtocolSnapshot(bool Enabled, ulong Total, ulong Evicted, UnknownSample[] Samples);
    internal ProtocolSnapshot UnknownSnapshot(bool payloads)
    {
        lock (gate)
        {
            if (handle == IntPtr.Zero) return new(false, 0, 0, Array.Empty<UnknownSample>());
            int count = gw_unknown_count(handle, out ulong total);
            var samples = new List<UnknownSample>(); var bytes = new byte[256];
            for (int i = 0; i < count; i++)
            {
                int length = gw_unknown_read(handle, i, out uint tag, out uint truncated, bytes, bytes.Length);
                if (length < 0 || length > bytes.Length) throw new InvalidOperationException("Could not read protocol sample");
                samples.Add(new(tag == 1 ? "OSC" : "APC", length, truncated != 0, payloads ? Convert.ToBase64String(bytes, 0, length) : null));
            }
            return new(capturingUnknown, total, total - (ulong)count, samples.ToArray());
        }
    }
    internal object Inspect(bool payloads)
    {
        lock (gate)
        {
            var frame = Capture();
            var modes = new Dictionary<string, bool?>();
            foreach (var entry in new (int Number, string Name)[] { (1,"application-cursor"), (25,"cursor-visible"), (1000,"mouse-buttons"), (1002,"mouse-drag"), (1003,"mouse-motion"), (1004,"focus"), (1006,"sgr-mouse"), (2004,"bracketed-paste"), (2026,"synchronized-output"), (2031,"color-scheme-report") })
            {
                int result = handle == IntPtr.Zero ? -1 : gw_mode_query(handle, entry.Number);
                modes.Add(entry.Name, result < 0 ? null : result != 0);
            }
            return new { performance = new { outputBytes, feedCalls, lastFeedMilliseconds }, columns = Cols, rows = Rows, cellPixels = new { width = CellWidthPixels, height = CellHeightPixels }, cursor = new { x = frame.CursorX, y = frame.CursorY, style = frame.CursorStyle, blinking = frame.CursorBlink }, render = new { cells = frame.Cells.Length, images = frame.Images.Length, synchronized = Held }, modes, unknownSequences = UnknownSnapshot(payloads) };
        }
    }
    [DllImport("ghostty-win.dll", CallingConvention = CallingConvention.Cdecl)] static extern int gw_unknown_capture(IntPtr handle, int enabled);
    [DllImport("ghostty-win.dll", CallingConvention = CallingConvention.Cdecl)] static extern int gw_unknown_count(IntPtr handle, out ulong total);
    [DllImport("ghostty-win.dll", CallingConvention = CallingConvention.Cdecl)] static extern int gw_unknown_read(IntPtr handle, int index, out uint tag, out uint truncated, byte[] buffer, int capacity);
    [DllImport("ghostty-win.dll", CallingConvention = CallingConvention.Cdecl)] static extern int gw_mode_query(IntPtr handle, int number);
}

static class DiagnosticReport
{
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true, IncludeFields = true };
    internal static object TryInspect(Engine engine, bool payloads) { try { return engine.Inspect(payloads); } catch (Exception e) when (e is not OutOfMemoryException and not AccessViolationException) { return new { unavailable = e.Message }; } }
    internal static object Collect(TerminalView? terminal, bool payloads = false)
    {
        var artifacts = new Dictionary<string, object>();
        foreach (string name in new[] { "Ghostty.Windows.exe", "Ghostty.Windows.dll", "ghostty-win.dll", "build-manifest.json" })
        {
            string path = Path.Combine(AppContext.BaseDirectory, name);
            artifacts[name] = File.Exists(path) ? new { bytes = new FileInfo(path).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() } : new { bytes = 0L, sha256 = "missing" };
        }
        object abi; try { abi = NativeAbi.Current; } catch (Exception e) { abi = new { unavailable = e.Message }; }
        return new { schema = 1, capturedUtc = DateTimeOffset.UtcNow, product = HostIdentity.Text, runtime = Environment.Version.ToString(), os = Environment.OSVersion.ToString(), architecture = RuntimeInformation.ProcessArchitecture.ToString(), settingsPath = Settings.FilePath, artifacts, abi, terminal = terminal == null ? null : new { profile = terminal.Profile, unsupportedPreferenceKeys = terminal.UnsupportedPreferenceKeys, readOnly = terminal.ReadOnly, transport = terminal.TransportDiagnostics, state = TryInspect(terminal.Engine, payloads) }, coverage = "Current pane only; no environment, settings contents, screen text or typed input. Unknown-sequence samples cover complete unsupported OSC/APC only; cancelled, malformed and known disabled protocols are excluded. Payloads omitted unless explicitly selected." };
    }
    internal static void Write(string path, TerminalView? terminal = null, bool payloads = false)
    {
        string target = Path.GetFullPath(path), temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        string text = JsonSerializer.Serialize(Collect(terminal, payloads), Json);
        try { File.WriteAllText(temp, text); if (File.Exists(target)) File.Replace(temp, target, target + ".backup", true); else File.Move(temp, target); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    internal static void Export(Window owner, TerminalView? terminal)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = "ghostty-diagnostics.json", Filter = "JSON report|*.json" };
        if (dialog.ShowDialog(owner) != true) return;
        try { Write(dialog.FileName, terminal); }
        catch (Exception e) when (e is not OutOfMemoryException and not AccessViolationException) { Dialogs.Show(e.Message, "Diagnostics export failed"); }
    }
}

static class Inspector
{
    internal static Window Create(Window owner, TerminalView terminal, IEnumerable<WindowCommand> commands)
    {
        var window = new Window { Owner = owner, Title = "Terminal inspector", Width = 760, Height = 650, MinWidth = 480, MinHeight = 320 };
        var panel = new DockPanel { Margin = new Thickness(12) }; window.Content = panel;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(buttons, Dock.Top); panel.Children.Add(buttons);
        var refresh = new Button { Content = "Refresh", Margin = new Thickness(0,0,8,8) }; buttons.Children.Add(refresh);
        var export = new Button { Content = "Export diagnostics", Margin = new Thickness(0,0,8,8) }; buttons.Children.Add(export);
        var payloads = new CheckBox { Content = "Show captured payloads (Base64)", VerticalAlignment = VerticalAlignment.Center }; buttons.Children.Add(payloads);
        var text = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new System.Windows.Media.FontFamily("Consolas") }; panel.Children.Add(text);
        void Refresh()
        {
            try { text.Text = JsonSerializer.Serialize(new { profile = terminal.Profile, unsupportedPreferenceKeys = terminal.UnsupportedPreferenceKeys, readOnly = terminal.ReadOnly, transport = terminal.TransportDiagnostics, state = DiagnosticReport.TryInspect(terminal.Engine, payloads.IsChecked == true), actions = commands.Select(c => new { c.Id, c.Display, available = c.CanRun() }).ToArray() }, DiagnosticReport.Json); }
            catch (Exception e) when (e is not OutOfMemoryException and not AccessViolationException) { text.Text = "Inspector unavailable: " + e.Message; }
        }
        refresh.Click += (_, _) => Refresh(); payloads.Click += (_, _) => Refresh(); export.Click += (_, _) => DiagnosticReport.Export(window, terminal);
        window.Loaded += (_, _) => Refresh(); return window;
    }
    internal static void Show(Window owner, TerminalView terminal, IEnumerable<WindowCommand> commands) => Create(owner, terminal, commands).Show();
}
