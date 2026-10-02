using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Renders brand/icon.svg (transcribed as geometry) to the app's image assets.
//
//   dotnet run --project scripts/IconGen -- [output.ico]      the app icon
//   dotnet run --project scripts/IconGen -- --msix <dir>      the Microsoft Store
//                                                             tile assets
//
// The MSIX assets all come from the same geometry, so the package cannot drift
// from the brand (docs/AGENTS.md rule 7).

namespace IconGen;

internal static class Program
{
    private static readonly Color ForgeInk = Color.FromRgb(0x23, 0x40, 0x8E);

    // From brand/icon.svg: the white "W" and the two pen-nib slits on the tile.
    private const string WPathData =
        "M12 18 L26 18 L33 50 L40 30 L48 30 L55 50 L62 18 L76 18 L60 70 L55 80 L50 70 L44 52 L38 70 L33 80 L28 70 Z";
    private const string SlitPathData = "M33 79 L33 66 M55 79 L55 66";

    private static readonly int[] Sizes = [16, 20, 24, 32, 48, 64, 256];

    /// <summary>The tiles a packaged Wordwright needs, and the scales MSIX can
    /// ask for them at.</summary>
    private static readonly (string Name, int Width, int Height)[] Tiles =
    [
        ("Square44x44Logo", 44, 44),
        ("Square150x150Logo", 150, 150),
        ("Wide310x150Logo", 310, 150),
        ("StoreLogo", 50, 50),
    ];

    private static readonly int[] Scales = [100, 150, 200, 400];

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--msix")
        {
            WriteMsixAssets(args[1]);
            return 0;
        }

        var outputPath = args.Length > 0 ? args[0] : Path.Combine("brand", "icon.ico");

        // Small sizes are written as 32bpp BMP (DIB) frames, not PNG: the shell's
        // icon extractor cannot read PNG-compressed entries at taskbar and
        // Explorer sizes and falls back to a generic placeholder (so the taskbar
        // button and shortcuts lose the mark). PNG is used only for 256, the one
        // size Windows documents PNG frames for.
        var frames = Sizes
            .Select(size => size >= 256 ? RenderPng(size) : RenderDib(size))
            .ToArray();

        using (var stream = File.Create(outputPath))
        using (var writer = new BinaryWriter(stream))
        {
            // ICONDIR
            writer.Write((ushort)0);          // reserved
            writer.Write((ushort)1);          // type: icon
            writer.Write((ushort)Sizes.Length);

            var offset = 6 + 16 * Sizes.Length;
            for (var i = 0; i < Sizes.Length; i++)
            {
                // ICONDIRENTRY (0 means 256)
                var dimension = Sizes[i] >= 256 ? (byte)0 : (byte)Sizes[i];
                writer.Write(dimension);      // width
                writer.Write(dimension);      // height
                writer.Write((byte)0);        // palette
                writer.Write((byte)0);        // reserved
                writer.Write((ushort)1);      // colour planes
                writer.Write((ushort)32);     // bits per pixel
                writer.Write((uint)frames[i].Length);
                writer.Write((uint)offset);
                offset += frames[i].Length;
            }

            foreach (var frame in frames)
            {
                writer.Write(frame);
            }
        }

