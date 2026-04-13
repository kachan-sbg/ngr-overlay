using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Rendering;
using SimOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace SimOverlay.Overlays;

/// <summary>
/// Full-field leaderboard. Columns (all optional via config):
/// POS | [CLS] | CC | CAR# | DRIVER NAME | [TEAM] | LIC | iRTG | TYRE | INT | GAP | BEST | LAST | STINT | GAINED | [PIT T]
/// </summary>
public sealed class StandingsOverlay : BaseOverlay
{
    public const string OverlayId   = "Standings";
    public const string WindowTitle = "SimOverlay \u2014 Standings";

    public static OverlayConfig DefaultConfig => new()
    {
        Id                   = OverlayId,
        Enabled              = true,
        X                    = 100,
        Y                    = 100,
        Width                = 860,
        Height               = 500,
        Opacity              = 0.85f,
        BackgroundColor      = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize             = 13f,
        ShowClassBadge       = true,
        ShowBestLap          = true,
        ShowLastLap          = true,
        ShowInterval         = true,
        ShowStint            = true,
        ShowPositionsGained  = true,
        ShowIRating          = true,
        ShowLicense          = true,
        ShowTeam             = false,
        ShowPitTime          = false,
        MaxStandingsRows     = 30,
        StandingsDisplayMode = StandingsDisplayMode.Combined,
    };

    // ── Live data ─────────────────────────────────────────────────────────────
    private volatile StandingsData? _standings;

    // ── Mock data ─────────────────────────────────────────────────────────────
    private static readonly StandingsData MockData = BuildMock();

