using System.ComponentModel;
using System.Runtime.CompilerServices;
using SimOverlay.Core.Config;

namespace SimOverlay.App.Settings;

/// <summary>
/// Editable ViewModel copy of <see cref="OverlayConfig"/>.
/// Always a detached clone — never mutates the live config directly.
/// Call <see cref="LoadFrom"/> to populate, <see cref="ToConfig"/> to produce a
/// new config snapshot for <c>PreviewConfig</c> / <c>ApplyConfig</c>.
/// </summary>
public sealed class OverlayConfigViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Identity ─────────────────────────────────────────────────────────────
    private string _id = "";
    private bool   _enabled;

    public string Id      { get => _id;      set => Set(ref _id,      value); }
    public bool   Enabled { get => _enabled; set => Set(ref _enabled, value); }

    // ── Position & size ───────────────────────────────────────────────────────
    private int _x, _y, _width = 300, _height = 200;

    public int X      { get => _x;       set => Set(ref _x,       value); }
    public int Y      { get => _y;       set => Set(ref _y,       value); }
    public int Width  { get => _width;   set => Set(ref _width,   value); }
    public int Height { get => _height;  set => Set(ref _height,  value); }

    // ── Appearance ────────────────────────────────────────────────────────────
    private float _opacity   = 1f;
    private float _fontSize  = 13f;

    public float Opacity  { get => _opacity;  set => Set(ref _opacity,  value); }
    public float FontSize { get => _fontSize; set => Set(ref _fontSize, value); }

    public ColorViewModel BackgroundColor    { get; } = new();
    public ColorViewModel TextColor         { get; } = new();

    // ── Relative-specific ─────────────────────────────────────────────────────
    private bool _showIRating  = true;
    private bool _showLicense  = true;
    private int  _maxDrivers   = 15;

    public bool ShowIRating    { get => _showIRating;  set => Set(ref _showIRating,  value); }
    public bool ShowLicense    { get => _showLicense;  set => Set(ref _showLicense,  value); }
    public int  MaxDriversShown{ get => _maxDrivers;   set => Set(ref _maxDrivers,   value); }

    public ColorViewModel PlayerHighlightColor { get; } = new();

    // ── Session Info-specific ─────────────────────────────────────────────────
    private bool            _showWeather   = true;
    private bool            _showDelta     = true;
    private bool            _showGameTime  = true;
    private bool            _use12Hour;
    private TemperatureUnit _tempUnit      = TemperatureUnit.Celsius;

    public bool            ShowWeather    { get => _showWeather;  set => Set(ref _showWeather,  value); }
    public bool            ShowDelta      { get => _showDelta;    set => Set(ref _showDelta,    value); }
    public bool            ShowGameTime   { get => _showGameTime; set => Set(ref _showGameTime, value); }
    public bool            Use12HourClock { get => _use12Hour;    set => Set(ref _use12Hour,    value); }
    public TemperatureUnit TemperatureUnit{ get => _tempUnit;     set => Set(ref _tempUnit,     value); }

    // ── Delta Bar-specific ────────────────────────────────────────────────────
    private float _deltaBarMax   = 2f;
    private bool  _showTrend     = true;
    private bool  _showDeltaTxt  = true;

    public float DeltaBarMaxSeconds { get => _deltaBarMax;  set => Set(ref _deltaBarMax,  value); }
    public bool  ShowTrendArrow     { get => _showTrend;    set => Set(ref _showTrend,    value); }
    public bool  ShowDeltaText      { get => _showDeltaTxt; set => Set(ref _showDeltaTxt, value); }

    public ColorViewModel FasterColor { get; } = new();
    public ColorViewModel SlowerColor { get; } = new();

    // ── Stream override ───────────────────────────────────────────────────────
    public StreamOverrideViewModel StreamOverride { get; } = new();

    // ── Load / save ───────────────────────────────────────────────────────────

    public void LoadFrom(OverlayConfig c)
    {
        _id      = c.Id;
        _enabled = c.Enabled;
        _x = c.X;  _y = c.Y;
        _width  = c.Width;  _height = c.Height;
        _opacity  = c.Opacity;
        _fontSize = c.FontSize;

        BackgroundColor.LoadFrom(c.BackgroundColor);
        TextColor.LoadFrom(c.TextColor);

        _showIRating = c.ShowIRating;
        _showLicense = c.ShowLicense;
        _maxDrivers  = c.MaxDriversShown;
        PlayerHighlightColor.LoadFrom(c.PlayerHighlightColor);

        _showWeather  = c.ShowWeather;
        _showDelta    = c.ShowDelta;
        _showGameTime = c.ShowGameTime;
        _use12Hour    = c.Use12HourClock;
        _tempUnit     = c.TemperatureUnit;

        _deltaBarMax  = c.DeltaBarMaxSeconds;
        _showTrend    = c.ShowTrendArrow;
        _showDeltaTxt = c.ShowDeltaText;
        FasterColor.LoadFrom(c.FasterColor);
        SlowerColor.LoadFrom(c.SlowerColor);

        StreamOverride.LoadFrom(c.StreamOverride, c);

        // Notify all at once after bulk load.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public OverlayConfig ToConfig() => new()
    {
        Id      = _id,
        Enabled = _enabled,
        X = _x,  Y = _y,
        Width  = _width,  Height = _height,
        Opacity  = _opacity,
        FontSize = _fontSize,
        BackgroundColor    = BackgroundColor.ToColorConfig(),
        TextColor          = TextColor.ToColorConfig(),
        ShowIRating        = _showIRating,
        ShowLicense        = _showLicense,
        MaxDriversShown    = _maxDrivers,
        PlayerHighlightColor = PlayerHighlightColor.ToColorConfig(),
        ShowWeather        = _showWeather,
        ShowDelta          = _showDelta,
        ShowGameTime       = _showGameTime,
        Use12HourClock     = _use12Hour,
        TemperatureUnit    = _tempUnit,
        DeltaBarMaxSeconds = _deltaBarMax,
        ShowTrendArrow     = _showTrend,
        ShowDeltaText      = _showDeltaTxt,
        FasterColor        = FasterColor.ToColorConfig(),
        SlowerColor        = SlowerColor.ToColorConfig(),
        StreamOverride     = StreamOverride.ToConfig(),
    };

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
