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

    // ── Pit Helper ───────────────────────────────────────────────────────────
    private bool _hasShowPitServices, _hasShowNextPit;
    private bool _showPitServices = true, _showNextPit = true;

    public bool HasShowPitServices     { get => _hasShowPitServices; set => Set(ref _hasShowPitServices, value); }
    public bool HasShowNextPitEstimate { get => _hasShowNextPit;     set => Set(ref _hasShowNextPit,     value); }
    public bool ShowPitServices        { get => _showPitServices;    set => Set(ref _showPitServices,    value); }
    public bool ShowNextPitEstimate    { get => _showNextPit;        set => Set(ref _showNextPit,        value); }

    // ── Weather ───────────────────────────────────────────────────────────────
    private bool          _hasShowHumidity, _hasShowWind, _hasWindSpeedUnit;
    private bool          _showHumidity = true, _showWind = true;
    private WindSpeedUnit _windSpeedUnit = WindSpeedUnit.Kph;

    public bool          HasShowHumidity  { get => _hasShowHumidity;  set => Set(ref _hasShowHumidity,  value); }
    public bool          HasShowWind      { get => _hasShowWind;      set => Set(ref _hasShowWind,      value); }
    public bool          HasWindSpeedUnit { get => _hasWindSpeedUnit; set => Set(ref _hasWindSpeedUnit, value); }
    public bool          ShowHumidity     { get => _showHumidity;     set => Set(ref _showHumidity,     value); }
    public bool          ShowWind         { get => _showWind;         set => Set(ref _showWind,         value); }
    public WindSpeedUnit WindSpeedUnit    { get => _windSpeedUnit;    set => Set(ref _windSpeedUnit,    value); }

    // ── Flat Track Map ────────────────────────────────────────────────────────
    private bool             _hasFlatMapLabel, _hasPlayerMarker, _hasCarMarker, _hasShowPitCars;
    private FlatMapLabelMode _flatMapLabel = FlatMapLabelMode.CarNumber;
    private float            _playerMarker = 8f, _carMarker = 4f;
    private bool             _showPitCars = true;

    public bool             HasFlatMapLabelMode { get => _hasFlatMapLabel;   set => Set(ref _hasFlatMapLabel,   value); }
    public bool             HasPlayerMarkerSize { get => _hasPlayerMarker;   set => Set(ref _hasPlayerMarker,   value); }
    public bool             HasCarMarkerSize    { get => _hasCarMarker;      set => Set(ref _hasCarMarker,      value); }
    public bool             HasShowPitCars      { get => _hasShowPitCars;    set => Set(ref _hasShowPitCars,    value); }
    public FlatMapLabelMode FlatMapLabelMode    { get => _flatMapLabel;      set => Set(ref _flatMapLabel,      value); }
    public float            PlayerMarkerSize    { get => _playerMarker;      set => Set(ref _playerMarker,      value); }
    public float            CarMarkerSize       { get => _carMarker;         set => Set(ref _carMarker,         value); }
    public bool             ShowPitCars         { get => _showPitCars;       set => Set(ref _showPitCars,       value); }

    // ── Standings ─────────────────────────────────────────────────────────────
    private bool _hasStandingsMode, _hasShowClassBadge, _hasShowBestLap, _hasMaxStandingsRows;
    private StandingsDisplayMode _standingsMode = StandingsDisplayMode.Combined;
    private bool _showClassBadge = true, _showBestLap = true;
    private int  _maxStandingsRows = 30;

    public bool                 HasStandingsDisplayMode { get => _hasStandingsMode;       set => Set(ref _hasStandingsMode,       value); }
    public bool                 HasShowClassBadge       { get => _hasShowClassBadge;      set => Set(ref _hasShowClassBadge,      value); }
    public bool                 HasShowBestLap          { get => _hasShowBestLap;         set => Set(ref _hasShowBestLap,         value); }
    public bool                 HasMaxStandingsRows     { get => _hasMaxStandingsRows;    set => Set(ref _hasMaxStandingsRows,    value); }
    public StandingsDisplayMode StandingsDisplayMode    { get => _standingsMode;          set => Set(ref _standingsMode,          value); }
    public bool                 ShowClassBadge          { get => _showClassBadge;         set => Set(ref _showClassBadge,         value); }
    public bool                 ShowBestLap             { get => _showBestLap;            set => Set(ref _showBestLap,            value); }
    public int                  MaxStandingsRows        { get => _maxStandingsRows;       set => Set(ref _maxStandingsRows,       value); }

    // ── Fuel Calculator ───────────────────────────────────────────────────────
    private bool     _hasFuelUnit, _hasFuelMarginLaps, _hasShowFuelMargin;
    private FuelUnit _fuelUnit = FuelUnit.Liters;
    private float    _fuelMarginLaps = 1.0f;
    private bool     _showFuelMargin = true;

    public bool     HasFuelUnit             { get => _hasFuelUnit;        set => Set(ref _hasFuelUnit,        value); }
    public bool     HasFuelSafetyMarginLaps { get => _hasFuelMarginLaps;  set => Set(ref _hasFuelMarginLaps,  value); }
    public bool     HasShowFuelMargin       { get => _hasShowFuelMargin;  set => Set(ref _hasShowFuelMargin,  value); }
    public FuelUnit FuelUnit                { get => _fuelUnit;           set => Set(ref _fuelUnit,           value); }
    public float    FuelSafetyMarginLaps    { get => _fuelMarginLaps;     set => Set(ref _fuelMarginLaps,     value); }
    public bool     ShowFuelMargin          { get => _showFuelMargin;     set => Set(ref _showFuelMargin,     value); }

    // ── Input Telemetry ───────────────────────────────────────────────────────
    private bool      _hasShowThrottle, _hasShowBrake, _hasShowClutch;
    private bool      _hasShowInputTrace, _hasShowGearSpeed, _hasSpeedUnit;
    private bool      _hasThrottleColor, _hasBrakeColor, _hasClutchColor;
    private bool      _showThrottle = true, _showBrake = true, _showClutch = true;
    private bool      _showInputTrace = true, _showGearSpeed = true;
    private SpeedUnit _speedUnit = SpeedUnit.Kph;

    public bool      HasShowThrottle   { get => _hasShowThrottle;   set => Set(ref _hasShowThrottle,   value); }
    public bool      HasShowBrake      { get => _hasShowBrake;      set => Set(ref _hasShowBrake,      value); }
    public bool      HasShowClutch     { get => _hasShowClutch;     set => Set(ref _hasShowClutch,     value); }
    public bool      HasShowInputTrace { get => _hasShowInputTrace; set => Set(ref _hasShowInputTrace, value); }
    public bool      HasShowGearSpeed  { get => _hasShowGearSpeed;  set => Set(ref _hasShowGearSpeed,  value); }
    public bool      HasSpeedUnit      { get => _hasSpeedUnit;      set => Set(ref _hasSpeedUnit,      value); }
    public bool      HasThrottleColor  { get => _hasThrottleColor;  set => Set(ref _hasThrottleColor,  value); }
    public bool      HasBrakeColor     { get => _hasBrakeColor;     set => Set(ref _hasBrakeColor,     value); }
    public bool      HasClutchColor    { get => _hasClutchColor;    set => Set(ref _hasClutchColor,    value); }
    public bool      ShowThrottle      { get => _showThrottle;      set => Set(ref _showThrottle,      value); }
    public bool      ShowBrake         { get => _showBrake;         set => Set(ref _showBrake,         value); }
    public bool      ShowClutch        { get => _showClutch;        set => Set(ref _showClutch,        value); }
    public bool      ShowInputTrace    { get => _showInputTrace;    set => Set(ref _showInputTrace,    value); }
    public bool      ShowGearSpeed     { get => _showGearSpeed;     set => Set(ref _showGearSpeed,     value); }
    public SpeedUnit SpeedUnit         { get => _speedUnit;         set => Set(ref _speedUnit,         value); }
    public ColorViewModel ThrottleColor { get; } = new();
    public ColorViewModel BrakeColor    { get; } = new();
    public ColorViewModel ClutchColor   { get; } = new();

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

        LoadNullable(src?.ShowPitServices,     baseConfig.ShowPitServices,     ref _hasShowPitServices, ref _showPitServices);
        LoadNullable(src?.ShowNextPitEstimate, baseConfig.ShowNextPitEstimate, ref _hasShowNextPit,      ref _showNextPit);
        LoadNullable(src?.ShowHumidity,        baseConfig.ShowHumidity,        ref _hasShowHumidity,    ref _showHumidity);
        LoadNullable(src?.ShowWind,            baseConfig.ShowWind,            ref _hasShowWind,        ref _showWind);
        LoadNullable(src?.WindSpeedUnit,       baseConfig.WindSpeedUnit,       ref _hasWindSpeedUnit,   ref _windSpeedUnit);
        LoadNullable(src?.FlatMapLabelMode,    baseConfig.FlatMapLabelMode,    ref _hasFlatMapLabel,    ref _flatMapLabel);
        LoadNullable(src?.PlayerMarkerSize,    baseConfig.PlayerMarkerSize,    ref _hasPlayerMarker,    ref _playerMarker);
        LoadNullable(src?.CarMarkerSize,       baseConfig.CarMarkerSize,       ref _hasCarMarker,       ref _carMarker);
        LoadNullable(src?.ShowPitCars,         baseConfig.ShowPitCars,         ref _hasShowPitCars,     ref _showPitCars);

        LoadNullable(src?.StandingsDisplayMode, baseConfig.StandingsDisplayMode, ref _hasStandingsMode,    ref _standingsMode);
        LoadNullable(src?.ShowClassBadge,       baseConfig.ShowClassBadge,       ref _hasShowClassBadge,   ref _showClassBadge);
        LoadNullable(src?.ShowBestLap,          baseConfig.ShowBestLap,          ref _hasShowBestLap,      ref _showBestLap);
        LoadNullable(src?.MaxStandingsRows,     baseConfig.MaxStandingsRows,     ref _hasMaxStandingsRows, ref _maxStandingsRows);

        LoadNullable(src?.FuelUnit,             baseConfig.FuelUnit,             ref _hasFuelUnit,        ref _fuelUnit);
        LoadNullable(src?.FuelSafetyMarginLaps, baseConfig.FuelSafetyMarginLaps, ref _hasFuelMarginLaps,  ref _fuelMarginLaps);
        LoadNullable(src?.ShowFuelMargin,        baseConfig.ShowFuelMargin,       ref _hasShowFuelMargin,  ref _showFuelMargin);

        LoadNullable(src?.ShowThrottle,   baseConfig.ShowThrottle,   ref _hasShowThrottle,   ref _showThrottle);
        LoadNullable(src?.ShowBrake,      baseConfig.ShowBrake,      ref _hasShowBrake,      ref _showBrake);
        LoadNullable(src?.ShowClutch,     baseConfig.ShowClutch,     ref _hasShowClutch,     ref _showClutch);
        LoadNullable(src?.ShowInputTrace, baseConfig.ShowInputTrace, ref _hasShowInputTrace, ref _showInputTrace);
        LoadNullable(src?.ShowGearSpeed,  baseConfig.ShowGearSpeed,  ref _hasShowGearSpeed,  ref _showGearSpeed);
        LoadNullable(src?.SpeedUnit,      baseConfig.SpeedUnit,      ref _hasSpeedUnit,      ref _speedUnit);
        _hasThrottleColor = src?.ThrottleColor != null;
        ThrottleColor.LoadFrom(src?.ThrottleColor ?? baseConfig.ThrottleColor);
        _hasBrakeColor = src?.BrakeColor != null;
        BrakeColor.LoadFrom(src?.BrakeColor ?? baseConfig.BrakeColor);
        _hasClutchColor = src?.ClutchColor != null;
        ClutchColor.LoadFrom(src?.ClutchColor ?? baseConfig.ClutchColor);

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
            ShowPitServices     = _hasShowPitServices ? _showPitServices : null,
            ShowNextPitEstimate = _hasShowNextPit     ? _showNextPit     : null,
            ShowHumidity        = _hasShowHumidity    ? _showHumidity    : null,
            ShowWind            = _hasShowWind        ? _showWind        : null,
            WindSpeedUnit       = _hasWindSpeedUnit   ? _windSpeedUnit   : null,
            FlatMapLabelMode    = _hasFlatMapLabel    ? _flatMapLabel    : null,
            PlayerMarkerSize    = _hasPlayerMarker    ? _playerMarker    : null,
            CarMarkerSize       = _hasCarMarker       ? _carMarker       : null,
            ShowPitCars         = _hasShowPitCars     ? _showPitCars     : null,
            StandingsDisplayMode = _hasStandingsMode    ? _standingsMode    : null,
            ShowClassBadge       = _hasShowClassBadge   ? _showClassBadge   : null,
            ShowBestLap          = _hasShowBestLap      ? _showBestLap      : null,
            MaxStandingsRows     = _hasMaxStandingsRows ? _maxStandingsRows : null,
            FuelUnit             = _hasFuelUnit        ? _fuelUnit        : null,
            FuelSafetyMarginLaps = _hasFuelMarginLaps  ? _fuelMarginLaps  : null,
            ShowFuelMargin       = _hasShowFuelMargin  ? _showFuelMargin  : null,
            ShowThrottle      = _hasShowThrottle   ? _showThrottle   : null,
            ShowBrake         = _hasShowBrake      ? _showBrake      : null,
            ShowClutch        = _hasShowClutch     ? _showClutch     : null,
            ShowInputTrace    = _hasShowInputTrace ? _showInputTrace : null,
            ShowGearSpeed     = _hasShowGearSpeed  ? _showGearSpeed  : null,
            SpeedUnit         = _hasSpeedUnit      ? _speedUnit      : null,
            ThrottleColor     = _hasThrottleColor  ? ThrottleColor.ToColorConfig()  : null,
            BrakeColor        = _hasBrakeColor     ? BrakeColor.ToColorConfig()     : null,
            ClutchColor       = _hasClutchColor    ? ClutchColor.ToColorConfig()    : null,
        };
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
