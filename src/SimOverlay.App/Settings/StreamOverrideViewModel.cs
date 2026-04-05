using System.ComponentModel;
using System.Runtime.CompilerServices;
using SimOverlay.Core.Config;

namespace SimOverlay.App.Settings;

/// <summary>
/// ViewModel for <see cref="StreamOverrideConfig"/>.
/// Each overridable field has a <c>HasXxx</c> bool (= non-null in config) and a value.
/// When <c>HasXxx</c> is false the corresponding input is disabled and shows the
/// inherited base value as a placeholder.
/// </summary>
public sealed class StreamOverrideViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _enabled;
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    // ── Width / Height ────────────────────────────────────────────────────────
    private bool _hasWidth, _hasHeight;
    private int  _width = 300, _height = 200;

    public bool HasWidth  { get => _hasWidth;  set => Set(ref _hasWidth,  value); }
    public bool HasHeight { get => _hasHeight; set => Set(ref _hasHeight, value); }
    public int  Width     { get => _width;     set => Set(ref _width,     value); }
    public int  Height    { get => _height;    set => Set(ref _height,    value); }

    // ── Appearance ────────────────────────────────────────────────────────────
    private bool  _hasOpacity, _hasFontSize, _hasBg, _hasText;
    private float _opacity = 1f, _fontSize = 13f;

    public bool  HasOpacity          { get => _hasOpacity; set => Set(ref _hasOpacity, value); }
    public bool  HasFontSize         { get => _hasFontSize; set => Set(ref _hasFontSize, value); }
    public bool  HasBackgroundColor  { get => _hasBg;   set => Set(ref _hasBg,   value); }
    public bool  HasTextColor        { get => _hasText; set => Set(ref _hasText, value); }
    public float Opacity             { get => _opacity;   set => Set(ref _opacity,   value); }
    public float FontSize            { get => _fontSize;  set => Set(ref _fontSize,  value); }

    public ColorViewModel BackgroundColor { get; } = new();
    public ColorViewModel TextColor       { get; } = new();

    // ── Relative ──────────────────────────────────────────────────────────────
    private bool _hasShowIRating, _hasShowLicense, _hasMaxDrivers, _hasPlayerHL;
    private bool _showIRating = true, _showLicense = true;
    private int  _maxDrivers  = 15;

    public bool HasShowIRating        { get => _hasShowIRating; set => Set(ref _hasShowIRating, value); }
    public bool HasShowLicense        { get => _hasShowLicense; set => Set(ref _hasShowLicense, value); }
    public bool HasMaxDriversShown    { get => _hasMaxDrivers;  set => Set(ref _hasMaxDrivers,  value); }
    public bool HasPlayerHighlight    { get => _hasPlayerHL;    set => Set(ref _hasPlayerHL,    value); }
    public bool ShowIRating           { get => _showIRating;    set => Set(ref _showIRating,    value); }
    public bool ShowLicense           { get => _showLicense;    set => Set(ref _showLicense,    value); }
    public int  MaxDriversShown       { get => _maxDrivers;     set => Set(ref _maxDrivers,     value); }
    public ColorViewModel PlayerHighlightColor { get; } = new();

    // ── Session Info ──────────────────────────────────────────────────────────
    private bool _hasShowWeather, _hasShowDelta, _hasShowGameTime, _hasUse12H, _hasTempUnit;
    private bool _showWeather = true, _showDelta = true, _showGameTime = true, _use12H;
    private TemperatureUnit _tempUnit = TemperatureUnit.Celsius;

    public bool            HasShowWeather    { get => _hasShowWeather;  set => Set(ref _hasShowWeather,  value); }
    public bool            HasShowDelta      { get => _hasShowDelta;    set => Set(ref _hasShowDelta,    value); }
    public bool            HasShowGameTime   { get => _hasShowGameTime; set => Set(ref _hasShowGameTime, value); }
    public bool            HasUse12HourClock { get => _hasUse12H;       set => Set(ref _hasUse12H,       value); }
    public bool            HasTemperatureUnit{ get => _hasTempUnit;     set => Set(ref _hasTempUnit,     value); }
    public bool            ShowWeather       { get => _showWeather;     set => Set(ref _showWeather,     value); }
    public bool            ShowDelta         { get => _showDelta;       set => Set(ref _showDelta,       value); }
    public bool            ShowGameTime      { get => _showGameTime;    set => Set(ref _showGameTime,    value); }
    public bool            Use12HourClock    { get => _use12H;          set => Set(ref _use12H,          value); }
    public TemperatureUnit TemperatureUnit   { get => _tempUnit;        set => Set(ref _tempUnit,        value); }

    // ── Delta Bar ─────────────────────────────────────────────────────────────
    private bool  _hasDeltaMax, _hasFaster, _hasSlower, _hasShowTrend, _hasShowDeltaTxt;
    private float _deltaMax = 2f;
    private bool  _showTrend = true, _showDeltaTxt = true;

    public bool  HasDeltaBarMaxSeconds{ get => _hasDeltaMax;       set => Set(ref _hasDeltaMax,       value); }
    public bool  HasFasterColor       { get => _hasFaster;         set => Set(ref _hasFaster,         value); }
    public bool  HasSlowerColor       { get => _hasSlower;         set => Set(ref _hasSlower,         value); }
    public bool  HasShowTrendArrow    { get => _hasShowTrend;      set => Set(ref _hasShowTrend,      value); }
    public bool  HasShowDeltaText     { get => _hasShowDeltaTxt;   set => Set(ref _hasShowDeltaTxt,   value); }
    public float DeltaBarMaxSeconds   { get => _deltaMax;          set => Set(ref _deltaMax,          value); }
    public bool  ShowTrendArrow       { get => _showTrend;         set => Set(ref _showTrend,         value); }
    public bool  ShowDeltaText        { get => _showDeltaTxt;      set => Set(ref _showDeltaTxt,      value); }
    public ColorViewModel FasterColor { get; } = new();
    public ColorViewModel SlowerColor { get; } = new();

    // ── Load / save ───────────────────────────────────────────────────────────

    public void LoadFrom(StreamOverrideConfig? src, OverlayConfig baseConfig)
    {
        _enabled = src?.Enabled ?? false;

        LoadNullable(src?.Width,   baseConfig.Width,   ref _hasWidth,  ref _width);
        LoadNullable(src?.Height,  baseConfig.Height,  ref _hasHeight, ref _height);
        LoadNullable(src?.Opacity, baseConfig.Opacity, ref _hasOpacity, ref _opacity);
        LoadNullable(src?.FontSize, baseConfig.FontSize, ref _hasFontSize, ref _fontSize);

        _hasBg = src?.BackgroundColor != null;
        BackgroundColor.LoadFrom(src?.BackgroundColor ?? baseConfig.BackgroundColor);
        _hasText = src?.TextColor != null;
        TextColor.LoadFrom(src?.TextColor ?? baseConfig.TextColor);

        LoadNullable(src?.ShowIRating, baseConfig.ShowIRating, ref _hasShowIRating, ref _showIRating);
        LoadNullable(src?.ShowLicense, baseConfig.ShowLicense, ref _hasShowLicense, ref _showLicense);
        LoadNullable(src?.MaxDriversShown, baseConfig.MaxDriversShown, ref _hasMaxDrivers, ref _maxDrivers);
        _hasPlayerHL = src?.PlayerHighlightColor != null;
        PlayerHighlightColor.LoadFrom(src?.PlayerHighlightColor ?? baseConfig.PlayerHighlightColor);

        LoadNullable(src?.ShowWeather,   baseConfig.ShowWeather,   ref _hasShowWeather,  ref _showWeather);
        LoadNullable(src?.ShowDelta,     baseConfig.ShowDelta,     ref _hasShowDelta,    ref _showDelta);
        LoadNullable(src?.ShowGameTime,  baseConfig.ShowGameTime,  ref _hasShowGameTime, ref _showGameTime);
        LoadNullable(src?.Use12HourClock,baseConfig.Use12HourClock,ref _hasUse12H,       ref _use12H);
        LoadNullable(src?.TemperatureUnit, baseConfig.TemperatureUnit, ref _hasTempUnit, ref _tempUnit);

        LoadNullable(src?.DeltaBarMaxSeconds, baseConfig.DeltaBarMaxSeconds, ref _hasDeltaMax, ref _deltaMax);
        _hasFaster = src?.FasterColor != null;
        FasterColor.LoadFrom(src?.FasterColor ?? baseConfig.FasterColor);
        _hasSlower = src?.SlowerColor != null;
        SlowerColor.LoadFrom(src?.SlowerColor ?? baseConfig.SlowerColor);
        LoadNullable(src?.ShowTrendArrow, baseConfig.ShowTrendArrow, ref _hasShowTrend,    ref _showTrend);
        LoadNullable(src?.ShowDeltaText,  baseConfig.ShowDeltaText,  ref _hasShowDeltaTxt, ref _showDeltaTxt);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private static void LoadNullable<T>(T? src, T baseVal, ref bool hasField, ref T field)
        where T : struct
    {
        hasField = src.HasValue;
        field    = src ?? baseVal;
    }

    public StreamOverrideConfig? ToConfig()
    {
        if (!_enabled) return null;

        return new StreamOverrideConfig
        {
            Enabled           = true,
            Width             = _hasWidth  ? _width  : null,
            Height            = _hasHeight ? _height : null,
            Opacity           = _hasOpacity  ? _opacity  : null,
            FontSize          = _hasFontSize ? _fontSize : null,
            BackgroundColor   = _hasBg   ? BackgroundColor.ToColorConfig()   : null,
            TextColor         = _hasText ? TextColor.ToColorConfig()         : null,
            ShowIRating       = _hasShowIRating ? _showIRating : null,
            ShowLicense       = _hasShowLicense ? _showLicense : null,
            MaxDriversShown   = _hasMaxDrivers  ? _maxDrivers  : null,
            PlayerHighlightColor = _hasPlayerHL ? PlayerHighlightColor.ToColorConfig() : null,
            ShowWeather       = _hasShowWeather  ? _showWeather  : null,
            ShowDelta         = _hasShowDelta    ? _showDelta    : null,
            ShowGameTime      = _hasShowGameTime ? _showGameTime : null,
            Use12HourClock    = _hasUse12H       ? _use12H       : null,
            TemperatureUnit   = _hasTempUnit     ? _tempUnit     : null,
            DeltaBarMaxSeconds= _hasDeltaMax     ? _deltaMax     : null,
            FasterColor       = _hasFaster ? FasterColor.ToColorConfig() : null,
            SlowerColor       = _hasSlower ? SlowerColor.ToColorConfig() : null,
            ShowTrendArrow    = _hasShowTrend    ? _showTrend    : null,
            ShowDeltaText     = _hasShowDeltaTxt ? _showDeltaTxt : null,
        };
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
