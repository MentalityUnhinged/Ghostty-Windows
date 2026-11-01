using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace GhosttyWindows;

// Own the HWND backdrop and restore an opaque client surface when it is unavailable.
sealed class WindowBackdrop
{
    readonly Window window;
    readonly Settings settings;
    readonly Action<bool> changed;
    HwndSource? source;
    bool applying;
    public WindowBackdrop(Window window, Settings settings, Action<bool> changed)
    {
        this.window = window; this.settings = settings; this.changed = changed;
        window.SourceInitialized += Initialize;
        settings.Changed += Apply;
        window.Closed += Close;
    }
    void Initialize(object? sender, EventArgs args)
    {
        source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        source?.AddHook(Messages); Apply();
    }
    IntPtr Messages(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        if (message is 0x001A or 0x0015 or 0x031A or 0x031E) Apply();
        return IntPtr.Zero;
    }
    static bool TransparencyAllowed()
    {
        try { return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1) is int value && value != 0; }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException) { return false; }
    }
    void Apply()
    {
        if (source == null || applying) return;
        applying = true;
        try
        {
            bool enabled = settings.BackgroundEffect == "Acrylic" && !SystemParameters.HighContrast
                && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621)
                && TransparencyAllowed();
            int material = enabled ? 3 : 1; // DWMSBT_TRANSIENTWINDOW / DWMSBT_NONE
            enabled &= DwmSetWindowAttribute(source.Handle, 38, ref material, sizeof(int)) >= 0;
            var margins = new Margins { Left = enabled ? -1 : 0 };
            if (DwmExtendFrameIntoClientArea(source.Handle, ref margins) < 0) enabled = false;
            if (!enabled)
            {
                material = 1; DwmSetWindowAttribute(source.Handle, 38, ref material, sizeof(int));
                margins = new(); DwmExtendFrameIntoClientArea(source.Handle, ref margins);
            }
            if (source.CompositionTarget != null) source.CompositionTarget.BackgroundColor = enabled ? Colors.Transparent : Colors.Black;
            changed(enabled);
        }
        finally { applying = false; }
    }
    void Close(object? sender, EventArgs args)
    {
        settings.Changed -= Apply; source?.RemoveHook(Messages); source = null;
        window.SourceInitialized -= Initialize; window.Closed -= Close;
    }
    [StructLayout(LayoutKind.Sequential)] struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
}
