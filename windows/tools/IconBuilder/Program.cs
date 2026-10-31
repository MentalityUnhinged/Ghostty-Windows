using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Legacy DIB entries for small shell icons, PNG for 256px. WIC decodes PNGs.
string root = Path.GetFullPath(args[0]);
string destination = Path.GetFullPath(args[1]);
int[] sizes = {16,32,64,128,256};
var entries = new List<byte[]>();
foreach (int size in sizes)
{
    string png = Path.Combine(root, "images", "gnome", size + ".png");
    if (size == 256) { entries.Add(File.ReadAllBytes(png)); continue; }
    var source = new PngBitmapDecoder(new Uri(png), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
    if (source.PixelWidth != size || source.PixelHeight != size) throw new InvalidDataException("Unexpected icon artwork dimensions");
    var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
    int stride = size * 4, maskStride = ((size + 31) / 32) * 4;
    byte[] pixels = new byte[stride * size]; converted.CopyPixels(pixels, stride, 0);
    using var buffer = new MemoryStream(); using var dib = new BinaryWriter(buffer);
    dib.Write(40); dib.Write(size); dib.Write(size * 2); dib.Write((ushort)1); dib.Write((ushort)32);
    dib.Write(0); dib.Write(stride * size + maskStride * size); dib.Write(0); dib.Write(0); dib.Write(0); dib.Write(0);
    for (int y = size - 1; y >= 0; y--) dib.Write(pixels, y * stride, stride);
    for (int y = size - 1; y >= 0; y--)
    {
        byte[] mask = new byte[maskStride];
        for (int x = 0; x < size; x++) if (pixels[y * stride + x * 4 + 3] == 0) mask[x / 8] |= (byte)(128 >> (x % 8));
        dib.Write(mask);
    }
    entries.Add(buffer.ToArray());
}
Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
using var output = new BinaryWriter(File.Create(destination));
output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)sizes.Length);
int offset = 6 + sizes.Length * 16;
for (int i = 0; i < sizes.Length; i++)
{
    output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
    output.Write((byte)0); output.Write((byte)0); output.Write((ushort)1); output.Write((ushort)32);
    output.Write(entries[i].Length); output.Write(offset); offset += entries[i].Length;
}
foreach (byte[] entry in entries) output.Write(entry);
