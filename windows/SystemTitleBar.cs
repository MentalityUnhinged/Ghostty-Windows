using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace GhosttyWindows;

// Own the native frame separately from the user's terminal color theme.
sealed class SystemTitleBar
{
    readonly Window window;
    readonly Func<bool> prefersDark;
    HwndSource? source;
    bool applying;
    SystemTitleBar(Window window, Func<bool>? preference)
    {
        this.window = window; prefersDark = preference ?? ReadDarkPreference;
        window.SourceInitialized += Initialized;
        window.Closed += Closed;
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) Initialized(window, EventArgs.Empty);
    }
    internal static void Attach(Window window, Func<bool>? preference = null) => _ = new SystemTitleBar(window, preference);
    static bool ReadDarkPreference()
    {
        if (SystemParameters.HighContrast) return false;
        try { return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0; }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or System.IO.IOException) { return false; }
    }
    void Initialized(object? sender, EventArgs args)
    {
        source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        source?.AddHook(Messages); Apply();
    }
    IntPtr Messages(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        // Windows broadcasts these when personalization, contrast or DWM settings change.
        if (message is 0x001A or 0x0015 or 0x031A or 0x031E) Apply();
        return IntPtr.Zero;
    }
    void Apply()
    {
        if (source == null || applying) return;
        applying = true;
        try
        {
            int dark = prefersDark() ? 1 : 0;
            // Attribute 19 is the Windows 10 1809 compatibility fallback.
            if (DwmSetWindowAttribute(source.Handle, 20, ref dark, sizeof(int)) < 0)
                DwmSetWindowAttribute(source.Handle, 19, ref dark, sizeof(int));
        }
        finally { applying = false; }
    }
    void Closed(object? sender, EventArgs args)
    {
        source?.RemoveHook(Messages); source = null;
        window.SourceInitialized -= Initialized; window.Closed -= Closed;
    }
    internal static void VerifyFollowingChanges()
    {
        var window = new Window { Title = "Native title bar verification", ShowInTaskbar = false };
        bool dark = true; Attach(window, () => dark);
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        try
        {
            int attribute = 20;
            if (DwmGetWindowAttribute(hwnd, attribute, out int actual, sizeof(int)) < 0) attribute = 19;
            if (DwmGetWindowAttribute(hwnd, attribute, out actual, sizeof(int)) < 0 || actual != 1) throw new Exception("Dark title bar was not applied");
            dark = false; SendMessage(hwnd, 0x001A, IntPtr.Zero, IntPtr.Zero);
            if (DwmGetWindowAttribute(hwnd, attribute, out actual, sizeof(int)) < 0 || actual != 0) throw new Exception("Title bar did not switch to light on settings change");
            dark = true; SendMessage(hwnd, 0x031A, IntPtr.Zero, IntPtr.Zero);
            if (DwmGetWindowAttribute(hwnd, attribute, out actual, sizeof(int)) < 0 || actual != 1) throw new Exception("Title bar did not switch back to dark on theme change");
        }
        finally { window.Close(); }
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam);
}
