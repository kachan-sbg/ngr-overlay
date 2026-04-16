using System.IO.MemoryMappedFiles;
using NrgOverlay.Core;
using NrgOverlay.Core.Config;
using NrgOverlay.Sim.iRacing;

namespace NrgOverlay.Sim.iRacing.Tests;

public class IRacingSharedMemoryStabilityTests
{
    private const int StatusOffset = 4;
    private const int ConnectedBit = 0x01;

    [Fact]
    public void ConnectionProbe_MissingMap_ReturnsFalse()
    {
        var probe = new IRacingConnectionProbe(
            mmfName: $"Local\\IRSDK_Test_{Guid.NewGuid():N}",
            statusOffset: StatusOffset,
            statusConnectedBit: ConnectedBit);

        Assert.False(probe.IsConnected());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void ConnectionProbe_ReadsConnectedBit(int rawStatus, bool expected)
    {
        var map = $"Local\\IRSDK_Test_{Guid.NewGuid():N}";
        using var mmf = MemoryMappedFile.CreateOrOpen(map, 64, MemoryMappedFileAccess.ReadWrite);
        using (var view = mmf.CreateViewAccessor(0, 64, MemoryMappedFileAccess.ReadWrite))
        {
            view.Write(StatusOffset, rawStatus);
        }

        var probe = new IRacingConnectionProbe(
            mmfName: map,
            statusOffset: StatusOffset,
            statusConnectedBit: ConnectedBit);

        Assert.Equal(expected, probe.IsConnected());
    }

    [Fact]
    public async Task ConnectionProbe_RandomCreateUpdateCloseConcurrency_DoesNotThrow()
    {
        var map = $"Local\\IRSDK_Test_{Guid.NewGuid():N}";
        var probe = new IRacingConnectionProbe(
            mmfName: map,
            statusOffset: StatusOffset,
            statusConnectedBit: ConnectedBit);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var token = cts.Token;
        Exception? producerFailure = null;
        Exception? consumerFailure = null;
        var sawTrue = false;
        var sawFalse = false;

        var producer = Task.Run(async () =>
        {
            MemoryMappedFile? mmf = null;
            MemoryMappedViewAccessor? view = null;
            var rng = new Random(87123);
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (mmf is null || rng.NextDouble() < 0.25)
                    {
                        view?.Dispose();
                        mmf?.Dispose();
                        view = null;
                        mmf = null;

                        if (rng.NextDouble() < 0.30)
                        {
                            await Task.Delay(rng.Next(1, 8), token);
                            continue;
                        }

                        mmf = MemoryMappedFile.CreateOrOpen(map, 64, MemoryMappedFileAccess.ReadWrite);
                        view = mmf.CreateViewAccessor(0, 64, MemoryMappedFileAccess.ReadWrite);
                    }

                    if (view is not null)
                    {
                        var status = rng.NextDouble() < 0.5 ? 0 : ConnectedBit;
                        view.Write(StatusOffset, status);
                    }

                    await Task.Delay(rng.Next(1, 5), token);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on cancellation
            }
            catch (Exception ex)
            {
                producerFailure = ex;
            }
            finally
            {
                view?.Dispose();
                mmf?.Dispose();
            }
        }, token);

        var consumer = Task.Run(async () =>
        {
            var rng = new Random(98123);
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var connected = probe.IsConnected();
                    sawTrue |= connected;
                    sawFalse |= !connected;
                    await Task.Delay(rng.Next(1, 4), token);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on cancellation
            }
            catch (Exception ex)
            {
                consumerFailure = ex;
            }
        }, token);

        await Task.WhenAll(producer, consumer);

        Assert.Null(producerFailure);
        Assert.Null(consumerFailure);
        Assert.True(sawFalse);
    }

    [Fact]
    public void ConnectionProbe_TwoNamedMaps_AreIsolated()
    {
        var mapA = $"Local\\IRSDK_Test_A_{Guid.NewGuid():N}";
        var mapB = $"Local\\IRSDK_Test_B_{Guid.NewGuid():N}";

        using var mmfA = MemoryMappedFile.CreateOrOpen(mapA, 64, MemoryMappedFileAccess.ReadWrite);
        using var mmfB = MemoryMappedFile.CreateOrOpen(mapB, 64, MemoryMappedFileAccess.ReadWrite);
        using (var viewA = mmfA.CreateViewAccessor(0, 64, MemoryMappedFileAccess.ReadWrite))
        using (var viewB = mmfB.CreateViewAccessor(0, 64, MemoryMappedFileAccess.ReadWrite))
        {
            viewA.Write(StatusOffset, ConnectedBit);
            viewB.Write(StatusOffset, 0);
        }

        var probeA = new IRacingConnectionProbe(mapA, StatusOffset, ConnectedBit);
        var probeB = new IRacingConnectionProbe(mapB, StatusOffset, ConnectedBit);

        Assert.True(probeA.IsConnected());
        Assert.False(probeB.IsConnected());
    }

    [Fact]
    public void Provider_IsRunning_WhenProbeThrows_ReturnsFalse()
    {
        var provider = new IRacingProvider(
            new SimDataBus(),
            new AppConfig(),
            new ConfigStore(Path.Combine(Path.GetTempPath(), $"cfg-{Guid.NewGuid():N}.json")),
            new ThrowingProbe());

        Assert.False(provider.IsRunning());
    }

    private sealed class ThrowingProbe : IIRacingConnectionProbe
    {
        public bool IsConnected() => throw new UnauthorizedAccessException("denied");
    }
}

