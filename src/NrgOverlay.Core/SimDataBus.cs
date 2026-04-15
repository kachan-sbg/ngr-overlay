using System.Collections.Immutable;

namespace NrgOverlay.Core;

public sealed class SimDataBus : ISimDataBus
{
    private readonly object _lock = new();
    private ImmutableDictionary<Type, ImmutableArray<Delegate>> _subscribers =
        ImmutableDictionary<Type, ImmutableArray<Delegate>>.Empty;

    public void Subscribe<T>(Action<T> handler)
    {
        lock (_lock)
        {
            var key = typeof(T);
            var current = _subscribers.TryGetValue(key, out var list)
                ? list
                : ImmutableArray<Delegate>.Empty;

            _subscribers = _subscribers.SetItem(key, current.Add(handler));
        }
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        lock (_lock)
        {
            var key = typeof(T);
            if (!_subscribers.TryGetValue(key, out var list))
                return;

            var updated = list.Remove(handler);
            _subscribers = updated.IsEmpty
                ? _subscribers.Remove(key)
                : _subscribers.SetItem(key, updated);
        }
    }

    public void Publish<T>(T data)
    {
        // Read the snapshot outside the lock вЂ” zero contention on the hot path.
        if (!_subscribers.TryGetValue(typeof(T), out var handlers))
            return;

        foreach (var handler in handlers)
        {
            try
            {
                ((Action<T>)handler)(data);
            }
            catch (Exception ex)
            {
                AppLog.Exception(
                    $"SimDataBus subscriber threw for message type '{typeof(T).Name}'", ex);
            }
        }
    }
}

