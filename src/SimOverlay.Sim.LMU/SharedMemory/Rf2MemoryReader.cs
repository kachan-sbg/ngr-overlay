using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using SimOverlay.Core;

namespace SimOverlay.Sim.LMU.SharedMemory;

/// <summary>
/// Opens and reads the <c>LMU_Data</c> shared memory mapped file created by Le Mans Ultimate.
/// <para>
/// Scoring data (<see cref="LmuScoringInfo"/> + <see cref="LmuVehicleScoring"/> array) is
/// read from fixed byte offsets within the buffer.  Player telemetry fields are read via
/// unsafe pointer arithmetic using the per-field offsets defined in
/// <see cref="LmuSharedMemoryLayout"/>.
/// </para>
/// <para>
/// LMU does not use a version-bump consistency block, so torn-read protection is omitted.
/// <see cref="ReadScoring"/> returns null only on exception.
/// </para>
/// </summary>
internal sealed class LmuMemoryReader : IDisposable
{
    private static readonly int ScoringInfoSize    = Marshal.SizeOf<LmuScoringInfo>();
    private static readonly int VehicleScoringSize = Marshal.SizeOf<LmuVehicleScoring>();

    private MemoryMappedFile?         _file;
    private MemoryMappedViewAccessor? _view;
    private readonly string           _fileName;
    private bool                      _disposed;

    public bool IsOpen => _view != null;

