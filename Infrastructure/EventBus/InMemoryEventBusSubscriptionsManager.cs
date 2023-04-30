using RecAll.Infrastructure.EventBus.Abstractions;
using RecAll.Infrastructure.EventBus.Events;

namespace RecAll.Infrastructure.EventBus; 

public class
    InMemoryEventBusSubscriptionsManager : IEventBusSubscriptionsManager {
    private readonly Dictionary<string, List<SubscriptionInfo>> _handlers;

    private readonly List<Type> _eventTypes;

    public event EventHandler<string> OnEventRemoved;

    public InMemoryEventBusSubscriptionsManager() {
        _handlers = new Dictionary<string, List<SubscriptionInfo>>();
        _eventTypes = new List<Type>();
    }

    public bool IsEmpty => !_handlers.Keys.Any();

    public void AddSubscription<TIntegrationEvent, TIntegrationEventHandler>()
        where TIntegrationEvent : IntegrationEvent
        where TIntegrationEventHandler :
        IIntegrationEventHandler<TIntegrationEvent> {
        var eventName = GetEventName<TIntegrationEvent>();

        if (!HasSubscriptionsForEvent(eventName)) {
            _handlers.Add(eventName, new List<SubscriptionInfo>());
        }

        var handlerType = typeof(TIntegrationEventHandler);
        if (_handlers[eventName].Any(p => p.HandlerType == handlerType)) {
            throw new ArgumentException(
                $"Handler type {handlerType.Name} already registered for '{eventName}'",
                nameof(TIntegrationEventHandler));
        }

        _handlers[eventName].Add(SubscriptionInfo.Typed(handlerType));

        if (!_eventTypes.Contains(typeof(TIntegrationEvent))) {
            _eventTypes.Add(typeof(TIntegrationEvent));
        }
    }


    public void
        RemoveSubscription<TIntegrationEvent, TIntegrationEventHandler>()
        where TIntegrationEvent : IntegrationEvent
        where TIntegrationEventHandler :
        IIntegrationEventHandler<TIntegrationEvent> {
        var eventName = GetEventName<TIntegrationEvent>();
        if (!HasSubscriptionsForEvent(eventName)) {
            return;
        }

        var handlerToRemove = _handlers[eventName].SingleOrDefault(p =>
            p.HandlerType == typeof(TIntegrationEventHandler));
        if (handlerToRemove is null) {
            return;
        }

        _handlers[eventName].Remove(handlerToRemove);
        if (_handlers[eventName].Any()) {
            return;
        }

        _handlers.Remove(eventName);

        var eventType = _eventTypes.SingleOrDefault(p => p.Name == eventName);
        if (eventType != null) {
            _eventTypes.Remove(eventType);
        }

        OnEventRemoved?.Invoke(this, eventName);
    }

    public bool HasSubscriptionsForEvent(string eventName) =>
        _handlers.ContainsKey(eventName);

    public Type GetEventTypeByName(string eventName) =>
        _eventTypes.SingleOrDefault(p => p.Name == eventName);

    public void Clear() => _handlers.Clear();

    public IEnumerable<SubscriptionInfo>
        GetHandlersForEvent(string eventName) =>
        _handlers[eventName];

    public string GetEventName<TIntegrationEvent>()
        where TIntegrationEvent : IntegrationEvent =>
        typeof(TIntegrationEvent).Name;
}