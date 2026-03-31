namespace SimOverlay.Core;

public interface ISimDataBus
{
    void Publish<T>(T data);
    void Subscribe<T>(Action<T> handler);
    void Unsubscribe<T>(Action<T> handler);
}
