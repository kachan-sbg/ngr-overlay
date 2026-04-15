using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Rendering;
using NrgOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace NrgOverlay.Overlays;

/// <summary>
/// Current weather conditions: air/track temps, wind, humidity, sky, track wetness.
/// Rows with unavailable data are hidden.
/// </summary>
public sealed class WeatherOverlay : BaseOverlay
{
    public const string OverlayId   = "Weather";
    public const string WindowTitle = "NrgOverlay \u2014 Weather";

    public static OverlayConfig DefaultConfig => new()
    {
        Id              = OverlayId,
        Enabled         = true,
        X               = 100,
        Y               = 100,
        Width           = 220,
        Height          = 160,
        Opacity         = 0.85f,
        BackgroundColor = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize        = 13f,
        ShowHumidity    = true,
        ShowWind        = true,
        WindSpeedUnit   = WindSpeedUnit.Kph,
        TemperatureUnit = TemperatureUnit.Celsius,
    };

    private volatile WeatherData? _weather;

    private static readonly WeatherData MockWeather = new(
        AirTempC:         22.1f,
        TrackTempC:       38.7f,
        WindSpeedMps:     12f / 3.6f,
        WindDirectionDeg: 22.5f,   // NNE
        Humidity:         0.45f,
        SkyCoverage:      1,       // partly cloudy
        TrackWetness:     0f,      // dry
        IsPrecipitating:  false);

    public WeatherOverlay(
        ISimDataBus bus,
        OverlayConfig config,
        ConfigStore configStore,
        AppConfig appConfig)
        : base(WindowTitle, config, bus, configStore, appConfig)
    {
        Subscribe<WeatherData>(data => _weather = data);
    }

    protected override void OnRender(ID2D1RenderTarget ctx, OverlayConfig cfg)
    {
        var w = !IsLocked ? MockWeather : _weather;

        float pad    = 8f;
        float width  = (float)cfg.Width;
        float rowH   = cfg.FontSize + 6f;
        var   dw     = Resources.WriteFactory;
        var   fmt    = Resources.GetTextFormat("Oswald", cfg.FontSize);
        var   hdrFmt = Resources.GetTextFormat("Oswald", cfg.FontSize - 1f);
        var   text   = Resources.GetBrush(cfg.TextColor);
        var   dimmed = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                           cfg.TextColor.B, cfg.TextColor.A * 0.45f);
        var   sep    = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                           cfg.TextColor.B, 0.25f);

        float innerW = width - 2f * pad;
        float labelW = innerW * 0.50f;
        float valueW = innerW - labelW;
        float xL = pad, xV = pad + labelW;
        float y = pad;

        // Header
        DrawL(ctx, dw, hdrFmt, dimmed, "WEATHER", xL, y, innerW, rowH);
        y += rowH - 2f;
        ctx.DrawLine(new Vector2(pad, y), new Vector2(width - pad, y), sep, 1f);
        y += 4f;

        if (w is null) return;

        bool celsius = cfg.TemperatureUnit == TemperatureUnit.Celsius;

        // Air temp вЂ” always shown
        float airDisp   = celsius ? w.AirTempC   : w.AirTempC   * 1.8f + 32f;
        float trackDisp = celsius ? w.TrackTempC : w.TrackTempC * 1.8f + 32f;
        string unit = celsius ? "\u00b0C" : "\u00b0F";
        DrawRow(ctx, dw, fmt, text, dimmed, "Air",   $"{airDisp:F1}{unit}",   xL, xV, y, labelW, valueW, rowH); y += rowH;
        DrawRow(ctx, dw, fmt, text, dimmed, "Track", $"{trackDisp:F1}{unit}", xL, xV, y, labelW, valueW, rowH); y += rowH;

        // Wind
        if (cfg.ShowWind && w.WindSpeedMps > 0f)
        {
            float windDisp;
            string windUnit;
            switch (cfg.WindSpeedUnit)
            {
                case WindSpeedUnit.Mph: windDisp = w.WindSpeedMps * 2.23694f; windUnit = "mph"; break;
                case WindSpeedUnit.Ms:  windDisp = w.WindSpeedMps;            windUnit = "m/s"; break;
                default:                windDisp = w.WindSpeedMps * 3.6f;    windUnit = "km/h"; break;
            }
            string compass = ToCompass(w.WindDirectionDeg);
            DrawRow(ctx, dw, fmt, text, dimmed, "Wind",
                $"{windDisp:F0} {windUnit}  {compass}", xL, xV, y, labelW, valueW, rowH);
            y += rowH;
        }

        // Humidity
        if (cfg.ShowHumidity && w.Humidity > 0f)
        {
            DrawRow(ctx, dw, fmt, text, dimmed, "Humidity",
                $"{w.Humidity * 100f:F0}%", xL, xV, y, labelW, valueW, rowH);
            y += rowH;
        }

        // Sky
        string skyText = w.SkyCoverage switch
        {
            null => "??",
            0    => "Clear",
            1    => "Partly \u2601",
            2    => "Mostly \u2601",
            _    => "Overcast",
        };
        DrawRow(ctx, dw, fmt, text, dimmed, "Sky", skyText, xL, xV, y, labelW, valueW, rowH);
        y += rowH;

        // Track condition
        string trackWet = w.TrackWetness switch
        {
            < 0.05f => "Dry",
            < 0.25f => "Damp",
            < 0.55f => "Wet",
            < 0.80f => "Very Wet",
            _       => "Flooded",
        };
        if (w.IsPrecipitating) trackWet += " \ud83c\udf27";
        DrawRow(ctx, dw, fmt, text, dimmed, "Track", trackWet, xL, xV, y, labelW, valueW, rowH);
    }

    // в”Ђв”Ђ Helpers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

    private static string ToCompass(float deg)
    {
        string[] dirs = ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
                         "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"];
        int idx = (int)MathF.Round(((deg % 360f) + 360f) / 22.5f) % 16;
        return dirs[idx];
    }

    private static void DrawRow(
        ID2D1RenderTarget ctx, IDWriteFactory dw, IDWriteTextFormat fmt,
        ID2D1Brush lb, ID2D1Brush vb,
        string label, string value,
        float xL, float xV, float y, float lw, float vw, float rh)
    {
        using var ll = dw.CreateTextLayout(label, fmt, lw, rh);
        ll.TextAlignment = TextAlignment.Leading;
        ctx.DrawTextLayout(new Vector2(xL, y), ll, lb, DrawTextOptions.Clip);

        using var vl = dw.CreateTextLayout(value, fmt, vw, rh);
        vl.TextAlignment = TextAlignment.Trailing;
        ctx.DrawTextLayout(new Vector2(xV, y), vl, vb, DrawTextOptions.Clip);
    }

    private static void DrawL(
        ID2D1RenderTarget ctx, IDWriteFactory dw, IDWriteTextFormat fmt,
        ID2D1Brush brush, string text, float x, float y, float cw, float ch)
    {
        using var layout = dw.CreateTextLayout(text, fmt, cw, ch);
        layout.TextAlignment = TextAlignment.Leading;
        ctx.DrawTextLayout(new Vector2(x, y), layout, brush, DrawTextOptions.Clip);
    }
}



