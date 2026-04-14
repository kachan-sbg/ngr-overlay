using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Rendering;
using SimOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace SimOverlay.Overlays;

/// <summary>
/// Relative timing tower — shows ±<c>MaxDriversShown/2</c> drivers centered on the player.
/// Columns: POS | CC | CAR# | DRIVER NAME | TYRE | STATUS | GAP | LAST
/// </summary>
public sealed class RelativeOverlay : BaseOverlay
{
    public const string OverlayId   = "Relative";
    public const string WindowTitle = "SimOverlay \u2014 Relative";

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
        MaxDriversShown = 11,   // ±5 + player
    };

    private volatile RelativeData? _latest;

    // ── Edit-mode mock data ───────────────────────────────────────────────────
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
        // POS(3ch) | CC(2ch) | CAR(5ch) | NAME(variable) | TYRE(2ch) | STATUS(3ch) | GAP(7ch) | LAST(8ch)
        var posW    = 3f * charW;
        var ccW     = 2f * charW;
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
        var fmt      = Resources.GetTextFormat("Consolas", fontSize);
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

            if (entry.IsPlayer)
                DrawL(context, dw, fmt, text, "\u25ba", pad - charW, y, charW + 2f, rowH);

            // POS
            DrawR(context, dw, fmt, rowBrush, entry.Position > 0 ? entry.Position.ToString() : "-",
                  xPos, y, posW, rowH);

            // CC — 2-char country code
            DrawL(context, dw, fmt, ccColor, ClubToCode(entry.ClubName), xCc, y, ccW, rowH);

            // CAR#
            DrawR(context, dw, fmt, rowBrush, "#" + entry.CarNumber, xCar, y, carW, rowH);

            // DRIVER NAME
            var name = string.IsNullOrEmpty(entry.DriverName)
                ? "??" : Truncate(entry.DriverName, nameW, charW);
            DrawL(context, dw, fmt, rowBrush, name, xName, y, nameW, rowH);

            // TYRE — compound index → letter; 0 = "-"
            DrawL(context, dw, fmt, rowBrush, FormatTyre(entry.TireCompound), xTyre, y, tyreW, rowH);

            // STATUS — GAR / PIT / OUT / ""
            if (entry.IsInGarage)
                DrawL(context, dw, fmt, dimmed, "GAR", xStat, y, statW, rowH);
            else if (entry.IsOnPitRoad)
                DrawL(context, dw, fmt, pitColor, "PIT", xStat, y, statW, rowH);
            else if (entry.IsOutLap)
                DrawL(context, dw, fmt, pitColor, "OUT", xStat, y, statW, rowH);

            // GAP — suppress numeric gap for garage cars
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

    // ── Entry selection ───────────────────────────────────────────────────────

    private static IReadOnlyList<RelativeEntry> SelectVisible(
        IReadOnlyList<RelativeEntry> entries, int maxShown)
    {
        if (entries.Count == 0) return entries;

        int playerIdx = -1;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].IsPlayer) { playerIdx = i; break; }

        if (playerIdx < 0)
            return entries.Count <= maxShown ? entries : entries.Take(maxShown).ToArray();

        int ahead = maxShown / 2;
        int start = Math.Max(0, playerIdx - ahead);
        int end   = start + maxShown;

        if (end > entries.Count)
        {
            end   = entries.Count;
            start = Math.Max(0, end - maxShown);
        }

        return entries.Skip(start).Take(end - start).ToArray();
    }

    // ── Formatting ────────────────────────────────────────────────────────────

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

    /// <summary>
    /// Maps an iRacing ClubName (e.g. "Germany", "USA - Southeast") to a flag emoji
    /// (e.g. "🇩🇪"). Falls back to a 2-letter ISO code when the club name is not in
    /// the known list. Flag emoji are composed from Unicode regional indicator symbols
    /// and render correctly when the font stack includes Segoe UI Emoji.
    /// </summary>
    internal static string ClubToCode(string club)
    {
        if (string.IsNullOrEmpty(club)) return "  ";
        var iso = club switch
        {
            "Australia"             or "Australia and NZ"  => "AU",
            "Austria"                                       => "AT",
            "Belgium"                                       => "BE",
            "Brazil"                                        => "BR",
            "Canada"                                        => "CA",
            "China"                                         => "CN",
            "Czech Republic"                                => "CZ",
            "Denmark"                                       => "DK",
            "Finland"                                       => "FI",
            "France"                                        => "FR",
            "Germany"                                       => "DE",
            "Great Britain"                                 => "GB",
            "Greece"                                        => "GR",
            "Hungary"                                       => "HU",
            "India"                                         => "IN",
            "Italy"                                         => "IT",
            "Japan"                                         => "JP",
            "Korea"                                         => "KR",
            "Mexico"                                        => "MX",
            "Netherlands"                                   => "NL",
            "New Zealand"                                   => "NZ",
            "Norway"                                        => "NO",
            "Poland"                                        => "PL",
            "Portugal"                                      => "PT",
            "Romania"                                       => "RO",
            "Russia"                                        => "RU",
            "South Africa"                                  => "ZA",
            "Spain"                                         => "ES",
            "Sweden"                                        => "SE",
            "Switzerland"                                   => "CH",
            "Turkey"                                        => "TR",
            "Ukraine"                                       => "UA",
            "Argentina"                                     => "AR",
            "Colombia"                                      => "CO",
            "Chile"                                         => "CL",
            var s when s.StartsWith("USA")                  => "US",
            var s when s.StartsWith("Iberia")               => "ES",
            var s when s.StartsWith("Mid-South")            => "US",
            _ => club.Length >= 2 ? club[..2].ToUpperInvariant() : club.ToUpperInvariant(),
        };

        // Convert 2-letter ISO code to flag emoji via Unicode regional indicator symbols.
        // Each letter A-Z maps to U+1F1E6..U+1F1FF; two in sequence form a flag.
        if (iso.Length == 2 && char.IsAsciiLetter(iso[0]) && char.IsAsciiLetter(iso[1]))
        {
            return char.ConvertFromUtf32(0x1F1E6 + (char.ToUpperInvariant(iso[0]) - 'A'))
                 + char.ConvertFromUtf32(0x1F1E6 + (char.ToUpperInvariant(iso[1]) - 'A'));
        }
        return iso;
    }

    private static string Truncate(string text, float maxPixels, float charW)
    {
        int maxChars = (int)(maxPixels / charW);
        if (maxChars <= 1) return "";
        if (text.Length <= maxChars) return text;
        return text[..(maxChars - 1)] + "\u2026";
    }

    // ── Draw helpers ──────────────────────────────────────────────────────────

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
