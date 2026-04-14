namespace SimOverlay.Core;

/// <summary>
/// Smoothing constants for EMA filters applied to continuous display values.
/// Lower alpha = smoother output with more lag; higher = more responsive but jittery.
/// Range will be exposed in user config (suggested: 0.05–0.40).
/// </summary>
public static class EmaConstants
{
    /// <summary>Smoothing factor for gap-to-player and gap-to-leader values (0–1).</summary>
    public const float GapAlpha      = 0.15f;
    /// <summary>Smoothing factor for interval-to-car-ahead values (0–1).</summary>
    public const float IntervalAlpha = 0.15f;
}

/// <summary>
/// Exponential moving average filter: smoothedValue = α·new + (1−α)·prev.
/// Zero-allocation struct — safe to embed in per-car arrays.
/// On the first call the output equals the input exactly (no startup lag).
/// </summary>
public struct EmaFilter
{
    private float _value;
    private bool  _initialized;

    /// <summary>
    /// Feeds a new sample and returns the smoothed value.
    /// </summary>
    public float Update(float newValue, float alpha)
    {
        if (!_initialized) { _value = newValue; _initialized = true; return _value; }
        _value = alpha * newValue + (1f - alpha) * _value;
        return _value;
    }

    /// <summary>Resets the filter so the next sample is accepted as-is (no stale state).</summary>
    public void Reset() => _initialized = false;
}
