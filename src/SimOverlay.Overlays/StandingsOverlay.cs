using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Rendering;
using SimOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace SimOverlay.Overlays;

/// <summary>
/// Full-field leaderboard showing all drivers by race position, with multi-class support.
/// Supports Combined (all cars together) and ClassGrouped (class separator rows) display modes.
/// </summary>
public sealed class StandingsOverlay : BaseOverlay
{
    public const string OverlayId   = "Standings";
    public const string WindowTitle = "SimOverlay \u2014 Standings";

    public static OverlayConfig DefaultConfig => new()
    {
        Id                  = OverlayId,
        Enabled             = true,
        X                   = 100,
        Y                   = 100,
        Width               = 520,
        Height              = 500,
        Opacity             = 0.85f,
        BackgroundColor     = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize            = 13f,
        ShowClassBadge      = true,
        ShowBestLap         = true,
        MaxStandingsRows    = 30,
        StandingsDisplayMode = StandingsDisplayMode.Combined,
    };

    // ── Live data ─────────────────────────────────────────────────────────────
    private volatile StandingsData? _standings;

    // ── Mock data ─────────────────────────────────────────────────────────────
    private static readonly StandingsData MockData = BuildMock();

    private static StandingsData BuildMock()
    {
        var gtpColor  = new ColorConfig { R = 0.8f, G = 0.2f, B = 0.2f, A = 1f };
        var lmp2Color = new ColorConfig { R = 0.2f, G = 0.5f, B = 1.0f, A = 1f };
        var gt3Color  = new ColorConfig { R = 0.2f, G = 0.7f, B = 0.3f, A = 1f };

        var entries = new List<StandingsEntry>
        {
            new() { Position=1,  ClassPosition=1, CarNumber="91",  DriverName="K. Estre",       IRating=8234, CarClass="GTP",  ClassColor=gtpColor,  GapToLeaderSeconds=0f,     LapDifference=0, BestLapTime=TimeSpan.FromSeconds(102.3) },
            new() { Position=2,  ClassPosition=2, CarNumber="92",  DriverName="M. Campbell",    IRating=7891, CarClass="GTP",  ClassColor=gtpColor,  GapToLeaderSeconds=1.234f, LapDifference=0, BestLapTime=TimeSpan.FromSeconds(102.8) },
            new() { Position=3,  ClassPosition=3, CarNumber="3",   DriverName="A. Garcia",      IRating=7654, CarClass="GTP",  ClassColor=gtpColor,  GapToLeaderSeconds=4.567f, LapDifference=0, BestLapTime=TimeSpan.FromSeconds(103.1) },
            new() { Position=4,  ClassPosition=4, CarNumber="4",   DriverName="T. Milner",      IRating=6789, CarClass="GTP",  ClassColor=gtpColor,  GapToLeaderSeconds=8.901f, LapDifference=0, BestLapTime=TimeSpan.FromSeconds(103.5), IsPlayer=true },
            new() { Position=5,  ClassPosition=5, CarNumber="62",  DriverName="N. Tandy",       IRating=7234, CarClass="GTP",  ClassColor=gtpColor,  GapToLeaderSeconds=12.34f, LapDifference=0, BestLapTime=TimeSpan.FromSeconds(103.2) },
            new() { Position=6,  ClassPosition=1, CarNumber="31",  DriverName="R. Rast",        IRating=5678, CarClass="LMP2", ClassColor=lmp2Color, GapToLeaderSeconds=0f,     LapDifference=1, BestLapTime=TimeSpan.FromSeconds(101.2) },
            new() { Position=7,  ClassPosition=2, CarNumber="22",  DriverName="F. Albuquerque", IRating=5432, CarClass="LMP2", ClassColor=lmp2Color, GapToLeaderSeconds=2.10f,  LapDifference=1, BestLapTime=TimeSpan.FromSeconds(101.5) },
            new() { Position=8,  ClassPosition=1, CarNumber="77",  DriverName="M. Martin",      IRating=4321, CarClass="GT3",  ClassColor=gt3Color,  GapToLeaderSeconds=0f,     LapDifference=2, BestLapTime=TimeSpan.FromSeconds(107.8) },
            new() { Position=9,  ClassPosition=2, CarNumber="12",  DriverName="P. Hanson",      IRating=3987, CarClass="GT3",  ClassColor=gt3Color,  GapToLeaderSeconds=3.45f,  LapDifference=2, BestLapTime=TimeSpan.FromSeconds(108.1) },
            new() { Position=10, ClassPosition=3, CarNumber="88",  DriverName="C. Eastwood",    IRating=3456, CarClass="GT3",  ClassColor=gt3Color,  GapToLeaderSeconds=6.78f,  LapDifference=2, BestLapTime=TimeSpan.FromSeconds(108.4) },
        };
        return new StandingsData { Entries = entries };
    }

