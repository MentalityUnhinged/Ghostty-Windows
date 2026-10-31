using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GhosttyWindows;

// Actual Kitty sequences flow through the compiled engine and the Windows decoder/renderer.
static class GraphicsTests
{
    static readonly byte[] Pattern = { 255,0,0,255, 0,255,0,255, 0,0,255,255, 255,255,255,128 };
    static void Check(bool value, string name) { if (!value) throw new Exception("Graphics test failed: " + name); SelfTest.Passed.Add(name); }
    internal static string Command(string parameters, byte[]? data = null) => "\x1b_G" + parameters + ";" + (data == null ? "" : Convert.ToBase64String(data)) + "\x1b\\";
    internal static byte[] Png(byte[] rgba, int width, int height)
    {
        var bgra = (byte[])rgba.Clone(); for (int i = 0; i < bgra.Length; i += 4) (bgra[i], bgra[i+2]) = (bgra[i+2], bgra[i]);
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, width * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
    }
    static byte[] Pixels(BitmapSource image)
    {
        var result = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(result, image.PixelWidth * 4, 0); return result;
    }
    public static async Task Run(string artifacts)
    {
        using var engine = new Engine(); engine.Resize(40, 20, 10, 10);
        engine.Feed(Command("a=T,f=32,s=2,v=2,i=1,p=1,c=2,r=2,q=2", Pattern));
        var image = engine.Capture().Images.Single();
        Check(image.Info.Width == 2 && image.Info.Height == 2 && Pixels(image.Bitmap).Take(4).SequenceEqual(new byte[] { 0,0,255,255 }), "Kitty RGBA pixels reach managed snapshot");
        Check(image.Info.PixelWidth == 20 && image.Info.PixelHeight == 20, "Kitty explicit cell geometry");
        var firstBitmap = image.Bitmap;
        Check(ReferenceEquals(firstBitmap, engine.Capture().Images.Single().Bitmap), "Unchanged Kitty image reuses frozen bitmap");
        var green = new byte[] { 0,255,0,255, 0,255,0,255, 0,255,0,255, 0,255,0,255 };
        engine.Feed(Command("a=t,f=32,s=2,v=2,i=1,q=2", green));
        engine.Feed(Command("a=p,i=1,p=1,c=2,r=2,q=2"));
        var replacement = engine.Capture().Images.Single();
        Check(replacement.Info.Generation != image.Info.Generation && !ReferenceEquals(firstBitmap, replacement.Bitmap) && Pixels(replacement.Bitmap)[1] == 255, "Same-ID same-size image retransmission invalidates cache");
        engine.Feed(Command("a=p,i=1,p=2,x=1,y=0,w=1,h=2,c=1,r=2,X=2,Y=3,z=-1,q=2"));
        var cropped = engine.Capture().Images.Single(p => p.Info.PlacementId == 2);
        Check(cropped.Info.SourceX == 1 && cropped.Info.SourceWidth == 1 && cropped.Info.SourceHeight == 2 && cropped.Info.OffsetX == 2 && cropped.Info.OffsetY == 3 && cropped.Info.Z == -1, "Kitty crop, offsets and z-layer geometry");
        byte[] png = Png(Pattern, 2, 2);
        // Decode on an actual thread-pool thread as ConPTY reads are not UI-thread callbacks.
        await Task.Run(() => engine.Feed(Command("a=T,f=100,i=2,p=1,c=2,r=2,q=2", png)));
        var decoded = engine.Capture().Images.Single(p => p.Info.ImageId == 2);
        Check(Pixels(decoded.Bitmap).Take(12).SequenceEqual(new byte[] {0,0,255,255, 0,255,0,255, 255,0,0,255}), "Real PNG decoder preserves RGB on background I/O thread");
        Check(Pixels(decoded.Bitmap)[15] == 128, "PNG alpha channel preserved");
        var rawRgb = new byte[] { 32,64,128 };
        engine.Feed(Command("a=T,f=24,s=1,v=1,i=3,p=1,c=1,r=1,q=2", rawRgb));
        Check(Pixels(engine.Capture().Images.Single(p => p.Info.ImageId == 3).Bitmap).SequenceEqual(new byte[] {128,64,32,255}), "Kitty RGB payload conversion");
        var compressed = new MemoryStream(); using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, true)) zlib.Write(Pattern);
        engine.Feed(Command("a=T,f=32,s=2,v=2,o=z,i=4,p=1,c=2,r=2,q=2", compressed.ToArray()));
        Check(engine.Capture().Images.Any(p => p.Info.ImageId == 4), "Kitty zlib compressed payload");
        string payload = Convert.ToBase64String(png); int split = payload.Length / 8 * 4;
        engine.Feed("\x1b_Ga=T,f=100,i=5,p=1,c=2,r=2,m=1,q=2;" + payload[..split] + "\x1b\\");
        engine.Feed("\x1b_Gm=0;" + payload[split..] + "\x1b\\");
        Check(engine.Capture().Images.Any(p => p.Info.ImageId == 5), "Kitty chunked PNG transmission");
        engine.Feed("\x1b[?1049h"); Check(engine.Capture().Images.Length == 0, "Kitty alternate screen isolation");
        engine.Feed("\x1b[?1049l"); Check(engine.Capture().Images.Any(p => p.Info.ImageId == 2), "Kitty primary screen images restored");
        engine.Feed("\x1b[?2026h" + Command("a=t,f=32,s=2,v=2,i=2,q=2", green) + Command("a=p,i=2,p=1,c=2,r=2,q=2"));
        var held = engine.Capture().Images.Single(p => p.Info.ImageId == 2);
        Check(Pixels(held.Bitmap)[2] == 255, "Synchronized output freezes image pixels at hold boundary");
        engine.Feed("\x1b[?2026l");
        Check(Pixels(engine.Capture().Images.Single(p => p.Info.ImageId == 2).Bitmap)[1] == 255, "Synchronized output publishes replacement image on release");
        engine.Feed(Command("a=d,d=I,i=2,q=2")); Check(engine.Capture().Images.All(p => p.Info.ImageId != 2), "Kitty image deletion removes placements and cached bitmap");
        engine.Feed(Command("a=T,f=100,i=99,q=2", new byte[] {1,2,3,4})); Check(engine.Capture().Images.All(p => p.Info.ImageId != 99), "Malformed PNG rejected without crashing terminal");
        var oversized = (byte[])png.Clone(); oversized[16] = 0x7f;
        bool rejected = false; try { TerminalImages.DecodeToRgba(oversized, out _, out _); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Oversized PNG dimensions rejected before decode allocation");
        // Render actual native placement metadata through the production drawing function.
        using var renderEngine = new Engine(); renderEngine.Resize(10,10,10,10);
        renderEngine.Feed(Command("a=T,f=32,s=2,v=2,i=7,c=2,r=2,q=2", Pattern));
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) TerminalImages.Draw(dc, renderEngine.Capture().Images, 10, 10);
        var screenshot = new RenderTargetBitmap(40,40,96,96,PixelFormats.Pbgra32); screenshot.Render(visual);
        var screen = Pixels(screenshot); int pos = (10 * 40 + 12) * 4;
        Check(screen[pos + 2] > 200 && screen[pos + 1] < 40, "Kitty image appears at correct rendered viewport coordinates");
        var output = new PngBitmapEncoder(); output.Frames.Add(BitmapFrame.Create(screenshot)); using (var file = File.Create(Path.Combine(artifacts,"kitty-render.png"))) output.Save(file);
        renderEngine.Feed(string.Concat(Enumerable.Repeat("\r\n", 25)));
        Check(renderEngine.Capture().Images.Length == 0, "Kitty offscreen placements leave viewport snapshot");
        using (var hidpi = new Engine())
        {
            hidpi.Resize(10,10,15,30);
            hidpi.Feed(Command("a=T,f=32,s=1,v=1,i=1,c=2,r=2,q=2", new byte[] {255,0,0,255}));
            var drawing = new DrawingVisual(); using (var dc = drawing.RenderOpen()) TerminalImages.Draw(dc, hidpi.Capture().Images, 10,20,15,30);
            var rendered = new RenderTargetBitmap(60,70,96,96,PixelFormats.Pbgra32); rendered.Render(drawing);
            var pixels = Pixels(rendered);
            Check(pixels[(10 * 60 + 28) * 4 + 2] == 255 && pixels[(10 * 60 + 32) * 4 + 3] == 0,
                "HiDPI physical image pixels map to logical cell geometry");
        }
        foreach (int z in new[] { int.MinValue / 2 - 1, -1, 1 })
        {
            using var view = new TerminalView(new Settings(), "Command Prompt", true);
            view.Measure(new Size(200,200)); view.Arrange(new Rect(0,0,200,200));
            view.Engine.Feed("\x1b[H\x1b[42m \x1b[0m\x1b[H" + Command($"a=T,f=32,s=1,v=1,i=1,c=1,r=1,z={z},q=2", new byte[] {255,0,0,255}));
            view.RefreshFrame(); view.UpdateLayout();
            var rendered = new RenderTargetBitmap(200,200,96,96,PixelFormats.Pbgra32); rendered.Render(view);
            var pixels = Pixels(rendered); int offset = (10 * 200 + 12) * 4;
            bool red = pixels[offset + 2] > 200 && pixels[offset + 1] < 40;
            Check(z < int.MinValue / 2 ? !red && pixels[offset + 1] > pixels[offset + 2] : red,
                "Production terminal rendering Kitty z layer " + z);
        }
    }
}
