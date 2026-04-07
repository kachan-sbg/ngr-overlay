using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using SimOverlay.Core;

namespace SimOverlay.Sim.LMU.SharedMemory;

/// <summary>
/// Opens and reads the rF2/LMU shared memory mapped files:
/// <list type="bullet">
///   <item><c>$rFactor2SMMP_Scoring$</c> — session + per-vehicle scoring data</item>
///   <item><c>$rFactor2SMMP_Telemetry$</c> — per-vehicle physics + driver inputs</item>
/// </list>
///
/// <para>
/// Scoring data is read using version-bump consistency: if a write was in progress
/// (<c>VersionUpdateBegin != VersionUpdateEnd</c>), the tick is skipped.
/// </para>
/// <para>
/// The telemetry vehicle stride is computed from the mapped file size at runtime
/// (fileSize − versionBlock / MaxVehicles) to be robust against plugin version
/// differences.  If the file does not open (plugin not installed), telemetry
/// falls back to zero/default values.
/// </para>
/// </summary>
internal sealed class Rf2MemoryReader : IDisposable
{
    private const string ScoringMapName   = "$rFactor2SMMP_Scoring$";
    private const string TelemetryMapName = "$rFactor2SMMP_Telemetry$";

    // Cached marshal sizes (computed once — Marshal.SizeOf calls are not free).
    private static readonly int ScoringInfoSize    = Marshal.SizeOf<Rf2ScoringInfo>();
    private static readonly int VehicleScoringSize = Marshal.SizeOf<Rf2VehicleScoring>();
    private static readonly int TelemetryInputSize = Marshal.SizeOf<Rf2VehicleTelemetryInputs>();

    // Scoring: ScoringInfo immediately follows the 8-byte version block.
    private static readonly int ScoringInfoOffset = Rf2Structs.VersionBlockSize;
    // Vehicles immediately follow ScoringInfo.
    private static readonly int VehiclesOffset    = Rf2Structs.VersionBlockSize + ScoringInfoSize;

    private MemoryMappedFile?         _scoringFile;
    private MemoryMappedViewAccessor? _scoringView;
    private MemoryMappedFile?         _telemetryFile;
    private MemoryMappedViewAccessor? _telemetryView;
    private int                       _telemetryStride; // bytes per vehicle in telemetry file

    private bool _disposed;

    public bool IsScoringOpen    => _scoringView   != null;
    public bool IsTelemetryOpen  => _telemetryView != null;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to open the scoring mapped file.  Returns true on success.
    /// The telemetry file is opened separately via <see cref="TryOpenTelemetry"/>.
    /// </summary>
    public bool TryOpenScoring()
    {
        try
        {
            _scoringFile = MemoryMappedFile.OpenExisting(ScoringMapName);
            _scoringView = _scoringFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            AppLog.Info(
                $"Rf2MemoryReader: opened {ScoringMapName} " +
                $"(ScoringInfo={ScoringInfoSize}B, VehicleScoring={VehicleScoringSize}B, " +
                $"VehiclesAt={VehiclesOffset})");
            return true;
        }
        catch
        {
            CloseScoring();
            return false;
        }
    }

    /// <summary>
    /// Attempts to open the telemetry mapped file and compute the per-vehicle stride.
    /// Returns true on success.  Safe to call even if LMU is not running.
    /// </summary>
    public bool TryOpenTelemetry()
    {
        try
        {
            _telemetryFile = MemoryMappedFile.OpenExisting(TelemetryMapName);
            _telemetryView = _telemetryFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            // Derive stride from file size so we're robust against plugin version differences.
            long usable = _telemetryView.Capacity - Rf2Structs.VersionBlockSize;
            _telemetryStride = (int)(usable / Rf2Structs.MaxVehicles);

            AppLog.Info(
                $"Rf2MemoryReader: opened {TelemetryMapName} " +
                $"(stride={_telemetryStride}B, inputsSize={TelemetryInputSize}B)");

            if (_telemetryStride < TelemetryInputSize)
            {
                AppLog.Error(
                    $"Rf2MemoryReader: computed telemetry stride {_telemetryStride} < " +
                    $"inputs struct {TelemetryInputSize} — telemetry disabled.");
                CloseTelemetry();
                return false;
            }

            return true;
        }
        catch
        {
            CloseTelemetry();
            return false;
        }
    }

    public void CloseScoring()
    {
        _scoringView?.Dispose();
        _scoringView = null;
        _scoringFile?.Dispose();
        _scoringFile = null;
    }

    public void CloseTelemetry()
    {
        _telemetryView?.Dispose();
        _telemetryView = null;
        _telemetryFile?.Dispose();
        _telemetryFile = null;
    }

    // ── Reads ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the current scoring snapshot.
    /// Returns null if LMU is mid-write (version mismatch) or on any read error.
    /// </summary>
    public unsafe Rf2ScoringSnapshot? ReadScoring()
    {
        if (_scoringView == null) return null;

        byte* ptr = null;
        try
        {
            _scoringView.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

            uint vBegin = *(uint*)ptr;

            var info = Marshal.PtrToStructure<Rf2ScoringInfo>((IntPtr)(ptr + ScoringInfoOffset));

            int count = Math.Clamp(info.NumVehicles, 0, Rf2Structs.MaxVehicles);
            var vehicles = new Rf2VehicleScoring[count];
            for (int i = 0; i < count; i++)
            {
                long off = VehiclesOffset + (long)i * VehicleScoringSize;
                vehicles[i] = Marshal.PtrToStructure<Rf2VehicleScoring>((IntPtr)(ptr + off));
            }

            uint vEnd = *(uint*)(ptr + 4);

            // Torn read — writer was active; skip this tick.
            if (vBegin != vEnd) return null;

            return new Rf2ScoringSnapshot(info, vehicles);
        }
        catch (Exception ex)
        {
            AppLog.Exception("Rf2MemoryReader.ReadScoring", ex);
            return null;
        }
        finally
        {
            if (ptr != null)
                _scoringView.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    /// <summary>
    /// Reads the telemetry inputs for the vehicle whose slot ID matches <paramref name="playerId"/>.
    /// Returns null if the telemetry file is not open, the player entry is not found,
    /// or the stride is smaller than the inputs struct.
    /// </summary>
    public unsafe Rf2VehicleTelemetryInputs? ReadPlayerTelemetry(int playerId)
    {
        if (_telemetryView == null || _telemetryStride < TelemetryInputSize) return null;

        byte* ptr = null;
        try
        {
            _telemetryView.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

            byte* dataStart = ptr + Rf2Structs.VersionBlockSize;

            for (int i = 0; i < Rf2Structs.MaxVehicles; i++)
            {
                byte* veh = dataStart + (long)i * _telemetryStride;
                int slotId = *(int*)veh; // first field is int Id

                if (slotId == playerId)
                {
                    return Marshal.PtrToStructure<Rf2VehicleTelemetryInputs>((IntPtr)veh);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            AppLog.Exception("Rf2MemoryReader.ReadPlayerTelemetry", ex);
            return null;
        }
        finally
        {
            if (ptr != null)
                _telemetryView.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseScoring();
        CloseTelemetry();
    }
}

/// <summary>Immutable snapshot of rF2 scoring data for one polling tick.</summary>
internal sealed record Rf2ScoringSnapshot(
    Rf2ScoringInfo      Info,
    Rf2VehicleScoring[] Vehicles);
