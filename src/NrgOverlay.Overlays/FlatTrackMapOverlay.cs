using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Rendering;
using NrgOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace NrgOverlay.Overlays;

/// <summary>
/// Horizontal flat (linear) track map.  Each car is placed at its LapDistPct on a bar
/// spanning the full overlay width.  Player gets a larger, highlighted marker.
/// Multi-class cars are tinted with their class colour.
/// </summary>
public sealed class FlatTrackMapOverlay : BaseOverlay
{
    public const string OverlayId   = "FlatTrackMap";
    public const string WindowTitle = "NrgOverlay \u2014 Track Map";

    public static OverlayConfig DefaultConfig => new()
    {
        Id               = OverlayId,
        Enabled          = true,
        X                = 100,
        Y                = 100,
        Width            = 400,
        Height           = 60,
        Opacity          = 0.85f,
        BackgroundColor  = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize         = 11f,
        FlatMapLabelMode = FlatMapLabelMode.CarNumber,
        PlayerMarkerSize = 8f,
        CarMarkerSize    = 4f,
        ShowPitCars      = true,
    };

    private volatile TrackMapData? _trackMap;

    // в”Ђв”Ђ Mock data в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private static readonly TrackMapData MockData = BuildMock();

    private static TrackMapData BuildMock()
    {
        var gtpColor = new ColorConfig { R = 0.8f, G = 0.2f, B = 0.2f, A = 1f };
        var gt3Color = new ColorConfig { R = 0.2f, G = 0.7f, B = 0.3f, A = 1f };
        var cars = new List<TrackMapCarEntry>
        {
            new(0,  "91", 1,  0.04f, "GTP", false, false),
            new(1,  "92", 2,  0.12f, "GTP", false, false),
            new(2,  "3",  3,  0.19f, "GTP", false, false),
            new(3,  "4",  4,  0.26f, "GTP", true,  false),   // player
            new(4,  "62", 5,  0.33f, "GTP", false, false),
            new(5,  "77", 6,  0.41f, "GT3", false, false),
            new(6,  "31", 7,  0.50f, "GT3", false, false),
            new(7,  "12", 8,  0.58f, "GT3", false, false),
            new(8,  "88", 9,  0.66f, "GT3", false, false),
            new(9,  "55", 10, 0.73f, "GT3", false, false),
            new(10, "22", 11, 0.81f, "GT3", false, true),    // in pit
            new(11, "18", 12, 0.90f, "GT3", false, false),
        };
        return new TrackMapData(6000f, cars);
    }

    // For mock rendering, we synthesise class colours from class name
    private static ColorConfig MockClassColor(string cls) => cls switch
    {
        "GTP"  => new ColorConfig { R = 0.8f, G = 0.2f, B = 0.2f, A = 1f },
        "LMP2" => new ColorConfig { R = 0.2f, G = 0.5f, B = 1.0f, A = 1f },
        "GT3"  => new ColorConfig { R = 0.2f, G = 0.7f, B = 0.3f, A = 1f },
        _      => ColorConfig.White,
    };

    public FlatTrackMapOverlay(
        ISimDataBus bus,
        OverlayConfig config,
        ConfigStore configStore,
        AppConfig appConfig)
        : base(WindowTitle, config, bus, configStore, appConfig)
    {
        Subscribe<TrackMapData>(data => _trackMap = data);
    }