    public StandingsOverlay(
        ISimDataBus bus,
        OverlayConfig config,
        ConfigStore configStore,
        AppConfig appConfig)
        : base(WindowTitle, config, bus, configStore, appConfig)
    {
        Subscribe<StandingsData>(data => _standings = data);
    }

    protected override void OnRender(ID2D1RenderTarget ctx, OverlayConfig cfg)
    {
        var data    = IsLocked ? _standings : MockData;
        var entries = data?.Entries ?? [];

        float pad     = 8f;
        float w       = (float)cfg.Width;
        float h       = (float)cfg.Height;
        float fontSize = cfg.FontSize;
        float charW   = fontSize * 0.615f;
        float rowH    = fontSize + 6f;

        var dw      = Resources.WriteFactory;
        var fmt     = Resources.GetTextFormat("Consolas", fontSize);
        var smallFmt = Resources.GetTextFormat("Consolas", MathF.Max(fontSize - 1.5f, 8f));
        var text    = Resources.GetBrush(cfg.TextColor);
        var dimmed  = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                          cfg.TextColor.B, cfg.TextColor.A * 0.45f);
        var other   = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                          cfg.TextColor.B, cfg.TextColor.A * 0.70f);
        var sep     = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                          cfg.TextColor.B, 0.25f);
        var rowAlt  = Resources.GetBrush(1f, 1f, 1f, 0.04f);
        var black   = Resources.GetBrush(0f, 0f, 0f, 1f);

        // ── Detect if iRating data is present ────────────────────────────────
        bool hasIRating = entries.Any(e => e.IRating > 0);
        bool isMultiClass = entries.Any(e => !string.IsNullOrEmpty(e.CarClass));

        // ── Column widths ─────────────────────────────────────────────────────
        float posW  = 4f * charW;
        float clsW  = (cfg.ShowClassBadge && isMultiClass) ? 5f * charW : 0f;
        float carW  = 5f * charW;
        float irtgW = hasIRating ? 5f * charW : 0f;
        float gapW  = 8f * charW;
        float bestW = cfg.ShowBestLap ? 8f * charW : 0f;
        float colGap = charW;

        float fixedW = pad + posW + colGap
                           + (clsW > 0 ? clsW  + colGap : 0)
                           + carW  + colGap
                           + (irtgW > 0 ? irtgW + colGap : 0)
                           + gapW  + colGap
                           + (bestW > 0 ? bestW + colGap : 0)
                           + pad + 5f;

        float nameW = MathF.Max(w - fixedW, 40f);

        float xPos  = pad;
        float xCls  = xPos  + posW  + colGap;
        float xCar  = xCls  + (clsW > 0 ? clsW + colGap : 0);
        float xName = xCar  + carW  + colGap;
        float xIrtg = xName + nameW + colGap;
        float xGap  = xIrtg + (irtgW > 0 ? irtgW + colGap : 0);
        float xBest = xGap  + gapW  + colGap;

        // ── Header ────────────────────────────────────────────────────────────
        float y = pad;
        DrawR(ctx, dw, fmt, dimmed, "POS",    xPos,  y, posW,  rowH);
        if (clsW > 0)
            DrawL(ctx, dw, fmt, dimmed, "CLS", xCls, y, clsW, rowH);
        DrawR(ctx, dw, fmt, dimmed, "CAR",    xCar,  y, carW,  rowH);
        DrawL(ctx, dw, fmt, dimmed, "DRIVER NAME", xName, y, nameW, rowH);
        if (irtgW > 0)
            DrawR(ctx, dw, fmt, dimmed, "iRTG", xIrtg, y, irtgW, rowH);
        DrawR(ctx, dw, fmt, dimmed, "GAP",    xGap,  y, gapW,  rowH);
        if (bestW > 0)
            DrawR(ctx, dw, fmt, dimmed, "BEST", xBest, y, bestW, rowH);

        y += rowH + 1f;
        ctx.DrawLine(new Vector2(pad, y), new Vector2(w - pad, y), sep, 1f);
        y += 3f;

        // ── Rows ──────────────────────────────────────────────────────────────
        var grouped = cfg.StandingsDisplayMode == StandingsDisplayMode.ClassGrouped && isMultiClass
            ? GroupByClass(entries)
            : null;

        var toRender = grouped ?? new List<object>(entries.Cast<object>());

        int rowIndex = 0;
        foreach (var item in toRender)
        {
            if (y + rowH > h - pad) break;
            if (rowIndex >= cfg.MaxStandingsRows) break;

            if (item is string classLabel)
            {
                // Class separator row
                var sepBg = Resources.GetBrush(cfg.TextColor.R * 0.15f, cfg.TextColor.G * 0.15f,
                                                cfg.TextColor.B * 0.15f, 0.6f);
                ctx.FillRectangle(new Vortice.RawRectF(0, y, w, y + rowH), sepBg);
                using var sepLayout = dw.CreateTextLayout($" \u2500\u2500\u2500\u2500 {classLabel} ", fmt, w - 2f * pad, rowH);
                sepLayout.TextAlignment = TextAlignment.Center;
                ctx.DrawTextLayout(new Vector2(pad, y), sepLayout, dimmed, DrawTextOptions.Clip);
                y += rowH;
                continue;
            }

            if (item is not StandingsEntry entry) continue;

            var rowBrush = entry.IsPlayer ? text : other;

            if (entry.IsPlayer)
            {
                var hc = cfg.PlayerHighlightColor;
                var hl = Resources.GetBrush(hc.R, hc.G, hc.B, hc.A);
                ctx.FillRectangle(new Vortice.RawRectF(0, y - 1, w, y + rowH + 1), hl);
            }
            else if (rowIndex % 2 == 0)
            {
                ctx.FillRectangle(new Vortice.RawRectF(0, y, w, y + rowH), rowAlt);
            }

            if (entry.IsPlayer)
                DrawL(ctx, dw, fmt, text, "\u25ba", pad - charW, y, charW + 2f, rowH);

            // POS
            var posStr = entry.Position > 0 ? entry.Position.ToString() : "-";
            DrawR(ctx, dw, fmt, rowBrush, posStr, xPos, y, posW, rowH);

            // CLS badge
            if (clsW > 0 && !string.IsNullOrEmpty(entry.CarClass))
            {
                var cc = entry.ClassColor;
                var clsBg = Resources.GetBrush(cc.R, cc.G, cc.B, 0.85f);
                ctx.FillRectangle(new Vortice.RawRectF(xCls, y + 1f, xCls + clsW - 2f, y + rowH - 1f), clsBg);
                bool darkText = (cc.R * 0.299f + cc.G * 0.587f + cc.B * 0.114f) > 0.55f;
                var clsTextBrush = darkText ? black : rowBrush;
                using var clsLayout = dw.CreateTextLayout(entry.CarClass, smallFmt, clsW - 4f, rowH);
                clsLayout.TextAlignment = TextAlignment.Center;
                ctx.DrawTextLayout(new Vector2(xCls + 2f, y + 1f), clsLayout, clsTextBrush, DrawTextOptions.Clip);
            }

            // CAR
            DrawR(ctx, dw, fmt, rowBrush, "#" + entry.CarNumber, xCar, y, carW, rowH);

            // DRIVER NAME ("??" when unavailable)
            var name = string.IsNullOrEmpty(entry.DriverName)
                ? "??"
                : Truncate(entry.DriverName, nameW, charW);
            DrawL(ctx, dw, fmt, rowBrush, name, xName, y, nameW, rowH);

            // iRTG
            if (irtgW > 0)
            {
                var irtgStr = entry.IRating > 0 ? entry.IRating.ToString() : "----";
                DrawR(ctx, dw, fmt, rowBrush, irtgStr, xIrtg, y, irtgW, rowH);
            }

            // GAP
            DrawR(ctx, dw, fmt, rowBrush, FormatGap(entry), xGap, y, gapW, rowH);

            // BEST
            if (bestW > 0)
            {
                var bestStr = entry.BestLapTime.TotalSeconds > 1
                    ? FormatLapTime(entry.BestLapTime)
                    : "\u2014";
                DrawR(ctx, dw, fmt, rowBrush, bestStr, xBest, y, bestW, rowH);
            }

            y += rowH;
            rowIndex++;
        }
    }

    // ── Group entries by class for ClassGrouped mode ──────────────────────────

    private static List<object> GroupByClass(IReadOnlyList<StandingsEntry> entries)
    {
        var result = new List<object>(entries.Count + 8);
        string? currentClass = null;

        foreach (var entry in entries.OrderBy(e => e.Position == 0 ? int.MaxValue : e.Position))
        {
            if (entry.CarClass != currentClass)
            {
                currentClass = entry.CarClass;
                if (!string.IsNullOrEmpty(currentClass))
                    result.Add(currentClass);
            }
            result.Add(entry);
        }

        return result;
    }

    // ── Formatting ────────────────────────────────────────────────────────────

    private static string FormatGap(StandingsEntry entry)
    {
        // Only the explicit leader (gap set to exactly 0f in BuildStandings) shows "LEADER".
        if (entry.GapToLeaderSeconds == 0f) return "LEADER";
        if (entry.LapDifference == 1) return "+1 LAP";
        if (entry.LapDifference > 1)  return $"+{entry.LapDifference} LAPS";
        return $"+{entry.GapToLeaderSeconds:F3}";
    }

    private static string FormatLapTime(TimeSpan t)
    {
        int    m = (int)t.TotalMinutes;
        double s = t.TotalSeconds - m * 60.0;
        return m > 0 ? $"{m}:{s:00.0}" : $"{s:0.0}";
    }

    private static string Truncate(string text, float maxPx, float charW)
    {
        int max = (int)(maxPx / charW);
        if (max <= 1) return "";
        if (text.Length <= max) return text;
        return text[..(max - 1)] + "\u2026";
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
