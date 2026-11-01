using System.Runtime.InteropServices;

namespace GhosttyWindows;

static class NativeAbi
{
    [StructLayout(LayoutKind.Sequential)] internal struct Descriptor { public uint Version, CellSize, ImageSize, SystemImageSize, PointerSize, Capabilities; }
    internal const uint Version = 2;
    internal const uint RequiredCapabilities = 0xff;
    static readonly Lazy<Descriptor> descriptor = new(Read);
    internal static Descriptor Current => descriptor.Value;
    internal static void Ensure() => _ = Current;
    internal static void Validate(Descriptor value)
    {
        if (value.Version != Version || value.CellSize != Marshal.SizeOf<Engine.Cell>() || value.ImageSize != Marshal.SizeOf<TerminalImages.NativeImage>() || value.SystemImageSize != TerminalImages.SystemImageSize || value.PointerSize != IntPtr.Size || (value.Capabilities & RequiredCapabilities) != RequiredCapabilities)
            throw new PlatformNotSupportedException("Ghostty native adapter ABI mismatch. Replace the EXE, managed DLL and ghostty-win.dll together from the same Windows build.");
    }
    static Descriptor Read()
    {
        try { if (gw_abi(out var value, (uint)Marshal.SizeOf<Descriptor>()) != 0) throw new PlatformNotSupportedException("Native adapter could not describe its ABI."); Validate(value); return value; }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { throw new PlatformNotSupportedException("Ghostty native adapter is missing, from an older build, or has the wrong architecture. Install the complete matching Windows build.", e); }
    }
    [DllImport("ghostty-win.dll", CallingConvention = CallingConvention.Cdecl)] static extern int gw_abi(out Descriptor value, uint size);
}
