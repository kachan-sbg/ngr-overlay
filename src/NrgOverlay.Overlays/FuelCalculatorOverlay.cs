using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Rendering;
using NrgOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace NrgOverlay.Overlays;

/// <summary>
/// Read-only fuel management overlay: current level, avg consumption, laps remaining,
/// fuel to finish, and recommended pit-add total (with configurable safety margin).
/// </summary>
public sealed class FuelCalculatorOverlay : BaseOverlay
{
    public const string OverlayId   = "FuelCalculator";
    public const string WindowTitle = "NrgOverlay \u2014 Fuel";

    public static OverlayConfig DefaultConfig => new()
    {
        Id                   = OverlayId,
        Enabled              = true,
        X                    = 100,
        Y                    = 100,
        Width                = 240,
        Height               = 200,
        Opacity              = 0.85f,
        BackgroundColor      = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize             = 13f,
        FuelUnit             = FuelUnit.Liters,
        FuelSafetyMarginLaps = 1.0f,
        ShowFuelMargin       = true,
    };

    // в”Ђв”Ђ Live data в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private volatile TelemetryData? _telemetry;
    private volatile SessionData?   _session;
    private volatile DriverData?    _driver;

    // в”Ђв”Ђ Mock data в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private static readonly TelemetryData MockTelemetry = new(
        Throttle: 0.5f, Brake: 0f, Clutch: 0f, SteeringAngle: 0f,
        SpeedMps: 40f, Gear: 3, Rpm: 4000f,
        FuelLevelLiters: 12.4f,
        FuelConsumptionPerLap: 2.83f,
        LastLapFuelLiters: 2.91f,
        IncidentCount: 0);

    private static readonly SessionData MockSession = new()
    {
        TrackName            = "Silverstone",
        SessionType          = SessionType.Race,
        TotalLaps            = 20,
        SessionTimeRemaining = TimeSpan.FromMinutes(30),
    };

    private static readonly DriverData MockDriver = new()
    {
        Lap          = 10,
        LastLapTime  = TimeSpan.FromSeconds(94.5),
        BestLapTime  = TimeSpan.FromSeconds(93.9),
    };

    public FuelCalculatorOverlay(
        ISimDataBus bus,
        OverlayConfig config,
        ConfigStore configStore,
        AppConfig appConfig)
        : base(WindowTitle, config, bus, configStore, appConfig)
    {
        Subscribe<TelemetryData>(data => _telemetry = data);
        Subscribe<SessionData>(data => _session = data);
        Subscribe<DriverData>(data => _driver = data);
    }

    protected override void OnRender(ID2D1RenderTarget ctx, OverlayConfig cfg)
    {
        bool edit = !IsLocked;

        TelemetryData? telem  = edit ? MockTelemetry : _telemetry;
        SessionData?   session = edit ? MockSession   : _session;
        DriverData?    driver  = edit ? MockDriver    : _driver;

        float pad  = 8f;
        float w    = (float)cfg.Width;
        float h    = (float)cfg.Height;
        var   dw   = Resources.WriteFactory;

        float fontSize    = cfg.FontSize;
        float rowH        = fontSize + 6f;
        var   fmt         = Resources.GetTextFormat("Oswald", fontSize);
        var   boldFmt     = Resources.GetTextFormat("Oswald", fontSize + 1f);
        var   headerFmt   = Resources.GetTextFormat("Oswald", fontSize - 1f);
        var   text        = Resources.GetBrush(cfg.TextColor);
        var   dimmed      = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                                cfg.TextColor.B, cfg.TextColor.A * 0.45f);
        var   accent      = Resources.GetBrush(0.2f, 0.8f, 1f, 1f);
        var   sep         = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                                cfg.TextColor.B, 0.25f);

        float innerW  = w - 2f * pad;
        float labelW  = innerW * 0.55f;
        float valueW  = innerW - labelW;
        float xLabel  = pad;
        float xValue  = pad + labelW;

        float y = pad;

        // в”Ђв”Ђ Header в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        DrawL(ctx, dw, headerFmt, dimmed, "FUEL", xLabel, y, labelW, rowH);
        y += rowH - 2f;

        // Separator line
        ctx.DrawLine(new Vector2(pad, y), new Vector2(w - pad, y), sep, 1f);
        y += 4f;

        // в”Ђв”Ђ Compute values в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        bool isRace = session?.SessionType == SessionType.Race;
        bool isLapLimited = isRace && session?.TotalLaps > 0;

        float fuelL    = telem?.FuelLevelLiters        ?? 0f;
        float avgL     = telem?.FuelConsumptionPerLap  ?? 0f;
        float lastLapL = telem?.LastLapFuelLiters      ?? 0f;
        bool  hasAvg   = avgL > 0.001f;
        bool  hasLast  = lastLapL > 0.001f;

        // Convert to display unit
        bool   gallons      = cfg.FuelUnit == FuelUnit.Gallons;
        string unitStr      = gallons ? "gal" : "L";
        float  fuelDisp     = gallons ? fuelL    * 0.264172f : fuelL;
        float  avgDisp      = gallons ? avgL     * 0.264172f : avgL;
        float  lastLapDisp  = gallons ? lastLapL * 0.264172f : lastLapL;

        // Laps remaining on current fuel
        float? lapsLeft = hasAvg ? fuelL / avgL : null;

        // Avg lap time for time-limited estimation
        TimeSpan? avgLap = null;
        if (driver != null)
        {
            var best = driver.BestLapTime;
            var last = driver.LastLapTime;
            if (best.TotalSeconds > 10) avgLap = best;
            else if (last.TotalSeconds > 10) avgLap = last;
        }

        // Fuel needed to finish
        float? fuelToFinish = null;
        if (hasAvg && isRace)
        {
            if (isLapLimited && session != null && driver != null)
            {
                int remaining = Math.Max(0, session.TotalLaps - driver.Lap);
                fuelToFinish = remaining * avgL;
            }
            else if (!isLapLimited && session != null && avgLap.HasValue)
            {
                double lapsEst = session.SessionTimeRemaining.TotalSeconds / avgLap.Value.TotalSeconds;
                fuelToFinish = (float)lapsEst * avgL;
            }
        }

        float? fuelToAdd      = fuelToFinish.HasValue ? MathF.Max(0f, fuelToFinish.Value - fuelL) : null;
        float? marginFuel     = hasAvg ? cfg.FuelSafetyMarginLaps * avgL : null;
        float? pitAddTotal    = (fuelToAdd.HasValue && marginFuel.HasValue) ? fuelToAdd.Value + marginFuel.Value : null;

        // Convert finish/add/margin to display unit
        float? fuelToFinishDisp = fuelToFinish.HasValue ? (gallons ? fuelToFinish.Value * 0.264172f : fuelToFinish.Value) : null;
        float? fuelToAddDisp    = fuelToAdd.HasValue    ? (gallons ? fuelToAdd.Value    * 0.264172f : fuelToAdd.Value)    : null;
        float? marginFuelDisp   = marginFuel.HasValue   ? (gallons ? marginFuel.Value   * 0.264172f : marginFuel.Value)  : null;
        float? pitAddTotalDisp  = pitAddTotal.HasValue  ? (gallons ? pitAddTotal.Value  * 0.264172f : pitAddTotal.Value) : null;

        // в”Ђв”Ђ Rows в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        DrawRow(ctx, dw, fmt, text, dimmed, "Level", $"{fuelDisp:F1} {unitStr}", xLabel, xValue, y, labelW, valueW, rowH);
        y += rowH;

        DrawRow(ctx, dw, fmt, text, dimmed, "Last Lap",
            hasLast ? $"{lastLapDisp:F2} {unitStr}" : "\u2014",
            xLabel, xValue, y, labelW, valueW, rowH);
        y += rowH;

        DrawRow(ctx, dw, fmt, text, dimmed, "Avg/Lap (5)",
            hasAvg ? $"{avgDisp:F2} {unitStr}" : "\u2014",
            xLabel, xValue, y, labelW, valueW, rowH);
        y += rowH;

        DrawRow(ctx, dw, fmt, text, dimmed, "Laps Left",
            lapsLeft.HasValue ? $"{lapsLeft.Value:F1}" : "\u2014",
            xLabel, xValue, y, labelW, valueW, rowH);
        y += rowH;

        // Separator before finish/add section
        ctx.DrawLine(new Vector2(pad, y + rowH * 0.25f), new Vector2(w - pad, y + rowH * 0.25f), sep, 1f);
        y += rowH * 0.5f;

        if (isRace)
        {
            DrawRow(ctx, dw, fmt, text, dimmed, "Needed",
                fuelToFinishDisp.HasValue ? $"{fuelToFinishDisp.Value:F1} {unitStr}" : "\u2014",
                xLabel, xValue, y, labelW, valueW, rowH);
            y += rowH;

            DrawRow(ctx, dw, fmt, text, dimmed, "To Add",
                fuelToAddDisp.HasValue ? $"{fuelToAddDisp.Value:F1} {unitStr}" : "\u2014",
                xLabel, xValue, y, labelW, valueW, rowH);
            y += rowH;

            if (cfg.ShowFuelMargin)
            {
                DrawRow(ctx, dw, fmt, text, dimmed, $"+ Margin ({cfg.FuelSafetyMarginLaps:F1}L)",
                    marginFuelDisp.HasValue ? $"{marginFuelDisp.Value:F1} {unitStr}" : "\u2014",
                    xLabel, xValue, y, labelW, valueW, rowH);
                y += rowH;
            }

            // Double separator before PIT ADD
            ctx.DrawLine(new Vector2(pad, y + 1f), new Vector2(w - pad, y + 1f), sep, 1.5f);
            ctx.DrawLine(new Vector2(pad, y + 3f), new Vector2(w - pad, y + 3f), sep, 1.5f);
            y += 6f;

            DrawRow(ctx, dw, boldFmt, accent, accent, "PIT ADD",
                pitAddTotalDisp.HasValue ? $"{pitAddTotalDisp.Value:F1} {unitStr}" : "\u2014",
                xLabel, xValue, y, labelW, valueW, rowH);
        }
    }

    // в”Ђв”Ђ Helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private static void DrawRow(
        ID2D1RenderTarget ctx, IDWriteFactory dw, IDWriteTextFormat fmt,
        ID2D1Brush labelBrush, ID2D1Brush valueBrush,
        string label, string value,
        float xLabel, float xValue, float y, float labelW, float valueW, float rowH)
    {
        using var ll = dw.CreateTextLayout(label, fmt, labelW, rowH);
        ll.TextAlignment = TextAlignment.Leading;
        ctx.DrawTextLayout(new Vector2(xLabel, y), ll, labelBrush, DrawTextOptions.Clip);

        using var vl = dw.CreateTextLayout(value, fmt, valueW, rowH);
        vl.TextAlignment = TextAlignment.Trailing;
        ctx.DrawTextLayout(new Vector2(xValue, y), vl, valueBrush, DrawTextOptions.Clip);
    }

    private static void DrawL(
        ID2D1RenderTarget ctx, IDWriteFactory dw, IDWriteTextFormat fmt,
        ID2D1Brush brush, string text, float x, float y, float colW, float colH)
    {
        using var layout = dw.CreateTextLayout(text, fmt, colW, colH);
        layout.TextAlignment = TextAlignment.Leading;
        ctx.DrawTextLayout(new Vector2(x, y), layout, brush, DrawTextOptions.Clip);
    }
}



