using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Rendering;
using SimOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace SimOverlay.Overlays;

/// <summary>
/// Pit road assistance overlay. Shows pit limiter status and speed compliance on pit road;
/// compact mode (stop count + next stop estimate) when not on pit road.
/// </summary>
public sealed class PitHelperOverlay : BaseOverlay
{
    public const string OverlayId   = "PitHelper";
    public const string WindowTitle = "SimOverlay \u2014 Pit";

    public static OverlayConfig DefaultConfig => new()
    {
        Id                   = OverlayId,
        Enabled              = true,
        X                    = 100,
        Y                    = 100,
        Width                = 280,
        Height               = 180,
        Opacity              = 0.85f,
        BackgroundColor      = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize             = 13f,
        ShowPitServices      = true,
        ShowNextPitEstimate  = true,
        SpeedUnit            = SpeedUnit.Kph,
    };

    private volatile PitData?      _pit;
    private volatile TelemetryData? _telemetry;

    // Mock: on pit road, 60 km/h limit, fuel + tires requested
    private static readonly PitData MockPitOnRoad = new(
        IsOnPitRoad:        true,
        IsInPitStall:       false,
        PitLimiterSpeedMps: 60f / 3.6f,
        CurrentSpeedMps:    58.3f / 3.6f,
        PitLimiterActive:   true,
        PitStopCount:       2,
        RequestedService:   PitServiceFlags.Fuel | PitServiceFlags.AllTires,
        FuelToAddLiters:    18.7f);

    private static readonly TelemetryData MockTelemetry = new(
        Throttle: 0, Brake: 0, Clutch: 0, SteeringAngle: 0,
        SpeedMps: 58.3f / 3.6f, Gear: 1, Rpm: 2000,
        FuelLevelLiters: 12.4f,
        FuelConsumptionPerLap: 2.83f,
        IncidentCount: 0);

    public PitHelperOverlay(
        ISimDataBus bus,
        OverlayConfig config,
        ConfigStore configStore,
        AppConfig appConfig)
        : base(WindowTitle, config, bus, configStore, appConfig)
    {
        Subscribe<PitData>(data => _pit = data);
        Subscribe<TelemetryData>(data => _telemetry = data);
    }

