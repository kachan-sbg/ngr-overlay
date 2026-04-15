using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Rendering;
using NrgOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DCommon;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace NrgOverlay.Overlays;

/// <summary>
/// Relative timing tower вЂ” shows В±<c>MaxDriversShown/2</c> drivers centered on the player.
/// Columns: POS | CC | CAR# | DRIVER NAME | TYRE | STATUS | GAP | LAST
/// </summary>
public sealed class RelativeOverlay : BaseOverlay
{
    public const string OverlayId   = "Relative";
    public const string WindowTitle = "NrgOverlay \u2014 Relative";

    public static OverlayConfig DefaultConfig => new()
    {
        Id              = OverlayId,
        Enabled         = true,
        X               = 100,
        Y               = 200,
        Width           = 480,
        Height          = 310,
        Opacity         = 0.85f,
        BackgroundColor = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize        = 13f,
        ShowIRating     = false,
        ShowLicense     = false,
        MaxDriversShown = 11,   // В±5 + player
    };

    private volatile RelativeData? _latest;
    private readonly Dictionary<string, ID2D1Bitmap> _flagBitmaps = new(StringComparer.OrdinalIgnoreCase);

    // в”Ђв”Ђ Edit-mode mock data в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private static readonly RelativeData MockData = new()
    {
        Entries =
        [
            new() { Position =  1, CarNumber = "44", DriverName = "L. Hamilton",   ClubName = "Germany",         TireCompound = 1, GapToPlayerSeconds = -8.45f, LastLapTime = TimeSpan.FromSeconds(93.102) },
            new() { Position =  2, CarNumber =  "1", DriverName = "M. Verstappen", ClubName = "Netherlands",     TireCompound = 1, GapToPlayerSeconds = -5.10f, LastLapTime = TimeSpan.FromSeconds(93.388) },
            new() { Position =  3, CarNumber = "16", DriverName = "C. Leclerc",    ClubName = "France",          TireCompound = 2, GapToPlayerSeconds = -2.43f, LastLapTime = TimeSpan.FromSeconds(93.521) },
            new() { Position =  4, CarNumber = "63", DriverName = "G. Russell",    ClubName = "Great Britain",   TireCompound = 1, GapToPlayerSeconds = -1.12f, LastLapTime = TimeSpan.FromSeconds(93.887) },
            new() { Position =  5, CarNumber =  "4", DriverName = "L. Norris",     ClubName = "Great Britain",   TireCompound = 1, GapToPlayerSeconds =  0.00f, LastLapTime = TimeSpan.FromSeconds(94.521), IsPlayer = true },
            new() { Position =  6, CarNumber = "81", DriverName = "O. Piastri",    ClubName = "Australia",       TireCompound = 1, GapToPlayerSeconds = +1.34f, LastLapTime = TimeSpan.FromSeconds(94.233), IsOnPitRoad = false, IsOutLap = true },
            new() { Position =  7, CarNumber = "11", DriverName = "S. Perez",      ClubName = "Mexico",          TireCompound = 2, GapToPlayerSeconds = +2.88f, LastLapTime = TimeSpan.FromSeconds(94.102) },
            new() { Position =  8, CarNumber = "14", DriverName = "F. Alonso",     ClubName = "Spain",           TireCompound = 1, GapToPlayerSeconds = +4.21f, LastLapTime = TimeSpan.FromSeconds(95.011), IsOnPitRoad = true },
            new() { Position =  9, CarNumber = "55", DriverName = "C. Sainz",      ClubName = "Spain",           TireCompound = 2, GapToPlayerSeconds = +5.90f, LastLapTime = TimeSpan.FromSeconds(94.788) },
            new() { Position = 10, CarNumber = "18", DriverName = "L. Stroll",     ClubName = "Canada",          TireCompound = 1, GapToPlayerSeconds = +7.15f, LastLapTime = TimeSpan.FromSeconds(95.344) },
            new() { Position = 11, CarNumber = "31", DriverName = "E. Ocon",       ClubName = "France",          TireCompound = 1, GapToPlayerSeconds = +9.02f, LastLapTime = TimeSpan.FromSeconds(95.712) },
        ],
    };