    protected override void OnRender(ID2D1RenderTarget ctx, OverlayConfig cfg)
    {
        var data = !IsLocked ? MockData : _trackMap;

        float pad  = 8f;
        float w    = (float)cfg.Width;
        float h    = (float)cfg.Height;

        // в”Ђв”Ђ Track bar geometry в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        float barH   = 4f;
        float barY   = h / 2f;
        float barX0  = pad;
        float barX1  = w - pad;
        float barW   = barX1 - barX0;

        float labelH  = cfg.FontSize + 2f;
        bool  hasLabel = cfg.FlatMapLabelMode != FlatMapLabelMode.None;

        // Label rows above and below the bar (alternating to reduce collision)
        float aboveY  = barY - barH / 2f - labelH - 2f;
        float belowY  = barY + barH / 2f + 2f;

        var dw       = Resources.WriteFactory;
        var fmt      = Resources.GetTextFormat("Oswald", cfg.FontSize);
        var trackBrush = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                             cfg.TextColor.B, 0.35f);
        var sfBrush  = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                           cfg.TextColor.B, 0.70f);
        var pitBrush = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                           cfg.TextColor.B, 0.25f);
        var dimmed   = Resources.GetBrush(cfg.TextColor.R, cfg.TextColor.G,
                                           cfg.TextColor.B, 0.40f);

        // Track bar
        ctx.FillRectangle(new Vortice.RawRectF(barX0, barY - barH / 2f, barX1, barY + barH / 2f), trackBrush);

        // S/F line
        float sfX = barX0;
        ctx.DrawLine(new Vector2(sfX, barY - 8f), new Vector2(sfX, barY + 8f), sfBrush, 2f);

        if (data is null || data.Cars.Count == 0)
        {
            using var noData = dw.CreateTextLayout("No data", fmt, barW, h);
            noData.TextAlignment      = TextAlignment.Center;
            noData.ParagraphAlignment = ParagraphAlignment.Center;
            ctx.DrawTextLayout(new Vector2(pad, 0), noData, dimmed, DrawTextOptions.Clip);
            return;
        }

        // в”Ђв”Ђ Car markers в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
        bool isMultiClass = data.Cars.Any(c => !string.IsNullOrEmpty(c.CarClass));

        // Sort by pct so we can alternate above/below labels in order
        var sorted = data.Cars
            .Where(c => c.IsInPit ? cfg.ShowPitCars : true)
            .OrderBy(c => c.LapDistPct)
            .ToList();

        int labelRow = 0; // alternates 0=above, 1=below
        float lastLabelXAbove = float.MinValue;
        float lastLabelXBelow = float.MinValue;

        foreach (var car in sorted)
        {
            float cx = barX0 + car.LapDistPct * barW;
            bool  inPit = car.IsInPit;

            ColorConfig classColor = isMultiClass
                ? (!IsLocked ? MockClassColor(car.CarClass) : ColorConfig.White)
                : ColorConfig.White;

            float markerH = car.IsPlayer ? cfg.PlayerMarkerSize : cfg.CarMarkerSize;
            float markerHalf = markerH / 2f;

            ID2D1Brush markerBrush;
            if (inPit)
            {
                markerBrush = pitBrush;
            }
            else if (car.IsPlayer)
            {
                var hc = cfg.PlayerHighlightColor;
                markerBrush = Resources.GetBrush(hc.R, hc.G, hc.B, 1f);
            }
            else
            {
                markerBrush = Resources.GetBrush(classColor.R, classColor.G, classColor.B, 0.9f);
            }

            // Vertical tick
            ctx.DrawLine(
                new Vector2(cx, barY - markerHalf),
                new Vector2(cx, barY + markerHalf),
                markerBrush, car.IsPlayer ? 3f : 2f);

            // Label
            if (hasLabel)
            {
                string labelStr = cfg.FlatMapLabelMode == FlatMapLabelMode.Position
                    ? car.Position.ToString()
                    : car.CarNumber;

                // Alternate above/below; collapse if too close to last label
                float labelW = labelStr.Length * cfg.FontSize * 0.615f + 4f;
                float labelX = cx - labelW / 2f;

                bool above = labelRow % 2 == 0;
                float ly   = above ? aboveY : belowY;

                // Skip if overlapping with previous label on same row
                float minGap = cfg.FontSize * 1.2f;
                float lastRowX = above ? lastLabelXAbove : lastLabelXBelow;
                if (cx - lastRowX > minGap)
                {
                    var   lBrush = inPit ? dimmed : markerBrush;
                    using var ll = dw.CreateTextLayout(labelStr, fmt, labelW + 4f, labelH);
                    ll.TextAlignment = TextAlignment.Center;
                    ctx.DrawTextLayout(new Vector2(labelX, ly), ll, lBrush, DrawTextOptions.Clip);
                    if (above) lastLabelXAbove = cx;
                    else lastLabelXBelow = cx;
                }

                labelRow++;
            }
        }
    }
}