    private static StandingsData BuildMock()
    {
        var gtpColor  = new ColorConfig { R = 0.8f, G = 0.2f, B = 0.2f, A = 1f };
        var gt3Color  = new ColorConfig { R = 0.2f, G = 0.7f, B = 0.3f, A = 1f };

        return new StandingsData
        {
            Entries =
            [
                new() { Position=1,  ClassPosition=1, CarNumber="91",  DriverName="K. Estre",        ClubName="Germany",      TeamName="Porsche GT",  CarScreenName="Porsche 911 GT3 R (992)", IRating=8234, LicenseClass=LicenseClass.A,   LicenseLevel="A 4.2", CarClass="GTP",  ClassColor=gtpColor,  GapToLeaderSeconds=0f,     Interval=0f,     LapDifference=0, BestLapTime=TimeSpan.FromSeconds(102.3), LastLapTime=TimeSpan.FromSeconds(102.5), TireCompound=1, StintLaps=8,  PositionsGained=2,  PitLaneTime=22.1f },
                new() { Position=2,  ClassPosition=2, CarNumber="92",  DriverName="M. Campbell",     ClubName="Australia",    TeamName="Porsche GT",  CarScreenName="Porsche 911 GT3 R (992)", IRating=7891, LicenseClass=LicenseClass.A,   LicenseLevel="A 3.8", CarClass="GTP",  ClassColor=gtpColor,  GapToLeaderSeconds=1.234f, Interval=1.234f, LapDifference=0, BestLapTime=TimeSpan.FromSeconds(102.8), LastLapTime=TimeSpan.FromSeconds(103.1), TireCompound=1, StintLaps=8,  PositionsGained=0,  PitLaneTime=21.8f },
                new() { Position=3,  ClassPosition=3, CarNumber="4",   DriverName="T. Milner",       ClubName="USA - NE",     TeamName="Corvette RT", CarScreenName="Corvette Z06.R GT3.R",   IRating=6789, LicenseClass=LicenseClass.A,   LicenseLevel="A 2.1", CarClass="GTP",  ClassColor=gtpColor,  GapToLeaderSeconds=8.901f, Interval=7.667f, LapDifference=0, BestLapTime=TimeSpan.FromSeconds(103.5), LastLapTime=TimeSpan.FromSeconds(103.8), TireCompound=1, StintLaps=8,  PositionsGained=-1, PitLaneTime=23.4f, IsPlayer=true },
                new() { Position=4,  ClassPosition=4, CarNumber="3",   DriverName="A. Garcia",       ClubName="Spain",        TeamName="Corvette RT", CarScreenName="Corvette Z06.R GT3.R",   IRating=7654, LicenseClass=LicenseClass.A,   LicenseLevel="A 3.5", CarClass="GTP",  ClassColor=gtpColor,  GapToLeaderSeconds=12.34f, Interval=3.439f, LapDifference=0, BestLapTime=TimeSpan.FromSeconds(103.1), LastLapTime=TimeSpan.FromSeconds(103.4), TireCompound=1, StintLaps=6,  PositionsGained=1,  PitLaneTime=0f,    IsOnPitRoad=false, IsOutLap=true },
                new() { Position=5,  ClassPosition=5, CarNumber="62",  DriverName="N. Tandy",        ClubName="Great Britain",TeamName="Corvette RT", CarScreenName="Corvette Z06.R GT3.R",   IRating=7234, LicenseClass=LicenseClass.A,   LicenseLevel="A 3.1", CarClass="GTP",  ClassColor=gtpColor,  GapToLeaderSeconds=19.2f,  Interval=6.860f, LapDifference=0, BestLapTime=TimeSpan.FromSeconds(103.2), LastLapTime=TimeSpan.FromSeconds(103.5), TireCompound=1, StintLaps=8,  PositionsGained=0,  PitLaneTime=22.7f },
                new() { Position=6,  ClassPosition=1, CarNumber="77",  DriverName="M. Martin",       ClubName="Germany",      TeamName="BMW M",       CarScreenName="BMW M4 GT3",             IRating=4321, LicenseClass=LicenseClass.B,   LicenseLevel="B 4.5", CarClass="GT3",  ClassColor=gt3Color,  GapToLeaderSeconds=0f,     Interval=0f,     LapDifference=1, BestLapTime=TimeSpan.FromSeconds(107.8), LastLapTime=TimeSpan.FromSeconds(108.0), TireCompound=1, StintLaps=12, PositionsGained=3,  PitLaneTime=21.2f },
                new() { Position=7,  ClassPosition=2, CarNumber="12",  DriverName="P. Hanson",       ClubName="Sweden",       TeamName="United ARS",  CarScreenName="Porsche 911 GT3 R (992)", IRating=3987, LicenseClass=LicenseClass.B,   LicenseLevel="B 3.2", CarClass="GT3",  ClassColor=gt3Color,  GapToLeaderSeconds=3.45f,  Interval=3.450f, LapDifference=1, BestLapTime=TimeSpan.FromSeconds(108.1), LastLapTime=TimeSpan.FromSeconds(108.4), TireCompound=1, StintLaps=12, PositionsGained=-2, PitLaneTime=22.5f },
                new() { Position=8,  ClassPosition=3, CarNumber="88",  DriverName="C. Eastwood",     ClubName="Ireland",      TeamName="TF Sport",    CarScreenName="Aston Martin Vantage",   IRating=3456, LicenseClass=LicenseClass.C,   LicenseLevel="C 1.2", CarClass="GT3",  ClassColor=gt3Color,  GapToLeaderSeconds=9.87f,  Interval=6.420f, LapDifference=1, BestLapTime=TimeSpan.FromSeconds(108.4), LastLapTime=TimeSpan.FromSeconds(108.7), TireCompound=2, StintLaps=3,  PositionsGained=1,  PitLaneTime=24.1f, IsOnPitRoad=false, IsOutLap=true },
            ]
        };
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

        float pad      = 8f;
        float w        = (float)cfg.Width;
        float h        = (float)cfg.Height;
        float fontSize = cfg.FontSize;
        float charW    = fontSize * 0.615f;
        float rowH     = fontSize + 6f;

        var dw       = Resources.WriteFactory;
        var fmt      = Resources.GetTextFormat("Consolas", fontSize);
        var smallFmt = Resources.GetTextFormat("Consolas", MathF.Max(fontSize - 1.5f, 8f));
        var text     = Resources.GetBrush(cfg.TextColor);
        var dimmed   = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G, cfg.TextColor.B, cfg.TextColor.A * 0.45f);
        var other    = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G, cfg.TextColor.B, cfg.TextColor.A * 0.70f);
        var ccColor  = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G, cfg.TextColor.B, cfg.TextColor.A * 0.55f);
        var sep      = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G, cfg.TextColor.B, 0.25f);
        var rowAlt   = Resources.GetBrush(1f, 1f, 1f, 0.04f);
        var black    = Resources.GetBrush(0f, 0f, 0f, 1f);
        var pitColor = Resources.GetBrush(0.8f, 0.5f, 0.1f, 1f);
        var gainedPos = Resources.GetBrush(0.2f, 0.8f, 0.3f, 1f);   // green for positions gained
        var gainedNeg = Resources.GetBrush(0.9f, 0.3f, 0.2f, 1f);   // red for positions lost

        bool hasIRating   = cfg.ShowIRating  && entries.Any(e => e.IRating > 0);
        bool isMultiClass = entries.Any(e => !string.IsNullOrEmpty(e.CarClass));
        bool showCls      = cfg.ShowClassBadge && isMultiClass;
        bool showTeam     = cfg.ShowTeam;
        bool showLic      = cfg.ShowLicense;
        bool showBest     = cfg.ShowBestLap;
        bool showLast     = cfg.ShowLastLap;
        bool showInt      = cfg.ShowInterval;
        bool showStint    = cfg.ShowStint;
        bool showGained   = cfg.ShowPositionsGained;
        bool showPitTime  = cfg.ShowPitTime;

        // ── Column widths ─────────────────────────────────────────────────────
        float cg     = charW;   // column gap
        float posW   = 3f * charW;
        float clsW   = showCls     ? 5f * charW : 0f;
        float ccW    = 2f * charW;
        float carW   = 5f * charW;
        float licW   = showLic     ? 6f * charW : 0f;
        float irtgW  = hasIRating  ? 5f * charW : 0f;
        float tyreW  = 2f * charW;
        float intW   = showInt     ? 7f * charW : 0f;
        float gapW   = 8f * charW;
        float bestW  = showBest    ? 7f * charW : 0f;
        float lastW  = showLast    ? 7f * charW : 0f;
        float stintW = showStint   ? 4f * charW : 0f;
        float gainW  = showGained  ? 4f * charW : 0f;
        float pitTW  = showPitTime ? 6f * charW : 0f;

        float ColW(float cw) => cw > 0 ? cw + cg : 0f;

        float fixedW = pad
            + posW  + cg
            + ColW(clsW)
            + ccW   + cg
            + carW  + cg
            + ColW(licW)
            + ColW(irtgW)
            + tyreW + cg
            + ColW(intW)
            + gapW  + cg
            + ColW(bestW)
            + ColW(lastW)
            + ColW(stintW)
            + ColW(gainW)
            + ColW(pitTW)
            + pad + 5f;

        float nameW = MathF.Max(w - fixedW, 40f);
        // When team is shown, split name column: name + team (both variable)
        float teamW = showTeam ? MathF.Max(nameW * 0.35f, 40f) : 0f;
        if (showTeam) nameW = MathF.Max(nameW - teamW - cg, 40f);

        // ── Column X positions ────────────────────────────────────────────────
        float xPos   = pad;
        float xCls   = xPos  + posW  + cg;
        float xCc    = xCls  + ColW(clsW);
        float xCar   = xCc   + ccW   + cg;
        float xName  = xCar  + carW  + cg;
        float xTeam  = xName + nameW + cg;
        float xLic   = (showTeam ? xTeam + teamW : xName + nameW) + cg;
        float xIrtg  = xLic  + ColW(licW);
        float xTyre  = xIrtg + ColW(irtgW);
        float xInt   = xTyre + tyreW + cg;
        float xGap   = xInt  + ColW(intW);
        float xBest  = xGap  + gapW  + cg;
        float xLast  = xBest + ColW(bestW);
        float xStint = xLast + ColW(lastW);
        float xGain  = xStint+ ColW(stintW);
        float xPitT  = xGain + ColW(gainW);

        // ── Header ────────────────────────────────────────────────────────────
        float y = pad;
        DrawR(ctx, dw, fmt, dimmed, "P",     xPos,  y, posW,  rowH);
        if (showCls)
            DrawL(ctx, dw, fmt, dimmed, "CLS",  xCls,  y, clsW,  rowH);
        DrawL(ctx, dw, fmt, dimmed, "CC",    xCc,   y, ccW,   rowH);
        DrawR(ctx, dw, fmt, dimmed, "CAR",   xCar,  y, carW,  rowH);
        DrawL(ctx, dw, fmt, dimmed, "DRIVER NAME", xName, y, nameW, rowH);
        if (showTeam)
            DrawL(ctx, dw, fmt, dimmed, "TEAM",  xTeam, y, teamW, rowH);
        if (showLic)
            DrawL(ctx, dw, fmt, dimmed, "LIC",   xLic,  y, licW,  rowH);
        if (hasIRating)
            DrawR(ctx, dw, fmt, dimmed, "iRTG",  xIrtg, y, irtgW, rowH);
        DrawL(ctx, dw, fmt, dimmed, "T",     xTyre, y, tyreW, rowH);
        if (showInt)
            DrawR(ctx, dw, fmt, dimmed, "INT",   xInt,  y, intW,  rowH);
        DrawR(ctx, dw, fmt, dimmed, "GAP",   xGap,  y, gapW,  rowH);
        if (showBest)
            DrawR(ctx, dw, fmt, dimmed, "BEST",  xBest, y, bestW, rowH);
        if (showLast)
            DrawR(ctx, dw, fmt, dimmed, "LAST",  xLast, y, lastW, rowH);
        if (showStint)
            DrawR(ctx, dw, fmt, dimmed, "STI",   xStint,y, stintW,rowH);
        if (showGained)
            DrawR(ctx, dw, fmt, dimmed, "\u25b2", xGain, y, gainW, rowH);  // ▲
        if (showPitTime)
            DrawR(ctx, dw, fmt, dimmed, "PIT T", xPitT, y, pitTW, rowH);

        y += rowH + 1f;
        ctx.DrawLine(new Vector2(pad, y), new Vector2(w - pad, y), sep, 1f);
        y += 3f;

        // ── Rows ──────────────────────────────────────────────────────────────
        var toRender = cfg.StandingsDisplayMode == StandingsDisplayMode.ClassGrouped && isMultiClass
            ? GroupByClass(entries)
            : new List<object>(entries.Cast<object>());

        int rowIndex = 0;
        foreach (var item in toRender)
        {
            if (y + rowH > h - pad) break;
            if (rowIndex >= cfg.MaxStandingsRows) break;

            if (item is string classLabel)
            {
                var sepBg = Resources.GetBrush(cfg.TextColor.R * 0.15f, cfg.TextColor.G * 0.15f,
                                                cfg.TextColor.B * 0.15f, 0.6f);
                ctx.FillRectangle(new Vortice.RawRectF(0, y, w, y + rowH), sepBg);
                using var sepLayout = dw.CreateTextLayout($" \u2500\u2500\u2500\u2500 {classLabel} ",
                    fmt, w - 2f * pad, rowH);
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
            DrawR(ctx, dw, fmt, rowBrush, entry.Position > 0 ? entry.Position.ToString() : "-",
                  xPos, y, posW, rowH);

            // CLS badge
            if (showCls && !string.IsNullOrEmpty(entry.CarClass))
            {
                var cc    = entry.ClassColor;
                var clsBg = Resources.GetBrush(cc.R, cc.G, cc.B, 0.85f);
                ctx.FillRectangle(new Vortice.RawRectF(xCls, y + 1f, xCls + clsW - 2f, y + rowH - 1f), clsBg);
                bool darkText   = (cc.R * 0.299f + cc.G * 0.587f + cc.B * 0.114f) > 0.55f;
                var  clsTextBrush = darkText ? black : rowBrush;
                using var clsLayout = dw.CreateTextLayout(entry.CarClass, smallFmt, clsW - 4f, rowH);
                clsLayout.TextAlignment = TextAlignment.Center;
                ctx.DrawTextLayout(new Vector2(xCls + 2f, y + 1f), clsLayout, clsTextBrush, DrawTextOptions.Clip);
            }

            // CC
            DrawL(ctx, dw, fmt, ccColor, RelativeOverlay.ClubToCode(entry.ClubName), xCc, y, ccW, rowH);

            // CAR#
            DrawR(ctx, dw, fmt, rowBrush, "#" + entry.CarNumber, xCar, y, carW, rowH);

            // DRIVER NAME
            var driverName = string.IsNullOrEmpty(entry.DriverName)
                ? "??" : Truncate(entry.DriverName, nameW, charW);
            DrawL(ctx, dw, fmt, rowBrush, driverName, xName, y, nameW, rowH);

            // TEAM
            if (showTeam && !string.IsNullOrEmpty(entry.TeamName))
            {
                var teamStr = Truncate(entry.TeamName, teamW, charW);
                DrawL(ctx, dw, fmt, dimmed, teamStr, xTeam, y, teamW, rowH);
            }

            // LIC
            if (showLic && !string.IsNullOrEmpty(entry.LicenseLevel))
            {
                var (lr, lg, lb, la) = entry.LicenseClass.GetColor();
                var licBg   = Resources.GetBrush(lr, lg, lb, la);
                ctx.FillRectangle(new Vortice.RawRectF(xLic, y + 1f, xLic + licW - 2f, y + rowH - 1f), licBg);
                var licText = entry.LicenseClass.RequiresDarkText() ? black : rowBrush;
                DrawL(ctx, dw, fmt, licText, entry.LicenseLevel, xLic + 2f, y, licW - 4f, rowH);
            }

            // iRTG
            if (hasIRating)
            {
                DrawR(ctx, dw, fmt, rowBrush,
                      entry.IRating > 0 ? entry.IRating.ToString() : "----",
                      xIrtg, y, irtgW, rowH);
            }

            // TYRE
            DrawL(ctx, dw, fmt, rowBrush, FormatTyre(entry.TireCompound), xTyre, y, tyreW, rowH);

            // STATUS overlay on tyre cell (PIT / OUT)
            if (entry.IsOnPitRoad || entry.IsOutLap)
            {
                var statusStr = entry.IsOnPitRoad ? "PIT" : "OUT";
                DrawL(ctx, dw, fmt, pitColor, statusStr, xTyre - tyreW - cg, y, tyreW + tyreW, rowH);
            }

            // INT
            if (showInt)
                DrawR(ctx, dw, fmt, rowBrush, FormatInterval(entry), xInt, y, intW, rowH);

            // GAP
            DrawR(ctx, dw, fmt, rowBrush, FormatGap(entry), xGap, y, gapW, rowH);

            // BEST
            if (showBest)
            {
                DrawR(ctx, dw, fmt, rowBrush,
                      entry.BestLapTime.TotalSeconds > 1 ? FormatLapTime(entry.BestLapTime) : "\u2014",
                      xBest, y, bestW, rowH);
            }

            // LAST
            if (showLast)
            {
                DrawR(ctx, dw, fmt, rowBrush,
                      entry.LastLapTime.TotalSeconds > 1 ? FormatLapTime(entry.LastLapTime) : "\u2014",
                      xLast, y, lastW, rowH);
            }

            // STINT
            if (showStint)
                DrawR(ctx, dw, fmt, rowBrush,
                      entry.StintLaps > 0 ? entry.StintLaps.ToString() : "-",
                      xStint, y, stintW, rowH);

            // POSITIONS GAINED
            if (showGained)
            {
                var g       = entry.PositionsGained;
                var gBrush  = g > 0 ? gainedPos : g < 0 ? gainedNeg : dimmed;
                var gStr    = g > 0 ? $"+{g}" : g < 0 ? g.ToString() : "\u2013";
                DrawR(ctx, dw, fmt, gBrush, gStr, xGain, y, gainW, rowH);
            }

            // PIT TIME
            if (showPitTime)
            {
                DrawR(ctx, dw, fmt, rowBrush,
                      entry.PitLaneTime > 0f
                          ? entry.PitLaneTime.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                          : "\u2014",
                      xPitT, y, pitTW, rowH);
            }

            y += rowH;
            rowIndex++;
        }
    }

    // ── Grouping ──────────────────────────────────────────────────────────────

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
        if (entry.GapToLeaderSeconds == 0f) return "LEADER";
        if (entry.LapDifference == 1) return "+1 LAP";
        if (entry.LapDifference > 1)  return $"+{entry.LapDifference} LAPS";
        return $"+{entry.GapToLeaderSeconds:F3}";
    }

    private static string FormatInterval(StandingsEntry entry)
    {
        if (entry.GapToLeaderSeconds == 0f) return "\u2014";
        if (entry.LapDifference > 0) return "LAP";
        return $"+{entry.Interval:F3}";
    }

    private static string FormatLapTime(TimeSpan t)
    {
        int    m = (int)t.TotalMinutes;
        double s = t.TotalSeconds - m * 60.0;
        return m > 0 ? $"{m}:{s:00.0}" : $"{s:0.0}";
    }

    private static string FormatTyre(int compound) => compound switch
    {
        0 => "-",
        1 => "D",
        2 => "W",
        3 => "S",
        4 => "M",
        _ => compound.ToString(),
    };

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