    public RelativeOverlay(
        ISimDataBus bus,
        OverlayConfig config,
        ConfigStore configStore,
        AppConfig appConfig)
        : base(WindowTitle, config, bus, configStore, appConfig)
    {
        Subscribe<RelativeData>(data => _latest = data);
    }

    protected override void OnRender(ID2D1RenderTarget context, OverlayConfig config)
    {
        var data    = IsLocked ? _latest : MockData;
        var entries = data?.Entries ?? [];

        var fontSize = config.FontSize;
        var charW    = fontSize * 0.615f;
        var rowH     = fontSize + 6f;
        var pad      = 8f;
        var w        = (float)config.Width;
        var h        = (float)config.Height;

        // ----- Column widths -----
        // POS(3ch) | CC(3ch) | CAR(5ch) | NAME(variable) | TYRE(2ch) | STATUS(3ch) | GAP(7ch) | LAST(8ch)
        var posW    = 3f * charW;
        var ccW     = 3f * charW;
        var carW    = 5f * charW;
        var tyreW   = 2f * charW;
        var statW   = 3f * charW;
        var gapW    = 7f * charW;
        var lastW   = 8f * charW;
        var colGap  = charW;

        var fixedW = pad + posW + colGap
                        + ccW   + colGap
                        + carW  + colGap
                        + tyreW + colGap
                        + statW + colGap
                        + gapW  + colGap
                        + lastW + pad + 5f;

        var nameW = MathF.Max(w - fixedW, 50f);

        // ----- Column left edges -----
        float xPos  = pad;
        float xCc   = xPos  + posW  + colGap;
        float xCar  = xCc   + ccW   + colGap;
        float xName = xCar  + carW  + colGap;
        float xTyre = xName + nameW + colGap;
        float xStat = xTyre + tyreW + colGap;
        float xGap  = xStat + statW + colGap;
        float xLast = xGap  + gapW  + colGap;

        var dw       = Resources.WriteFactory;
        var fmt      = Resources.GetTextFormat("Oswald", fontSize);
        var text     = Resources.GetBrush(config.TextColor);
        var dimmed   = Resources.GetBrush(config.TextColor.R, config.TextColor.G,
                                           config.TextColor.B, config.TextColor.A * 0.45f);
        var otherText = Resources.GetBrush(config.TextColor.R, config.TextColor.G,
                                            config.TextColor.B, config.TextColor.A * 0.70f);
        var sep      = Resources.GetBrush(config.TextColor.R, config.TextColor.G,
                                           config.TextColor.B, 0.25f);
        var rowAlt   = Resources.GetBrush(1f, 1f, 1f, 0.04f);
        var pitColor = Resources.GetBrush(0.8f, 0.5f, 0.1f, 1f);   // amber for pit/outlap status
        var ccColor  = Resources.GetBrush(config.TextColor.R, config.TextColor.G,
                                           config.TextColor.B, config.TextColor.A * 0.55f);

        // ----- Header -----
        float y = pad;
        DrawR(context, dw, fmt, dimmed, "P",      xPos,  y, posW,  rowH);
        DrawL(context, dw, fmt, dimmed, "CC",     xCc,   y, ccW,   rowH);
        DrawR(context, dw, fmt, dimmed, "CAR",    xCar,  y, carW,  rowH);
        DrawL(context, dw, fmt, dimmed, "DRIVER", xName, y, nameW, rowH);
        DrawL(context, dw, fmt, dimmed, "T",      xTyre, y, tyreW, rowH);
        DrawL(context, dw, fmt, dimmed, "ST",     xStat, y, statW, rowH);
        DrawR(context, dw, fmt, dimmed, "GAP",    xGap,  y, gapW,  rowH);
        DrawR(context, dw, fmt, dimmed, "LAST",   xLast, y, lastW, rowH);

        y += rowH + 1f;
        context.DrawLine(new Vector2(pad, y), new Vector2(w - pad, y), sep, 1f);
        y += 3f;

        // ----- Select visible window centered on player -----
        var visible = SelectVisible(entries, config.MaxDriversShown);

        int rowIndex = 0;
        foreach (var entry in visible)
        {
            if (y + rowH > h - pad) break;

            var rowBrush = entry.IsPlayer ? text : otherText;

            if (entry.IsPlayer)
            {
                var hc = config.PlayerHighlightColor;
                var hl = Resources.GetBrush(hc.R, hc.G, hc.B, hc.A);
                context.FillRectangle(new Vortice.RawRectF(0, y - 1, w, y + rowH + 1), hl);
            }
            else if (rowIndex % 2 == 0)
            {
                context.FillRectangle(new Vortice.RawRectF(0, y, w, y + rowH), rowAlt);
            }

            // Multi-class indicator: 3px class-color strip on the left side.
            if (!string.IsNullOrEmpty(entry.CarClass))
            {
                var cc = entry.ClassColor;
                var strip = Resources.GetBrush(cc.R, cc.G, cc.B, 0.95f);
                context.FillRectangle(new Vortice.RawRectF(1f, y, 4f, y + rowH), strip);
            }

            if (entry.IsPlayer)
                DrawL(context, dw, fmt, text, "\u25ba", pad - charW, y, charW + 2f, rowH);

            // POS
            DrawR(context, dw, fmt, rowBrush, entry.Position > 0 ? entry.Position.ToString() : "-",
                  xPos, y, posW, rowH);

            // CC вЂ” flag resolved from country code fallback chain
            if (!TryDrawFlag(context, entry.CountryCode, xCc, y, ccW, rowH))
            {
                var ccFallback = FormatCountryFallback(entry.CountryCode, entry.FlairId);
                if (!string.IsNullOrEmpty(ccFallback))
                    DrawL(context, dw, fmt, ccColor, ccFallback, xCc, y, ccW, rowH);
            }

            // CAR#
            DrawR(context, dw, fmt, rowBrush, "#" + entry.CarNumber, xCar, y, carW, rowH);

            // DRIVER NAME
            var name = string.IsNullOrEmpty(entry.DriverName)
                ? "??" : Truncate(entry.DriverName, nameW, charW);
            DrawL(context, dw, fmt, rowBrush, name, xName, y, nameW, rowH);

            // TYRE вЂ” compound index в†’ letter; 0 = "-"
            DrawL(context, dw, fmt, rowBrush, FormatTyre(entry.TireCompound), xTyre, y, tyreW, rowH);

            // STATUS вЂ” GAR / PIT / OUT / ""
            if (entry.IsInGarage)
                DrawL(context, dw, fmt, dimmed, "GAR", xStat, y, statW, rowH);
            else if (entry.IsOnPitRoad)
                DrawL(context, dw, fmt, pitColor, "PIT", xStat, y, statW, rowH);
            else if (entry.IsOutLap)
                DrawL(context, dw, fmt, pitColor, "OUT", xStat, y, statW, rowH);

            // GAP вЂ” suppress numeric gap for garage cars
            if (!entry.IsInGarage)
                DrawR(context, dw, fmt, rowBrush, FormatGap(entry), xGap, y, gapW, rowH);

            // LAST
            var lastStr = entry.LastLapTime > TimeSpan.Zero
                ? FormatLapTime(entry.LastLapTime) : "\u2014";
            DrawR(context, dw, fmt, rowBrush, lastStr, xLast, y, lastW, rowH);

            y += rowH;
            rowIndex++;
        }
    }

