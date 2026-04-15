using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using NrgOverlay.Sim.Contracts;
using Svg;

namespace NrgOverlay.Overlays;

internal readonly record struct FlagRaster(byte[] Pixels, int Width, int Height);

internal static class FlagIconStore
{
    private const int RasterWidth = 64;
    private const int RasterHeight = 48; // 4:3 ratio

    private static readonly ConcurrentDictionary<string, FlagRaster?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly string? FlagDirectoryPath = FindFlagDirectory();

    public static bool TryGetRaster(string? countryCode, out FlagRaster raster)
    {
        raster = default;

        var iso2 = CountryCodeResolver.NormalizeIso2Code(countryCode);
        if (iso2.Length != 2) return false;

        var cached = Cache.GetOrAdd(iso2, LoadRasterForIso2);
        if (cached is null) return false;

        raster = cached.Value;
        return true;
    }

    private static FlagRaster? LoadRasterForIso2(string iso2)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(FlagDirectoryPath))
                return null;

            var path = Path.Combine(FlagDirectoryPath, iso2.ToLowerInvariant() + ".svg");
            if (!File.Exists(path)) return null;

            var doc = SvgDocument.Open(path);
            using var bmp = new Bitmap(RasterWidth, RasterHeight, PixelFormat.Format32bppPArgb);
            using var rendered = doc.Draw();
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.CompositingMode = CompositingMode.SourceOver;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(rendered, 0, 0, RasterWidth, RasterHeight);
            }

            var pixels = CopyPArgbPixels(bmp);
            return new FlagRaster(pixels, bmp.Width, bmp.Height);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] CopyPArgbPixels(Bitmap source)
    {
        var rect = new Rectangle(0, 0, source.Width, source.Height);
        var data = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var raw = new byte[source.Width * source.Height * 4];
            for (int y = 0; y < source.Height; y++)
            {
                var src = data.Scan0 + y * data.Stride;
                var dst = y * source.Width * 4;
                System.Runtime.InteropServices.Marshal.Copy(src, raw, dst, source.Width * 4);
            }
            return raw;
        }
        finally
        {
            source.UnlockBits(data);
        }
    }

    private static string? FindFlagDirectory()
    {
        var direct = Path.Combine(AppContext.BaseDirectory, "Assets", "flags", "4x3");
        if (Directory.Exists(direct))
            return direct;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "NrgOverlay.Overlays", "Assets", "flags", "4x3");
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }
}