    internal LmuMemoryReader(string? fileName = null)
    {
        _fileName = string.IsNullOrWhiteSpace(fileName)
            ? LmuSharedMemoryLayout.DataFile
            : fileName;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to open the <c>LMU_Data</c> mapped file.
    /// Returns <c>true</c> on success; leaves the reader closed on failure.
    /// </summary>
    public bool TryOpen()
        => TryOpen(logSuccess: true);

    /// <summary>
    /// Closes and re-opens the LMU mapping.
    /// Useful for probing producer liveness: if LMU has exited, reopen fails once our
    /// previous handle is released.
    /// </summary>
    public bool Reopen()
    {
        Close();
        return TryOpen(logSuccess: false);
    }

    private bool TryOpen(bool logSuccess)
    {
        try
        {
            _file = MemoryMappedFile.OpenExisting(_fileName);
            _view = _file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            if (logSuccess)
            {
                AppLog.Info(
                    $"LmuMemoryReader: opened {_fileName} " +
                    $"(ScoringInfoSize={ScoringInfoSize}B, VehicleScoringSize={VehicleScoringSize}B, " +
                    $"ScoringInfoAt={LmuSharedMemoryLayout.ScoringInfoOffset}, " +
                    $"VehiclesAt={LmuSharedMemoryLayout.ScoringVehiclesOffset})");
                ValidateLayout();
            }
            return true;
        }
        catch
        {
            Close();
            return false;
        }
    }

    /// <summary>
    /// Reads a few anchor fields at their expected offsets and logs a warning if the values
    /// look implausible — a cheap guard against LMU updates shifting the struct layout.
    /// Does NOT abort the open; data may still be usable even if one anchor looks odd.
    /// </summary>
    private unsafe void ValidateLayout()
    {
        if (_view == null) return;
        byte* ptr = null;
        try
        {
            _view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

            // Anchor 1: NumVehicles at ScoringInfoOffset + 104 must be in [0, 104].
            int numVeh = *(int*)(ptr + LmuSharedMemoryLayout.ScoringInfoOffset + 104);
            bool numVehOk = numVeh >= 0 && numVeh <= LmuSharedMemoryLayout.MaxVehicles;

            // Anchor 2: CurrentET at ScoringInfoOffset + 68 must be >= 0.
            double currentEt = *(double*)(ptr + LmuSharedMemoryLayout.ScoringInfoOffset + 68);
            bool etOk = currentEt >= 0.0 && currentEt < 86400.0 * 7; // < 1 week in seconds

            if (!numVehOk || !etOk)
            {
                AppLog.Info(
                    $"LmuMemoryReader: layout anchor check — NumVehicles={numVeh} (ok={numVehOk}), " +
                    $"CurrentET={currentEt:F1} (ok={etOk}). " +
                    "If data looks wrong, LMU may have updated its struct layout.");
            }
            else
            {
                AppLog.Info(
                    $"LmuMemoryReader: layout anchors OK — NumVehicles={numVeh}, CurrentET={currentEt:F1}s.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Exception("LmuMemoryReader.ValidateLayout", ex);
        }
        finally
        {
            if (ptr != null)
                _view.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    private void Close()
    {
        _view?.Dispose();
        _view = null;
        _file?.Dispose();
        _file = null;
    }

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the current scoring snapshot (session info + all active vehicles).
    /// Returns <c>null</c> only on exception.
    /// </summary>
    public unsafe LmuScoringSnapshot? ReadScoring()
    {
        if (_view == null) return null;

        byte* ptr = null;
        bool shouldClose = false;
        try
        {
            _view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

            var info = Marshal.PtrToStructure<LmuScoringInfo>(
                (IntPtr)(ptr + LmuSharedMemoryLayout.ScoringInfoOffset));

            int count = Math.Clamp(info.NumVehicles, 0, LmuSharedMemoryLayout.MaxVehicles);
            var vehicles = new LmuVehicleScoring[count];
            for (int i = 0; i < count; i++)
            {
                long off = LmuSharedMemoryLayout.ScoringVehiclesOffset
                           + (long)i * LmuSharedMemoryLayout.VehicleScoringSize;
                vehicles[i] = Marshal.PtrToStructure<LmuVehicleScoring>((IntPtr)(ptr + off));
            }

            return new LmuScoringSnapshot(info, vehicles);
        }
        catch (Exception ex)
        {
            AppLog.Exception("LmuMemoryReader.ReadScoring", ex);
            shouldClose = true;
            return null;
        }
        finally
        {
            if (ptr != null)
                _view.SafeMemoryMappedViewHandle.ReleasePointer();
            if (shouldClose)
                Close();
        }
    }

    /// <summary>
    /// Reads player telemetry fields using the <c>playerVehicleIdx</c> byte that LMU writes into
    /// the telemetry header (<c>TelemetryHeaderOffset + 1</c>).  This is the authoritative index
    /// into the <c>telemInfo</c> array and is not necessarily equal to the scoring array index.
    /// Returns <c>null</c> if the file is not open or <c>playerHasVehicle</c> is zero.
    /// </summary>
    public unsafe LmuPlayerInputs? ReadPlayerTelemetry()
    {
        if (_view == null) return null;

        byte* ptr = null;
        bool shouldClose = false;
        try
        {
            _view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

            // SharedMemoryTelemetryData header layout:
            //   [+0] uint8_t activeVehicles
            //   [+1] uint8_t playerVehicleIdx   ← authoritative telemetry array index
            //   [+2] bool    playerHasVehicle
            //   [+3] 1-byte padding (pack4 aligns TelemInfoV01 array to 4B)
            byte playerVehicleIdx = *(ptr + LmuSharedMemoryLayout.TelemetryHeaderOffset + 1);
            byte playerHasVehicle = *(ptr + LmuSharedMemoryLayout.TelemetryHeaderOffset + 2);

            if (playerHasVehicle == 0) return null;
            if (playerVehicleIdx >= LmuSharedMemoryLayout.MaxVehicles) return null;

            byte* veh = ptr
                        + LmuSharedMemoryLayout.TelemetryVehOffset
                        + (long)playerVehicleIdx * LmuSharedMemoryLayout.VehicleTelemSize;

            int   gear     = *(int*)(veh + LmuSharedMemoryLayout.Telem_Gear);
            float rpm      = (float)*(double*)(veh + LmuSharedMemoryLayout.Telem_EngineRPM);
            float throttle = (float)*(double*)(veh + LmuSharedMemoryLayout.Telem_Throttle);
            float brake    = (float)*(double*)(veh + LmuSharedMemoryLayout.Telem_Brake);
            float steering = (float)*(double*)(veh + LmuSharedMemoryLayout.Telem_Steering);
            float clutch   = (float)*(double*)(veh + LmuSharedMemoryLayout.Telem_Clutch);
            float fuel     = (float)*(double*)(veh + LmuSharedMemoryLayout.Telem_Fuel);
            float fuelCap  = (float)*(double*)(veh + LmuSharedMemoryLayout.Telem_FuelCapacity);
            bool  limiter  = *(veh + LmuSharedMemoryLayout.Telem_SpeedLimiter) != 0;

            return new LmuPlayerInputs(
                Throttle:             throttle,
                Brake:                brake,
                Clutch:               clutch,
                Steering:             steering,
                Gear:                 gear,
                EngineRpm:            rpm,
                FuelLiters:           fuel,
                FuelCapacityLiters:   fuelCap,
                SpeedLimiterActive:   limiter);
        }
        catch (Exception ex)
        {
            AppLog.Exception("LmuMemoryReader.ReadPlayerTelemetry", ex);
            shouldClose = true;
            return null;
        }
        finally
        {
            if (ptr != null)
                _view.SafeMemoryMappedViewHandle.ReleasePointer();
            if (shouldClose)
                Close();
        }
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }
}

/// <summary>Player telemetry inputs read from the LMU_Data telemetry section.</summary>
internal sealed record LmuPlayerInputs(
    float Throttle,
    float Brake,
    float Clutch,
    float Steering,
    int   Gear,
    float EngineRpm,
    float FuelLiters,
    float FuelCapacityLiters,
    bool  SpeedLimiterActive);