    // в”Ђв”Ђ Entry selection в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private static IReadOnlyList<RelativeEntry> SelectVisible(
        IReadOnlyList<RelativeEntry> entries, int maxShown)
    {
        if (entries.Count == 0) return entries;

        int playerIdx = -1;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].IsPlayer) { playerIdx = i; break; }

        if (playerIdx < 0)
            return entries.Count <= maxShown ? entries : entries.Take(maxShown).ToArray();

        const int aroundPlayer = 5;
        int start = Math.Max(0, playerIdx - aroundPlayer);
        int end   = Math.Min(entries.Count - 1, playerIdx + aroundPlayer);
        var window = entries.Skip(start).Take(end - start + 1).ToArray();

        if (maxShown > 0 && window.Length > maxShown)
            return window.Take(maxShown).ToArray();

        return window;
    }

    private static string FormatCountryFallback(string? countryCode, int flairId)
    {
        var iso2 = CountryCodeResolver.NormalizeIso2Code(countryCode);
        if (iso2.Length == 2)
        {
            if (iso2 is "GO" or "RU")
                return string.Empty;
            return iso2;
        }

        return flairId > 0 ? $"#{flairId}" : string.Empty;
    }

    // в”Ђв”Ђ Formatting в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private static string FormatGap(RelativeEntry entry)
    {
        if (entry.IsPlayer) return " 0.00";
        var gap = entry.GapToPlayerSeconds;
        var s   = Math.Abs(gap).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        return gap < 0 ? $"-{s}" : $"+{s}";
    }

    private static string FormatLapTime(TimeSpan ts)
    {
        int m  = (int)ts.TotalMinutes;
        int s  = ts.Seconds;
        int ms = ts.Milliseconds;
        return $"{m}:{s:D2}.{ms:D3}";
    }

    private static string FormatTyre(int compound) => compound switch
    {
        0 => "-",
        1 => "D",   // Dry / Hard
        2 => "W",   // Wet
        3 => "S",   // Soft (some series)
        4 => "M",   // Medium
        _ => compound.ToString(),
    };

    private static string Truncate(string text, float maxPixels, float charW)
    {
        int maxChars = (int)(maxPixels / charW);
        if (maxChars <= 1) return "";
        if (text.Length <= maxChars) return text;
        return text[..(maxChars - 1)] + "\u2026";
    }

    protected override void OnDeviceRecovered()
    {
        DisposeFlagBitmaps();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DisposeFlagBitmaps();

        base.Dispose(disposing);
    }

    private void DisposeFlagBitmaps()
    {
        foreach (var bmp in _flagBitmaps.Values)
            bmp.Dispose();
        _flagBitmaps.Clear();
    }

    private bool TryDrawFlag(
        ID2D1RenderTarget context,
        string? countryCode,
        float x,
        float y,
        float colW,
        float rowH)
    {
        if (!TryGetFlagBitmap(context, countryCode, out var bitmap))
            return false;

        const float verticalPadding = 2f;
        var maxH = MathF.Max(8f, rowH - (verticalPadding * 2f));
        var maxW = MathF.Max(8f, colW - 1f);
        var w = MathF.Min(maxW, maxH * (4f / 3f));
        var h = MathF.Min(maxH, w * (3f / 4f));
        var left = x + MathF.Max(0f, (colW - w) * 0.5f);
        var top = y + verticalPadding + MathF.Max(0f, (maxH - h) * 0.5f);

        context.DrawBitmap(
            bitmap,
            new Vortice.RawRectF(left, top, left + w, top + h),
            1f,
            BitmapInterpolationMode.Linear,
            null);

        return true;
    }

    private bool TryGetFlagBitmap(
        ID2D1RenderTarget context,
        string? countryCode,
        out ID2D1Bitmap bitmap)
    {
        bitmap = null!;

        var iso2 = CountryCodeResolver.NormalizeIso2Code(countryCode);
        if (iso2.Length != 2) return false;

        if (_flagBitmaps.TryGetValue(iso2, out var cached))
        {
            bitmap = cached;
            return true;
        }

        if (!FlagIconStore.TryGetRaster(iso2, out var raster))
            return false;

        var handle = System.Runtime.InteropServices.GCHandle.Alloc(
            raster.Pixels,
            System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            bitmap = context.CreateBitmap(
                new SizeI(raster.Width, raster.Height),
                handle.AddrOfPinnedObject(),
                raster.Width * 4,
                new BitmapProperties(new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied)));
        }
        finally
        {
            handle.Free();
        }

        _flagBitmaps[iso2] = bitmap;
        return true;
    }

    // в”Ђв”Ђ Draw helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private static void DrawL(ID2D1RenderTarget ctx, IDWriteFactory dw, IDWriteTextFormat fmt,
        ID2D1Brush brush, string text, float x, float y, float colW, float colH)
    {
        using var layout = dw.CreateTextLayout(text, fmt, colW, colH);
        layout.TextAlignment = TextAlignment.Leading;
        ctx.DrawTextLayout(new Vector2(x, y), layout, brush, DrawTextOptions.Clip);
    }

    private static void DrawR(ID2D1RenderTarget ctx, IDWriteFactory dw, IDWriteTextFormat fmt,
        ID2D1Brush brush, string text, float x, float y, float colW, float colH)
    {
        using var layout = dw.CreateTextLayout(text, fmt, colW, colH);
        layout.TextAlignment = TextAlignment.Trailing;
        ctx.DrawTextLayout(new Vector2(x, y), layout, brush, DrawTextOptions.Clip);
    }
}



