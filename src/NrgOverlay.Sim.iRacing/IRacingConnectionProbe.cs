using System.IO.MemoryMappedFiles;

namespace NrgOverlay.Sim.iRacing;

internal interface IIRacingConnectionProbe
{
    bool IsConnected();
}

/// <summary>
/// Reads iRacing's shared-memory header status bit to determine live simulator connectivity.
/// </summary>
internal sealed class IRacingConnectionProbe : IIRacingConnectionProbe
{
    private readonly string _mmfName;
    private readonly int _statusOffset;
    private readonly int _statusConnectedBit;

    public IRacingConnectionProbe(
        string mmfName = "Local\\IRSDKMemMapFileName",
        int statusOffset = 4,
        int statusConnectedBit = 0x01)
    {
        _mmfName = mmfName;
        _statusOffset = statusOffset;
        _statusConnectedBit = statusConnectedBit;
    }

    public bool IsConnected()
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(_mmfName, MemoryMappedFileRights.Read);
            using var view = mmf.CreateViewAccessor(0, 8, MemoryMappedFileAccess.Read);
            return (view.ReadInt32(_statusOffset) & _statusConnectedBit) != 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }
}

