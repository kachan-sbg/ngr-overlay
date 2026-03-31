using SimOverlay.Core;

namespace SimOverlay.Core.Tests;

public class SimDataBusTests
{
    [Fact]
    public void Subscribe_ReceivesPublishedMessage()
    {
        var bus = new SimDataBus();
        int received = 0;

        bus.Subscribe<int>(x => received = x);
        bus.Publish(42);

        Assert.Equal(42, received);
    }

    [Fact]
    public void MultipleSubscribers_AllReceiveMessage()
    {
        var bus = new SimDataBus();
        var results = new List<int>();

        bus.Subscribe<int>(x => results.Add(x));
        bus.Subscribe<int>(x => results.Add(x * 2));
        bus.Publish(5);

        Assert.Equal(2, results.Count);
        Assert.Contains(5, results);
        Assert.Contains(10, results);
    }

    [Fact]
    public void Unsubscribe_StopsDelivery()
    {
        var bus = new SimDataBus();
        int received = 0;
        Action<int> handler = x => received = x;

        bus.Subscribe(handler);
        bus.Publish(1);
        Assert.Equal(1, received);

        bus.Unsubscribe(handler);
        bus.Publish(99);

        Assert.Equal(1, received); // still the first value — handler was not called again
    }

    [Fact]
    public void PublishFromBackgroundThread_IsReceived()
    {
        var bus = new SimDataBus();
        var tcs = new TaskCompletionSource<int>();

        bus.Subscribe<int>(x => tcs.TrySetResult(x));

        Task.Run(() => bus.Publish(7));

        Assert.True(tcs.Task.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(7, tcs.Task.Result);
    }

    [Fact]
    public void ConcurrentSubscribeUnsubscribeDuringPublish_DoesNotThrow()
    {
        var bus = new SimDataBus();
        var handlers = new List<Action<int>>();

        // Pre-populate with some subscribers.
        for (int i = 0; i < 20; i++)
        {
            Action<int> h = _ => { };
            handlers.Add(h);
            bus.Subscribe(h);
        }

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Publisher thread
        var publisher = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
                bus.Publish(1);
        });

        // Concurrent subscribe/unsubscribe thread
        var mutator = Task.Run(() =>
        {
            int idx = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    Action<int> h = _ => { };
                    bus.Subscribe(h);
                    bus.Unsubscribe(handlers[idx % handlers.Count]);
                    idx++;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        });

        Task.WaitAll(publisher, mutator);

        Assert.Empty(exceptions);
    }
}
