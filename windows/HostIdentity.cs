using System.Runtime.InteropServices;

namespace GhosttyWindows;

static class HostIdentity
{
    internal static string Version => typeof(HostIdentity).Assembly.GetName().Version!.ToString(3);
    internal static string Text => "ghostty-windows " + Version;
}

sealed partial class Engine
{
    [DllImport("ghostty-win.dll", CallingConvention = CallingConvention.Cdecl)] static extern int gw_host_identity(IntPtr handle, byte[] value, int count);
    [DllImport("ghostty-win.dll", CallingConvention = CallingConvention.Cdecl)] static extern int gw_host_scheme(IntPtr handle, int dark);
}
