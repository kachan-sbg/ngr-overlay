namespace SimOverlay.Sim.iRacing;

/// <summary>
/// Tracks per-lap fuel consumption and maintains a rolling average over the last
/// <see cref="BufferSize"/> green-flag laps.
/// <para>
/// Call <see cref="Update"/> on every telemetry tick. Caution laps are excluded
/// from the average because fuel usage is unrepresentative under yellow.
/// </para>
/// </summary>
internal sealed class FuelConsumptionTracker
{
    private const int BufferSize = 5;

    // iRacing SessionFlags bitmask values for yellow/caution conditions.
    private const int FlagYellow        = 0x0008;
    private const int FlagCaution       = 0x4000;
    private const int FlagCautionWaving = 0x8000;
    private const int CautionMask       = FlagYellow | FlagCaution | FlagCautionWaving;

    private readonly Queue<float> _buffer = new(BufferSize + 1);

    private int   _lastLap          = -1;
    private float _fuelAtLapStart   = float.NaN;
    private bool  _cautionThisLap;

    /// <summary>Rolling average fuel consumption per green-flag lap, in litres. Zero until at least one lap is recorded.</summary>
    public float PerLapAverage { get; private set; }

    /// <summary>Fuel consumed during the most recently completed green-flag lap, in litres. Zero until one lap is recorded.</summary>
    public float LastLapConsumption { get; private set; }

    /// <summary>
    /// Feed the tracker one telemetry tick.
    /// </summary>
    /// <param name="lap">Current lap counter from telemetry (increments at S/F line).</param>
    /// <param name="fuelLevel">Current fuel level in litres.</param>
    /// <param name="sessionFlags">Raw <c>SessionFlags</c> bitmask from iRacing telemetry.</param>
    public void Update(int lap, float fuelLevel, int sessionFlags)
    {
        // Track caution state — any tick under caution taints the whole lap.
        if ((sessionFlags & CautionMask) != 0)
            _cautionThisLap = true;

        if (_lastLap < 0)
        {
            // First tick — initialise without recording consumption.
            _lastLap        = lap;
            _fuelAtLapStart = fuelLevel;
            return;
        }

        if (lap > _lastLap)
        {
            // Lap boundary crossed.
            var consumed = _fuelAtLapStart - fuelLevel;

            // Only record if: green-flag lap, positive consumption (no pitstop refuel distortion).
            if (!_cautionThisLap && consumed > 0f)
            {
                LastLapConsumption = consumed;

                _buffer.Enqueue(consumed);
                if (_buffer.Count > BufferSize)
                    _buffer.Dequeue();

                PerLapAverage = _buffer.Sum() / _buffer.Count;
            }

            _lastLap        = lap;
            _fuelAtLapStart = fuelLevel;
            _cautionThisLap = false;
        }
    }

    /// <summary>Resets all state — call on sim disconnect / session change.</summary>
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
