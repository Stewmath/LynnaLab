namespace Util;

/// <summary>
/// Helper class for managing subscriptions to an event.
/// </summary>
public class EventSource<T>
{
    public EventSource(Action<EventHandler<T>> subscribe, Action<EventHandler<T>> unsubscribe)
    {
        this.Subscribe = subscribe;
        this.Unsubscribe = unsubscribe;
    }

    public Action<EventHandler<T>> Subscribe { get; }
    public Action<EventHandler<T>> Unsubscribe { get; }
}
