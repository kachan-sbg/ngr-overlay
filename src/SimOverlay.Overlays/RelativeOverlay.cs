using SimOverlay.Core;
using SimOverlay.Core.Config;
using SimOverlay.Rendering;
using SimOverlay.Sim.Contracts;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace SimOverlay.Overlays;

/// <summary>
/// Relative timing tower — shows up to <c>MaxDriversShown</c> drivers centered on
/// the player by track position, with POS / CAR / NAME / iRTG / LIC / GAP / LAP columns.
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
        Width           = 500,
        Height          = 380,
        Opacity         = 0.85f,
        BackgroundColor = new ColorConfig { R = 0f, G = 0f, B = 0f, A = 0.75f },
        FontSize        = 13f,
        ShowIRating     = false,
        ShowLicense     = false,
        MaxDriversShown = 15,
    };

    private volatile RelativeData? _latest;

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
        var data    = _latest;
        var entries = data?.Entries ?? [];

        var fontSize = config.FontSize;
        var charW    = fontSize * 0.615f;   // Consolas character width approximation
        var rowH     = fontSize + 6f;
        var pad      = 8f;
        var w        = (float)config.Width;
        var h        = (float)config.Height;

        // ----- Column widths -----
        // Fixed columns: POS(4ch) | gap | CAR(5ch) | gap | [iRTG(5ch) | gap] | [LIC(7ch) | gap] | GAP(7ch) | gap | LAP(4ch)
        var posW  = 4f * charW;
        var carW  = 5f * charW;
        var irtgW = config.ShowIRating ? 5f * charW : 0f;
        var licW  = config.ShowLicense ? 7f * charW : 0f;
        var gapW  = 7f * charW;
        var lapW  = 4f * charW;
        var colGap = charW;

        var fixedW = pad + posW + colGap
                         + carW + colGap
                         + (irtgW > 0 ? irtgW + colGap : 0)
                         + (licW  > 0 ? licW  + colGap : 0)
                         + gapW  + colGap
                         + lapW  + pad;

        var nameW = MathF.Max(w - fixedW, 60f);

        // ----- Column left edges -----
        float xPos  = pad;
        float xCar  = xPos  + posW  + colGap;
        float xName = xCar  + carW  + colGap;
        float xIrtg = xName + nameW + colGap;
        float xLic  = xIrtg + (irtgW > 0 ? irtgW + colGap : 0);
        float xGap  = xLic  + (licW  > 0 ? licW  + colGap : 0);
        float xLap  = xGap  + gapW  + colGap;

        var dw     = Resources.WriteFactory;
        var fmt    = Resources.GetTextFormat("Consolas", fontSize);
        var text   = Resources.GetBrush(config.TextColor);
        var dimmed = Resources.GetBrush(config.TextColor.R, config.TextColor.G,
                                         config.TextColor.B, config.TextColor.A * 0.45f);
        var sep    = Resources.GetBrush(config.TextColor.R, config.TextColor.G,
                                         config.TextColor.B, 0.25f);
        var black  = Resources.GetBrush(0f, 0f, 0f, 1f);

        // ----- Header row -----
        float y = pad;
        DrawR(context, dw, fmt, dimmed, "POS",         xPos,  y, posW,  rowH);
        DrawR(context, dw, fmt, dimmed, "CAR",         xCar,  y, carW,  rowH);
        DrawL(context, dw, fmt, dimmed, "DRIVER NAME", xName, y, nameW, rowH);
        if (config.ShowIRating)
            DrawR(context, dw, fmt, dimmed, "iRTG", xIrtg, y, irtgW, rowH);
        if (config.ShowLicense)
            DrawL(context, dw, fmt, dimmed, "LIC",  xLic,  y, licW,  rowH);
        DrawR(context, dw, fmt, dimmed, "GAP",    xGap, y, gapW, rowH);
        DrawR(context, dw, fmt, dimmed, "LAP",    xLap, y, lapW, rowH);

        // ----- Separator -----
        y += rowH + 1f;
        context.DrawLine(new Vector2(pad, y), new Vector2(w - pad, y), sep, 1f);
        y += 3f;

        // ----- Select visible entries centered on player -----
        var visible = SelectVisible(entries, config.MaxDriversShown);

        foreach (var entry in visible)
        {
            if (y + rowH > h - pad) break;

            // Player-row highlight background
            if (entry.IsPlayer)
            {
                var hc = config.PlayerHighlightColor;
                var hl = Resources.GetBrush(hc.R, hc.G, hc.B, hc.A);
                context.FillRectangle(new Vortice.RawRectF(0, y - 1, w, y + rowH + 1), hl);
            }

            // Player arrow marker
            if (entry.IsPlayer)
                DrawL(context, dw, fmt, text, "\u25ba", pad - charW, y, charW + 2f, rowH);

            // POS
            DrawR(context, dw, fmt, text, entry.Position.ToString(), xPos, y, posW, rowH);

            // CAR
            DrawR(context, dw, fmt, text, "#" + entry.CarNumber, xCar, y, carW, rowH);

            // DRIVER NAME (truncated with ellipsis)
            var name = Truncate(entry.DriverName, nameW, charW);
            DrawL(context, dw, fmt, text, name, xName, y, nameW, rowH);

            // iRTG
            if (config.ShowIRating)
            {
                var irtgText = entry.IRating > 0 ? entry.IRating.ToString() : "----";
                DrawR(context, dw, fmt, text, irtgText, xIrtg, y, irtgW, rowH);
            }

            // LIC (colored background cell)
            if (config.ShowLicense && !string.IsNullOrEmpty(entry.LicenseLevel))
            {
                var (lr, lg, lb, la) = entry.LicenseClass.GetColor();
                var licBg = Resources.GetBrush(lr, lg, lb, la);
                context.FillRectangle(new Vortice.RawRectF(xLic, y + 1f, xLic + licW - 2f, y + rowH - 1f), licBg);
                var licText = entry.LicenseClass.RequiresDarkText() ? black : text;
                DrawL(context, dw, fmt, licText, entry.LicenseLevel, xLic + 2f, y, licW - 4f, rowH);
            }

            // GAP
            DrawR(context, dw, fmt, text, FormatGap(entry), xGap, y, gapW, rowH);

            // LAP
            var lapText = entry.LapDifference == 0 ? "0"
                        : entry.LapDifference > 0  ? $"+{entry.LapDifference}"
                        : entry.LapDifference.ToString();
            DrawR(context, dw, fmt, text, lapText, xLap, y, lapW, rowH);

            y += rowH;
        }
    }

    // -------------------------------------------------------------------------
    // Layout helpers
    // -------------------------------------------------------------------------

    // Draw text left-aligned in a column rect.
    private static void DrawL(ID2D1RenderTarget ctx, IDWriteFactory dw, IDWriteTextFormat fmt,
        ID2D1Brush brush, string text, float x, float y, float colW, float colH)
    {
        using var layout = CreateLayout(dw, fmt, text, colW, colH, TextAlignment.Leading);
        ctx.DrawTextLayout(new Vector2(x, y), layout, brush, DrawTextOptions.Clip);
    }

    // Draw text right-aligned in a column rect.
    private static void DrawR(ID2D1RenderTarget ctx, IDWriteFactory dw, IDWriteTextFormat fmt,
        ID2D1Brush brush, string text, float x, float y, float colW, float colH)
    {
        using var layout = CreateLayout(dw, fmt, text, colW, colH, TextAlignment.Trailing);
        ctx.DrawTextLayout(new Vector2(x, y), layout, brush, DrawTextOptions.Clip);
    }

    private static IDWriteTextLayout CreateLayout(IDWriteFactory dw, IDWriteTextFormat fmt,
        string text, float w, float h, TextAlignment alignment)
    {
        var layout = dw.CreateTextLayout(text, fmt, w, h);
        layout.TextAlignment = alignment;
        return layout;
    }

    // -------------------------------------------------------------------------
    // Entry selection — center list on player
    // -------------------------------------------------------------------------

    private static IReadOnlyList<RelativeEntry> SelectVisible(
        IReadOnlyList<RelativeEntry> entries, int maxShown)
    {
        if (entries.Count == 0) return entries;

        int playerIdx = -1;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].IsPlayer) { playerIdx = i; break; }

        if (playerIdx < 0)
            return entries.Count <= maxShown ? entries : entries.Take(maxShown).ToArray();

        int ahead  = maxShown / 2;
        int start  = Math.Max(0, playerIdx - ahead);
        int end    = start + maxShown;

        if (end > entries.Count)
        {
            end   = entries.Count;
            start = Math.Max(0, end - maxShown);
        }

        return entries.Skip(start).Take(end - start).ToArray();
    }

    // -------------------------------------------------------------------------
    // Formatting helpers
    // -------------------------------------------------------------------------

    private static string FormatGap(RelativeEntry entry)
    {
        if (entry.IsPlayer) return " 0.00";
        var gap = entry.GapToPlayerSeconds;
        return gap < 0 ? $"{gap:F2}" : $"+{gap:F2}";
    }

    private static string Truncate(string text, float maxPixels, float charW)
    {
        int maxChars = (int)(maxPixels / charW);
        if (maxChars <= 1) return "";
        if (text.Length <= maxChars) return text;
        return text[..(maxChars - 1)] + "\u2026";
    }
}