        Console.WriteLine($"Wrote {Sizes.Length} frames (BMP up to 64, PNG at 256) to {Path.GetFullPath(outputPath)}");
        return 0;
    }

    /// <summary>Writes the store tiles, one file per tile and scale, as MSIX
    /// looks them up (<c>Square150x150Logo.scale-200.png</c>).</summary>
    private static void WriteMsixAssets(string directory)
    {
        Directory.CreateDirectory(directory);

        foreach (var (name, width, height) in Tiles)
        {
            foreach (var scale in Scales)
            {
                var pixelsWide = width * scale / 100;
                var pixelsHigh = height * scale / 100;

                // The square tiles are the icon edge to edge; the wide tile has
                // room around it, so the icon is centred at two thirds of the
                // height, as the Store's own tiles do.
                var bytes = RenderTile(pixelsWide, pixelsHigh);
                File.WriteAllBytes(Path.Combine(directory, $"{name}.scale-{scale}.png"), bytes);
                Console.WriteLine($"Wrote {name}.scale-{scale}.png ({pixelsWide}x{pixelsHigh})");

                // The manifest names each tile without a scale, and makeappx
                // insists that file exists as well as any scaled versions.
                if (scale == Scales[0])
                {
                    File.WriteAllBytes(Path.Combine(directory, $"{name}.png"), bytes);
                }
            }
        }
    }

    private static byte[] RenderTile(int width, int height)
    {
        var side = Math.Min(width, height);
        var icon = (int)(side * (width == height ? 1.0 : 0.66));
        var offsetX = (width - icon) / 2.0;
        var offsetY = (height - icon) / 2.0;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(offsetX, offsetY));
            DrawIcon(dc, icon);
            dc.Pop();
        }

        return Encode(visual, width, height);
    }

    private static void DrawIcon(DrawingContext dc, int size)
    {
        const double grid = 88;
        dc.PushTransform(new ScaleTransform(size / grid, size / grid));
        dc.DrawGeometry(
            new SolidColorBrush(ForgeInk),
            pen: null,
            new RectangleGeometry(new Rect(0, 0, grid, grid), 20, 20));
        dc.DrawGeometry(Brushes.White, pen: null, Geometry.Parse(WPathData));
        dc.DrawGeometry(
            brush: null,
            new Pen(new SolidColorBrush(ForgeInk), 2)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            },
            Geometry.Parse(SlitPathData));
        dc.Pop();
    }

    private static byte[] Encode(DrawingVisual visual, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static byte[] RenderPng(int size)
    {
        var bitmap = RenderBitmap(size);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>Renders one icon frame and packs it as a 32bpp BMP entry: a
    /// BITMAPINFOHEADER with the height doubled (the colour bitmap plus an AND
    /// mask), the BGRA pixels bottom-up, then a zeroed 1bpp mask — the alpha
    /// channel, not the mask, carries the rounded tile.</summary>
    private static byte[] RenderDib(int size)
    {
        // Pbgra32 is premultiplied; an icon frame wants straight BGRA.
        var converted = new FormatConvertedBitmap(RenderBitmap(size), PixelFormats.Bgra32, null, 0);

        var stride = size * 4;
        var pixels = new byte[stride * size];
        converted.CopyPixels(pixels, stride, 0);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(40);                 // biSize
        writer.Write(size);               // biWidth
        writer.Write(size * 2);           // biHeight (XOR bitmap + AND mask)
        writer.Write((ushort)1);          // biPlanes
        writer.Write((ushort)32);         // biBitCount
        writer.Write(0);                  // biCompression: BI_RGB
        writer.Write(stride * size);      // biSizeImage
        writer.Write(0);                  // biXPelsPerMeter
        writer.Write(0);                  // biYPelsPerMeter
        writer.Write(0);                  // biClrUsed
        writer.Write(0);                  // biClrImportant

        for (var y = size - 1; y >= 0; y--)
        {
            writer.Write(pixels, y * stride, stride);
        }

        writer.Write(new byte[((size + 31) / 32) * 4 * size]);

        return stream.ToArray();
    }

    private static RenderTargetBitmap RenderBitmap(int size)
    {
        const double grid = 88;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size / grid, size / grid));
            dc.DrawGeometry(
                new SolidColorBrush(ForgeInk),
                pen: null,
                new RectangleGeometry(new Rect(0, 0, grid, grid), 20, 20));
            dc.DrawGeometry(Brushes.White, pen: null, Geometry.Parse(WPathData));
            dc.DrawGeometry(
                brush: null,
                new Pen(new SolidColorBrush(ForgeInk), 2)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                },
                Geometry.Parse(SlitPathData));
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }
}