    protected override void OnRender(ID2D1RenderTarget ctx, OverlayConfig cfg)
    {
        bool edit   = !IsLocked;
        var  pit    = edit ? MockPitOnRoad : _pit;
        var  telem  = edit ? MockTelemetry : _telemetry;

        float pad   = 8f;
        float w     = (float)cfg.Width;
        float h     = (float)cfg.Height;
        float rowH  = cfg.FontSize + 6f;
        var   dw    = Resources.WriteFactory;
        var   fmt   = Resources.GetTextFormat("Consolas", cfg.FontSize);
        var   bigFmt = Resources.GetTextFormat("Consolas", MathF.Max(cfg.FontSize * 1.4f, 18f));
        var   text  = Resources.GetBrush(cfg.TextColor);
        var   dimmed = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                           cfg.TextColor.B, cfg.TextColor.A * 0.45f);
        var   sep   = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                          cfg.TextColor.B, 0.25f);
        var   green = Resources.GetBrush(0f, 0.87f, 0f, 1f);
        var   red   = Resources.GetBrush(0.87f, 0.13f, 0.13f, 1f);

        float innerW = w - 2f * pad;
        float labelW = innerW * 0.55f;
        float valueW = innerW - labelW;
        float xL = pad, xV = pad + labelW;

        float y = pad;

        bool onPitRoad = pit?.IsOnPitRoad ?? false;

        if (onPitRoad)
        {
            // ── PIT ROAD header ───────────────────────────────────────────────
            using var titleLayout = dw.CreateTextLayout("PIT ROAD", bigFmt, innerW, rowH + 6f);
            titleLayout.TextAlignment = TextAlignment.Center;
            ctx.DrawTextLayout(new Vector2(pad, y), titleLayout, text, DrawTextOptions.Clip);
            y += rowH + 6f;

            // Double separator
            ctx.DrawLine(new Vector2(pad, y), new Vector2(w - pad, y), sep, 2f);
            ctx.DrawLine(new Vector2(pad, y + 2f), new Vector2(w - pad, y + 2f), sep, 2f);
            y += 6f;

            // ── Speed limit (hide if 0 = unavailable) ────────────────────────
            bool limitKnown = (pit?.PitLimiterSpeedMps ?? 0f) > 0.1f;
            if (limitKnown)
            {
                float limitMps  = pit!.PitLimiterSpeedMps;
                float speedMps  = pit.CurrentSpeedMps;
                bool  kph       = cfg.SpeedUnit == SpeedUnit.Kph;
                float factor    = kph ? 3.6f : 2.23694f;
                string unitStr  = kph ? "km/h" : "mph";
                string limitStr = $"{limitMps * factor:F1} {unitStr}";
                string speedStr = $"{speedMps * factor:F1} {unitStr}";
                bool   ok       = speedMps <= limitMps + 0.5f;
                var    indBrush = ok ? green : red;
                string indStr   = ok ? "\u2713" : "\u2717"; // ✓ / ✗

                DrawRow(ctx, dw, fmt, text, dimmed, "Limit", limitStr, xL, xV, y, labelW, valueW, rowH);
                y += rowH;

                // Speed row with compliance indicator
                DrawRow(ctx, dw, fmt, text, dimmed, "Speed", speedStr, xL, xV, y, labelW, valueW, rowH);
                float indX = w - pad - cfg.FontSize * 1.5f;
                using var indLayout = dw.CreateTextLayout(indStr, fmt, cfg.FontSize * 1.5f, rowH);
                indLayout.TextAlignment = TextAlignment.Trailing;
                ctx.DrawTextLayout(new Vector2(indX, y), indLayout, indBrush, DrawTextOptions.Clip);
                y += rowH;

                ctx.DrawLine(new Vector2(pad, y + 1f), new Vector2(w - pad, y + 1f), sep, 1f);
                y += 5f;
            }

            // ── Service indicators ────────────────────────────────────────────
            if (cfg.ShowPitServices && pit != null)
            {
                var svc   = pit.RequestedService;
                var parts = new List<string>(6);
                if (svc.HasFlag(PitServiceFlags.Fuel))             parts.Add("Fuel");
                if ((svc & PitServiceFlags.AllTires) != 0)        parts.Add("Tires");
                if (svc.HasFlag(PitServiceFlags.WindshieldTearoff)) parts.Add("Tearoff");
                if (svc.HasFlag(PitServiceFlags.FastRepair))       parts.Add("Fast Rep");

                if (parts.Count > 0)
                {
                    string svcText = string.Join("  ", parts);
                    DrawRow(ctx, dw, fmt, text, dimmed, "Service:", svcText, xL, xV, y, labelW, valueW, rowH);
                    y += rowH;
                }

                // Fuel to add
                if (svc.HasFlag(PitServiceFlags.Fuel) && pit.FuelToAddLiters > 0.1f)
                {
                    float dispFuel = pit.FuelToAddLiters;
                    DrawRow(ctx, dw, fmt, text, dimmed, "Fuel Add",
                        $"{dispFuel:F1} L", xL, xV, y, labelW, valueW, rowH);
                    y += rowH;
                }

                ctx.DrawLine(new Vector2(pad, y + 1f), new Vector2(w - pad, y + 1f), sep, 1f);
                y += 5f;
            }
        }

        // ── Pit stop count (always shown) ─────────────────────────────────────
        DrawRow(ctx, dw, fmt, text, dimmed, "Pit Stops",
            (pit?.PitStopCount ?? 0).ToString(), xL, xV, y, labelW, valueW, rowH);
        y += rowH;

        // ── Next stop estimate (not on pit road) ──────────────────────────────
        if (cfg.ShowNextPitEstimate && !onPitRoad && telem != null)
        {
            float fuel = telem.FuelLevelLiters;
            float avg  = telem.FuelConsumptionPerLap;
            if (avg > 0.001f)
            {
                float lapsLeft = fuel / avg;
                string estText = $"~{lapsLeft:F1} laps";
                DrawRow(ctx, dw, fmt, text, dimmed, "Next stop in", estText, xL, xV, y, labelW, valueW, rowH);
            }
        }
    }

    private static void DrawRow(
        ID2D1RenderTarget ctx, IDWriteFactory dw, IDWriteTextFormat fmt,
        ID2D1Brush labelBrush, ID2D1Brush valueBrush,
        string label, string value,
        float xL, float xV, float y, float labelW, float valueW, float rowH)
    {
        using var ll = dw.CreateTextLayout(label, fmt, labelW, rowH);
        ll.TextAlignment = TextAlignment.Leading;
        ctx.DrawTextLayout(new Vector2(xL, y), ll, labelBrush, DrawTextOptions.Clip);

        using var vl = dw.CreateTextLayout(value, fmt, valueW, rowH);
        vl.TextAlignment = TextAlignment.Trailing;
        ctx.DrawTextLayout(new Vector2(xV, y), vl, valueBrush, DrawTextOptions.Clip);
    }
}
