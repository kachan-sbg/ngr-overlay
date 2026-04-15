namespace NrgOverlay.Sim.LMU;

/// <summary>
/// Tracks per-lap fuel consumption for LMU and maintains a rolling average over the last
/// <see cref="BufferSize"/> green-flag laps.
/// <para>
/// Call <see cref="Update"/> on every scoring tick.  Yellow-flag laps are excluded.
/// LMU does not have an iRacing-style SessionFlags bitmask; caution is detected via
/// the per-vehicle <c>mUnderYellow</c> byte from the rF2 scoring struct.
/// </para>
/// </summary>
internal sealed class LmuFuelTracker
{
    private const int BufferSize = 5;

    private readonly Queue<float> _buffer = new(BufferSize + 1);

    private short _lastLap        = -1;
    private float _fuelAtLapStart = float.NaN;
    private bool  _cautionThisLap;

    /// <summary>Rolling average fuel consumption per green-flag lap, in litres.  Zero until at least one lap is recorded.</summary>
    public float PerLapAverage { get; private set; }

    /// <summary>Fuel consumed during the most recently completed green-flag lap, in litres.  Zero until one lap is recorded.</summary>
    public float LastLapConsumption { get; private set; }

    /// <summary>
    /// Feed the tracker one scoring tick.
    /// </summary>
    /// <param name="totalLaps">Completed lap count from <c>mTotalLaps</c> (increments at S/F crossing).</param>
    /// <param name="fuelLiters">Absolute fuel level in litres.</param>
    /// <param name="underYellow">Non-zero if the vehicle is under a yellow flag this tick.</param>
    public void Update(short totalLaps, float fuelLiters, byte underYellow)
    {
        if (underYellow != 0)
            _cautionThisLap = true;

        if (_lastLap < 0)
        {
            _lastLap        = totalLaps;
            _fuelAtLapStart = fuelLiters;
            return;
        }

        if (totalLaps > _lastLap)
        {
            var consumed = _fuelAtLapStart - fuelLiters;

            if (!_cautionThisLap && consumed > 0f)
            {
                LastLapConsumption = consumed;

                _buffer.Enqueue(consumed);
                if (_buffer.Count > BufferSize)
                    _buffer.Dequeue();

                PerLapAverage = _buffer.Sum() / _buffer.Count;
            }

            _lastLap        = totalLaps;
            _fuelAtLapStart = fuelLiters;
            _cautionThisLap = false;
        }
    }

    /// <summary>Resets all state вЂ” call on disconnect or session change.</summary>
    public void Reset()
    {
        _buffer.Clear();
        _lastLap           = -1;
        _fuelAtLapStart    = float.NaN;
        _cautionThisLap    = false;
        PerLapAverage      = 0f;
        LastLapConsumption = 0f;
    }
}

