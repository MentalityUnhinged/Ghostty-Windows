using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GhosttyWindows;

// Each pane owns one frozen, bounded decoded image. No file handle is retained.
sealed class BackgroundImage
{
    BitmapSource? bitmap;
    string? loadedPath;
    long loadedLength, loadedStamp;
    internal string? Error { get; private set; }
    internal void Load(string path)
    {
        if (path.Length == 0) { loadedPath = null; bitmap = null; Error = null; return; }
        try
        {
            var metadata = new FileInfo(path);
            if (loadedPath == path && loadedLength == metadata.Length && loadedStamp == metadata.LastWriteTimeUtc.Ticks && bitmap != null) return;
            bitmap = null; Error = null;
            using var file = File.OpenRead(path);
            if (file.Length > 64 * 1024 * 1024) throw new InvalidDataException("Background image exceeds 64 MiB.");
            var decoder = BitmapDecoder.Create(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            if ((long)frame.PixelWidth * frame.PixelHeight > 32 * 1024 * 1024) throw new InvalidDataException("Background image exceeds 32 million pixels.");
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var copy = new WriteableBitmap(converted); copy.Freeze(); bitmap = copy; loadedPath = path; loadedLength = metadata.Length; loadedStamp = metadata.LastWriteTimeUtc.Ticks;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or System.Runtime.InteropServices.COMException or ArgumentException or FileFormatException or OverflowException) { bitmap = null; loadedPath = null; Error = e.Message; }
    }
    internal void Draw(DrawingContext dc, Size size, Settings settings)
    {
        if (bitmap == null || size.Width <= 0 || size.Height <= 0) return;
        var brush = new ImageBrush(bitmap) { Opacity = settings.BackgroundImageOpacity, AlignmentX = settings.BackgroundImageAlignment switch { "Left" => AlignmentX.Left, "Right" => AlignmentX.Right, _ => AlignmentX.Center }, AlignmentY = settings.BackgroundImageVerticalAlignment switch { "Top" => AlignmentY.Top, "Bottom" => AlignmentY.Bottom, _ => AlignmentY.Center } };
        brush.Stretch = settings.BackgroundImageFit switch { "Cover" => Stretch.UniformToFill, "Contain" => Stretch.Uniform, "Stretch" => Stretch.Fill, _ => Stretch.None };
        if (settings.BackgroundImageRepeat)
        {
            brush.TileMode = TileMode.Tile; brush.Stretch = Stretch.Fill;
            brush.ViewportUnits = BrushMappingMode.Absolute; brush.Viewport = new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
        }
        dc.DrawRectangle(brush, null, new Rect(size));
    }
}
