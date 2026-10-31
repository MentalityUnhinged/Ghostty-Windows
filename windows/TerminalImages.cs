using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GhosttyWindows;

// Pixel buffers belong to Ghostty; only frozen managed snapshots outlive the Engine lock.
static class TerminalImages
{
    internal const int StorageLimit = 64 * 1024 * 1024;
    const string Dll = "ghostty-win.dll";
    [StructLayout(LayoutKind.Sequential)] internal struct NativeImage
    {
        public uint Width, Height, Format;
        public int Z, Col, Row;
        public uint PixelWidth, PixelHeight, OffsetX, OffsetY, SourceX, SourceY, SourceWidth, SourceHeight;
        public ulong Generation;
        public IntPtr Pixels;
        public nuint Length;
        public uint ImageId, PlacementId;
    }
    [StructLayout(LayoutKind.Sequential)] struct SysImage { public uint Width, Height; public IntPtr Data; public nuint DataLength; }
    internal record Placement(BitmapSource Bitmap, NativeImage Info);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int DecodePng(IntPtr allocator, IntPtr data, nuint length, ref SysImage image);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void HoldSnapshot(IntPtr userdata);
    // Process-global callback lifetime matches the native library lifetime.
    static readonly DecodePng Decoder = Decode;
    public static void Initialize()
    {
        if (Marshal.SizeOf<NativeImage>() != 88) throw new PlatformNotSupportedException("Unexpected image adapter ABI");
        gw_initialize(Decoder);
    }
    static int Decode(IntPtr allocator, IntPtr pointer, nuint length, ref SysImage image)
    {
        IntPtr pixels = IntPtr.Zero;
        int decodedLength = 0;
        try
        {
            if (length < 24 || length > StorageLimit) return 0;
            var bytes = new byte[(int)length]; Marshal.Copy(pointer, bytes, 0, bytes.Length);
            var rgba = DecodeToRgba(bytes, out int width, out int height);
            decodedLength = rgba.Length;
            pixels = gw_image_alloc(allocator, (nuint)rgba.Length);
            if (pixels == IntPtr.Zero) return 0;
            Marshal.Copy(rgba, 0, pixels, rgba.Length);
            image = new SysImage { Width = (uint)width, Height = (uint)height, Data = pixels, DataLength = (nuint)rgba.Length };
            pixels = IntPtr.Zero; // Native allocator takes ownership only on success.
            return 1;
        }
        catch { return 0; } // Exceptions must never cross the unmanaged callback boundary.
        finally { if (pixels != IntPtr.Zero) gw_image_free(allocator, pixels, (nuint)decodedLength); }
    }
    internal static byte[] DecodeToRgba(byte[] bytes, out int width, out int height)
    {
        if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ||
            !bytes.AsSpan(12,4).SequenceEqual("IHDR"u8)) throw new InvalidDataException("Invalid PNG header");
        uint w = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4));
        uint h = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
        if (w == 0 || h == 0 || (ulong)w * h > StorageLimit / 4) throw new InvalidDataException("Decoded PNG exceeds terminal image storage");
        width = checked((int)w); height = checked((int)h);
        using var input = new MemoryStream(bytes, false);
        var decoder = new PngBitmapDecoder(input, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        if (frame.PixelWidth != width || frame.PixelHeight != height) throw new InvalidDataException("PNG dimensions disagree with its header");
        var bgra = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var rgba = new byte[checked(width * height * 4)];
        bgra.CopyPixels(rgba, width * 4, 0);
        for (int i = 0; i < rgba.Length; i += 4) (rgba[i], rgba[i + 2]) = (rgba[i + 2], rgba[i]);
        return rgba;
    }
    internal static Placement[] Capture(IntPtr terminal, Dictionary<ulong, BitmapSource> cache)
    {
        var result = new List<Placement>(); var used = new HashSet<ulong>();
        if (gw_images_begin(terminal) != 0)
        {
            while (gw_images_next(terminal, out var image) != 0)
            {
                if (!cache.TryGetValue(image.Generation, out var bitmap))
                {
                    int components = image.Format switch { 0 => 3, 1 => 4, 3 => 2, 4 => 1, _ => throw new InvalidDataException("Unsupported decoded Kitty pixel format") };
                    ulong length = (ulong)image.Width * image.Height * (uint)components;
                    if (length != (ulong)image.Length || length > StorageLimit || image.Width == 0 || image.Height == 0 || image.Pixels == IntPtr.Zero)
                        throw new InvalidDataException("Invalid Kitty pixel buffer");
                    // Native storage bounds input bytes; expanding gray to BGRA needs at most 4x.
                    ulong pixelCount = (ulong)image.Width * image.Height;
                    var source = new byte[(int)length]; Marshal.Copy(image.Pixels, source, 0, source.Length);
                    var bgra = new byte[(int)pixelCount * 4];
                    for (int dst = 0, src = 0; dst < bgra.Length; dst += 4, src += components)
                    {
                        if (components >= 3) { bgra[dst] = source[src + 2]; bgra[dst + 1] = source[src + 1]; bgra[dst + 2] = source[src]; bgra[dst + 3] = components == 4 ? source[src + 3] : (byte)255; }
                        else { bgra[dst] = bgra[dst + 1] = bgra[dst + 2] = source[src]; bgra[dst + 3] = components == 2 ? source[src + 1] : (byte)255; }
                    }
                    bitmap = BitmapSource.Create(checked((int)image.Width), checked((int)image.Height), 96, 96, PixelFormats.Bgra32, null, bgra, checked((int)image.Width * 4));
                    bitmap.Freeze(); cache.Add(image.Generation, bitmap);
                }
                used.Add(image.Generation); result.Add(new Placement(bitmap, image));
            }
        }
        foreach (ulong key in cache.Keys.Where(k => !used.Contains(k)).ToArray()) cache.Remove(key);
        // Equal-z placements use image/placement identity for deterministic order.
        return result.OrderBy(p => p.Info.Z).ThenBy(p => p.Info.ImageId).ThenBy(p => p.Info.PlacementId).ToArray();
    }
    internal static void Draw(DrawingContext dc, IEnumerable<Placement> images, double cellWidth, double cellHeight, int cellWidthPixels = 0, int cellHeightPixels = 0)
    {
        foreach (var image in images)
        {
            var p = image.Info;
            if (p.SourceWidth == 0 || p.SourceHeight == 0 || p.PixelWidth == 0 || p.PixelHeight == 0) continue;
            // Geometry is computed in native cell pixels; the frontend draws in WPF DIPs.
            double scaleX = cellWidth / (cellWidthPixels > 0 ? cellWidthPixels : Math.Ceiling(cellWidth)), scaleY = cellHeight / (cellHeightPixels > 0 ? cellHeightPixels : Math.Ceiling(cellHeight));
            var target = new Rect(10 + p.Col * cellWidth + p.OffsetX * scaleX, 8 + p.Row * cellHeight + p.OffsetY * scaleY, p.PixelWidth * scaleX, p.PixelHeight * scaleY);
            var crop = new Int32Rect(checked((int)p.SourceX), checked((int)p.SourceY), checked((int)p.SourceWidth), checked((int)p.SourceHeight));
            var source = new CroppedBitmap(image.Bitmap, crop); source.Freeze();
            dc.DrawImage(source, target);
        }
    }
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void gw_initialize(DecodePng decoder);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr gw_image_alloc(IntPtr allocator, nuint length);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void gw_image_free(IntPtr allocator, IntPtr p, nuint length);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern void gw_hold_snapshot(IntPtr terminal, HoldSnapshot callback, IntPtr userdata);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_images_begin(IntPtr terminal);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int gw_images_next(IntPtr terminal, out NativeImage image);
}
