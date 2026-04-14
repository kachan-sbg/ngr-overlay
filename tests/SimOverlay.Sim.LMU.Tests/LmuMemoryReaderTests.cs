using System.IO.MemoryMappedFiles;
using SimOverlay.Sim.LMU.SharedMemory;

namespace SimOverlay.Sim.LMU.Tests;

[Collection("LMU MMF")]
public class LmuMemoryReaderTests
{
    [Fact]
    public void TryOpen_MissingMapping_ReturnsFalse()
    {
        using var reader = new LmuMemoryReader($"LMU_Data_Test_{Guid.NewGuid():N}");

        Assert.False(reader.TryOpen());
        Assert.False(reader.IsOpen);
    }

    [Fact]
    public void Reopen_WhenProducerClosed_DropsStaleHandleAndReturnsFalse()
    {
        var mapName = $"LMU_Data_Test_{Guid.NewGuid():N}";
        using var producer = MemoryMappedFile.CreateOrOpen(mapName, 4096, MemoryMappedFileAccess.ReadWrite);
        using var reader = new LmuMemoryReader(mapName);

        Assert.True(reader.TryOpen());
        Assert.True(reader.IsOpen);

        producer.Dispose(); // simulate producer shutdown; only reader handle remains

        Assert.False(reader.Reopen());
        Assert.False(reader.IsOpen);
    }
}

[CollectionDefinition("LMU MMF", DisableParallelization = true)]
public sealed class LmuMmfCollection { }
