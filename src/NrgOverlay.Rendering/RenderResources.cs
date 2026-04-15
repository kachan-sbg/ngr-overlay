using NrgOverlay.Core.Config;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace NrgOverlay.Rendering;

/// <summary>
/// Lazily creates and caches <see cref="ID2D1SolidColorBrush"/> and
/// <see cref="IDWriteTextFormat"/> objects for a single overlay.
/// Call <see cref="Invalidate"/> when <c>OverlayConfig</c> changes so
/// resources are recreated on the next render frame.
/// </summary>
public sealed class RenderResources : IDisposable
{
    private ID2D1RenderTarget _context;
    private readonly IDWriteFactory _writeFactory;

    private readonly object _lock = new();
    private readonly Dictionary<uint, ID2D1SolidColorBrush> _brushes = new();
    private readonly Dictionary<(string Family, float Size), IDWriteTextFormat> _textFormats = new();

    private bool _disposed;

    public IDWriteFactory WriteFactory => _writeFactory;

    public RenderResources(ID2D1RenderTarget context)
    {
        _context = context;
        _writeFactory = DWrite.DWriteCreateFactory<IDWriteFactory>(Vortice.DirectWrite.FactoryType.Shared);
    }

    // -------------------------------------------------------------------------
    // Brush cache
    // -------------------------------------------------------------------------

    public ID2D1SolidColorBrush GetBrush(ColorConfig color) =>
        GetBrush(color.R, color.G, color.B, color.A);

    public ID2D1SolidColorBrush GetBrush(float r, float g, float b, float a = 1f)
    {
        var key = PackColor(r, g, b, a);

        lock (_lock)
        {
            if (!_brushes.TryGetValue(key, out var brush))
            {
                brush = _context.CreateSolidColorBrush(new Color4(r, g, b, a));
                _brushes[key] = brush;
            }

            return _brushes[key];
        }
    }

    // -------------------------------------------------------------------------
    // Text format cache
    // -------------------------------------------------------------------------

    public IDWriteTextFormat GetTextFormat(string fontFamily, float fontSize)
    {
        var key = (fontFamily, fontSize);

        lock (_lock)
        {
            if (!_textFormats.TryGetValue(key, out var format))
            {
                format = _writeFactory.CreateTextFormat(
                    fontFamily,
                    fontCollection:  null,
                    fontWeight:      FontWeight.Normal,
                    fontStyle:       FontStyle.Normal,
                    fontStretch:     FontStretch.Normal,
                    fontSize:        fontSize,
                    localeName:      "");

                _textFormats[key] = format;
            }

            return _textFormats[key];
        }
    }

    // -------------------------------------------------------------------------
    // Context update вЂ” called after device recovery with the new D2D context
    // -------------------------------------------------------------------------

    /// <summary>
    /// Replaces the D2D device context and invalidates all cached resources.
    /// Must be called after <see cref="OverlayWindow.RecoverDevice"/> so that
    /// subsequent <see cref="GetBrush"/> / <see cref="GetTextFormat"/> calls
    /// use the newly created context rather than the dead one.
    /// </summary>
    public void UpdateContext(ID2D1RenderTarget context)
    {
        lock (_lock)
        {
            _context = context;
            InvalidateCore();
        }
    }

    // -------------------------------------------------------------------------
    // Invalidation вЂ” releases all cached resources; factory is kept alive
    // -------------------------------------------------------------------------

    public void Invalidate()
    {
        lock (_lock)
            InvalidateCore();
    }

    private void InvalidateCore()
    {
        foreach (var brush in _brushes.Values)
            brush.Dispose();
        _brushes.Clear();

        foreach (var format in _textFormats.Values)
            format.Dispose();
        _textFormats.Clear();
    }

    // -------------------------------------------------------------------------
    // Disposal
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        lock (_lock)
            InvalidateCore();
        _writeFactory.Dispose();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static uint PackColor(float r, float g, float b, float a)
    {
        var ri = (uint)(Math.Clamp(r, 0f, 1f) * 255) & 0xFF;
        var gi = (uint)(Math.Clamp(g, 0f, 1f) * 255) & 0xFF;
        var bi = (uint)(Math.Clamp(b, 0f, 1f) * 255) & 0xFF;
        var ai = (uint)(Math.Clamp(a, 0f, 1f) * 255) & 0xFF;
        return (ai << 24) | (ri << 16) | (gi << 8) | bi;
    }
}

