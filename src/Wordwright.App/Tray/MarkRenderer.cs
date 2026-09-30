using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Wordwright.App.Tray;

/// <summary>
/// Renders the brand mark as bitmaps for the tray icon. The "W" geometry is
/// transcribed from brand/tray-light-taskbar.svg and brand/tray-dark-taskbar.svg
/// (same path, different fill).
/// </summary>
internal static class MarkRenderer
{
    // The "W" path on the 88px grid used by all files in brand/.
    private const string WPathData =
        "M12 18 L26 18 L33 50 L40 30 L48 30 L55 50 L62 18 L76 18 L60 70 L55 80 L50 70 L44 52 L38 70 L33 80 L28 70 Z";

    // The tray SVGs use viewBox "8 14 72 70".
    private const double ViewBoxX = 8;
    private const double ViewBoxY = 14;
    private const double Grid = 88;

    // Tray icon sizes across the common Windows DPI scales, plus 48/64 headroom.
    private static readonly int[] TraySizes = [16, 20, 24, 32, 48, 64];

    /// <summary>Tray-light-taskbar.svg uses Anvil; tray-dark-taskbar.svg uses white.</summary>
    internal static readonly Color LightTaskbarGlyph = Color.FromRgb(0x1A, 0x20, 0x30);
    internal static readonly Color DarkTaskbarGlyph = Color.FromRgb(0xFF, 0xFF, 0xFF);

    /// <summary>
    /// Renders the glyph to a multi-size ICO (PNG-compressed entries), ready for
    /// TaskbarIcon.Icon. H.NotifyIcon's IconSource path only accepts images
    /// loaded from a file URI, so a dynamic glyph must go through Icon instead.
    /// </summary>
    internal static byte[] RenderTrayIconIco(Color glyphColor)
    {
        var frames = TraySizes.Select(size => RenderGlyphPng(size, glyphColor)).ToArray();

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // ICONDIR
        writer.Write((ushort)0);              // reserved
        writer.Write((ushort)1);              // type: icon
        writer.Write((ushort)TraySizes.Length);

        var offset = 6 + 16 * TraySizes.Length;
        for (var i = 0; i < TraySizes.Length; i++)
        {
            // ICONDIRENTRY
            writer.Write((byte)TraySizes[i]); // width
            writer.Write((byte)TraySizes[i]); // height
            writer.Write((byte)0);            // palette
            writer.Write((byte)0);            // reserved
            writer.Write((ushort)1);          // colour planes
            writer.Write((ushort)32);         // bits per pixel
            writer.Write((uint)frames[i].Length);
            writer.Write((uint)offset);
            offset += frames[i].Length;
        }

        foreach (var frame in frames)
        {
            writer.Write(frame);
        }

        return stream.ToArray();
    }

    private static byte[] RenderGlyphPng(int size, Color glyphColor)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // Scale the 88px brand grid, then move into the tray SVG's viewBox,
            // so the glyph has the same size and margins as the source files.
            dc.PushTransform(new ScaleTransform(size / Grid, size / Grid));
            dc.PushTransform(new TranslateTransform(-ViewBoxX, -ViewBoxY));
            dc.DrawGeometry(new SolidColorBrush(glyphColor), null, Geometry.Parse(WPathData));
            dc.Pop();
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new MemoryStream();
        encoder.Save(png);
        return png.ToArray();
    }
}