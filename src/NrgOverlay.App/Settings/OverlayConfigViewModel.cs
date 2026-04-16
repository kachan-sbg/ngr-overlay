using System.ComponentModel;
using System.Runtime.CompilerServices;
using NrgOverlay.Core.Config;

namespace NrgOverlay.App.Settings;

/// <summary>
/// Editable ViewModel copy of <see cref="OverlayConfig"/>.
/// Always a detached clone вЂ” never mutates the live config directly.
/// Call <see cref="LoadFrom"/> to populate, <see cref="ToConfig"/> to produce a
/// new config snapshot for <c>PreviewConfig</c> / <c>ApplyConfig</c>.
/// </summary>
public sealed class OverlayConfigViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // в”Ђв”Ђ Identity в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private string _id = "";
    private bool   _enabled;

    public string Id      { get => _id;      set => Set(ref _id,      value); }
    public bool   Enabled { get => _enabled; set => Set(ref _enabled, value); }

    // в”Ђв”Ђ Position & size в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private int _x, _y, _width = 300, _height = 200;

    public int X      { get => _x;       set => Set(ref _x,       value); }
    public int Y      { get => _y;       set => Set(ref _y,       value); }
    public int Width  { get => _width;   set => Set(ref _width,   value); }
    public int Height { get => _height;  set => Set(ref _height,  value); }

    // в”Ђв”Ђ Appearance в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private float _opacity   = 1f;
    private float _fontSize  = 13f;

    public float Opacity  { get => _opacity;  set => Set(ref _opacity,  value); }
    public float FontSize { get => _fontSize; set => Set(ref _fontSize, value); }

    public ColorViewModel BackgroundColor    { get; } = new();
    public ColorViewModel TextColor         { get; } = new();

    // в”Ђв”Ђ Relative-specific в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private bool _showIRating  = true;
    private bool _showLicense  = true;
    private int  _maxDrivers   = 15;

    public bool ShowIRating    { get => _showIRating;  set => Set(ref _showIRating,  value); }
    public bool ShowLicense    { get => _showLicense;  set => Set(ref _showLicense,  value); }
    public int  MaxDriversShown{ get => _maxDrivers;   set => Set(ref _maxDrivers,   value); }

    public ColorViewModel PlayerHighlightColor { get; } = new();

    // в”Ђв”Ђ Session Info-specific в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
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

    // в”Ђв”Ђ Delta Bar-specific в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private float _deltaBarMax   = 3f;
    private bool  _showTrend     = true;
    private bool  _showDeltaTxt  = true;
    private bool  _showReferenceLapTime = true;

    public float DeltaBarMaxSeconds { get => _deltaBarMax;  set => Set(ref _deltaBarMax,  value); }
    public bool  ShowTrendArrow     { get => _showTrend;    set => Set(ref _showTrend,    value); }
    public bool  ShowDeltaText      { get => _showDeltaTxt; set => Set(ref _showDeltaTxt, value); }
    public bool  ShowReferenceLapTime { get => _showReferenceLapTime; set => Set(ref _showReferenceLapTime, value); }

    public ColorViewModel FasterColor { get; } = new();
    public ColorViewModel SlowerColor { get; } = new();

    // в”Ђв”Ђ Pit Helper-specific в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private bool _showPitServices = true, _showNextPitEstimate = true;

    public bool ShowPitServices     { get => _showPitServices;     set => Set(ref _showPitServices,     value); }
    public bool ShowNextPitEstimate { get => _showNextPitEstimate; set => Set(ref _showNextPitEstimate, value); }

    // в”Ђв”Ђ Weather-specific в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private bool          _showHumidity = true, _showWind = true;
    private WindSpeedUnit _windSpeedUnit = WindSpeedUnit.Kph;

    public bool          ShowHumidity  { get => _showHumidity;  set => Set(ref _showHumidity,  value); }
    public bool          ShowWind      { get => _showWind;      set => Set(ref _showWind,      value); }
    public WindSpeedUnit WindSpeedUnit { get => _windSpeedUnit; set => Set(ref _windSpeedUnit, value); }

    // в”Ђв”Ђ Flat Track Map-specific в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private FlatMapLabelMode _flatMapLabelMode = FlatMapLabelMode.CarNumber;
    private float            _playerMarkerSize = 8f, _carMarkerSize = 4f;
    private bool             _showPitCars = true;

    public FlatMapLabelMode FlatMapLabelMode { get => _flatMapLabelMode;  set => Set(ref _flatMapLabelMode,  value); }
    public float            PlayerMarkerSize { get => _playerMarkerSize;  set => Set(ref _playerMarkerSize,  value); }
    public float            CarMarkerSize    { get => _carMarkerSize;     set => Set(ref _carMarkerSize,     value); }
    public bool             ShowPitCars      { get => _showPitCars;       set => Set(ref _showPitCars,       value); }

    // в”Ђв”Ђ Standings-specific в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private StandingsDisplayMode _standingsMode = StandingsDisplayMode.Combined;
    private bool _showClassBadge = true, _showBestLap = true;
    private int  _maxStandingsRows = 30;

    public StandingsDisplayMode StandingsDisplayMode { get => _standingsMode;    set => Set(ref _standingsMode,    value); }
    public bool ShowClassBadge                       { get => _showClassBadge;   set => Set(ref _showClassBadge,   value); }
    public bool ShowBestLap                          { get => _showBestLap;      set => Set(ref _showBestLap,      value); }
    public int  MaxStandingsRows                     { get => _maxStandingsRows; set => Set(ref _maxStandingsRows, value); }

    // в”Ђв”Ђ Fuel Calculator-specific в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private FuelUnit _fuelUnit = FuelUnit.Liters;
    private float    _fuelSafetyMarginLaps = 1.0f;
    private bool     _showFuelMargin = true;

    public FuelUnit FuelUnit             { get => _fuelUnit;             set => Set(ref _fuelUnit,             value); }
    public float    FuelSafetyMarginLaps { get => _fuelSafetyMarginLaps; set => Set(ref _fuelSafetyMarginLaps, value); }
    public bool     ShowFuelMargin       { get => _showFuelMargin;       set => Set(ref _showFuelMargin,       value); }

    // в”Ђв”Ђ Input Telemetry-specific в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ
    private bool      _showThrottle   = true;
    private bool      _showBrake      = true;
    private bool      _showClutch     = true;
    private bool      _showInputTrace = true;
    private bool      _showGearSpeed  = true;
    private SpeedUnit _speedUnit      = SpeedUnit.Kph;

    public bool      ShowThrottle   { get => _showThrottle;   set => Set(ref _showThrottle,   value); }
    public bool      ShowBrake      { get => _showBrake;      set => Set(ref _showBrake,      value); }
    public bool      ShowClutch     { get => _showClutch;     set => Set(ref _showClutch,     value); }
    public bool      ShowInputTrace { get => _showInputTrace; set => Set(ref _showInputTrace, value); }
    public bool      ShowGearSpeed  { get => _showGearSpeed;  set => Set(ref _showGearSpeed,  value); }
    public SpeedUnit SpeedUnit      { get => _speedUnit;      set => Set(ref _speedUnit,      value); }

    public ColorViewModel ThrottleColor { get; } = new();
    public ColorViewModel BrakeColor    { get; } = new();
    public ColorViewModel ClutchColor   { get; } = new();

    // в”Ђв”Ђ Load / save в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

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
        _showReferenceLapTime = c.ShowReferenceLapTime;
        FasterColor.LoadFrom(c.FasterColor);
        SlowerColor.LoadFrom(c.SlowerColor);

        _showPitServices     = c.ShowPitServices;
        _showNextPitEstimate = c.ShowNextPitEstimate;
        _showHumidity        = c.ShowHumidity;
        _showWind            = c.ShowWind;
        _windSpeedUnit       = c.WindSpeedUnit;
        _flatMapLabelMode    = c.FlatMapLabelMode;
        _playerMarkerSize    = c.PlayerMarkerSize;
        _carMarkerSize       = c.CarMarkerSize;
        _showPitCars         = c.ShowPitCars;

        _standingsMode    = c.StandingsDisplayMode;
        _showClassBadge   = c.ShowClassBadge;
        _showBestLap      = c.ShowBestLap;
        _maxStandingsRows = c.MaxStandingsRows;

        _fuelUnit             = c.FuelUnit;
        _fuelSafetyMarginLaps = c.FuelSafetyMarginLaps;
        _showFuelMargin       = c.ShowFuelMargin;

        _showThrottle   = c.ShowThrottle;
        _showBrake      = c.ShowBrake;
        _showClutch     = c.ShowClutch;
        _showInputTrace = c.ShowInputTrace;
        _showGearSpeed  = c.ShowGearSpeed;
        _speedUnit      = c.SpeedUnit;
        ThrottleColor.LoadFrom(c.ThrottleColor);
        BrakeColor.LoadFrom(c.BrakeColor);
        ClutchColor.LoadFrom(c.ClutchColor);

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
        ShowReferenceLapTime = _showReferenceLapTime,
        FasterColor        = FasterColor.ToColorConfig(),
        SlowerColor        = SlowerColor.ToColorConfig(),
        ShowPitServices     = _showPitServices,
        ShowNextPitEstimate = _showNextPitEstimate,
        ShowHumidity        = _showHumidity,
        ShowWind            = _showWind,
        WindSpeedUnit       = _windSpeedUnit,
        FlatMapLabelMode    = _flatMapLabelMode,
        PlayerMarkerSize    = _playerMarkerSize,
        CarMarkerSize       = _carMarkerSize,
        ShowPitCars         = _showPitCars,
        StandingsDisplayMode = _standingsMode,
        ShowClassBadge       = _showClassBadge,
        ShowBestLap          = _showBestLap,
        MaxStandingsRows     = _maxStandingsRows,
        FuelUnit             = _fuelUnit,
        FuelSafetyMarginLaps = _fuelSafetyMarginLaps,
        ShowFuelMargin       = _showFuelMargin,
        ShowThrottle       = _showThrottle,
        ShowBrake          = _showBrake,
        ShowClutch         = _showClutch,
        ShowInputTrace     = _showInputTrace,
        ShowGearSpeed      = _showGearSpeed,
        SpeedUnit          = _speedUnit,
        ThrottleColor      = ThrottleColor.ToColorConfig(),
        BrakeColor         = BrakeColor.ToColorConfig(),
        ClutchColor        = ClutchColor.ToColorConfig(),
    };

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

