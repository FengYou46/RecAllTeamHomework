using RecAll.Infrastructure.EventBus.Abstractions;
using RecAll.Infrastructure.EventBus.Events;

namespace RecAll.Infrastructure.EventBus; 

public interface IEventBusSubscriptionsManager {
    bool IsEmpty { get; }

    event EventHandler<string> OnEventRemoved;

    void AddSubscription<TIntegrationEvent, TIntegrationEventHandler>()
        where TIntegrationEvent : IntegrationEvent
        where TIntegrationEventHandler :
        IIntegrationEventHandler<TIntegrationEvent>;

    void RemoveSubscription<TIntegrationEvent, TIntegrationEventHandler>()
        where TIntegrationEvent : IntegrationEvent
        where TIntegrationEventHandler :
        IIntegrationEventHandler<TIntegrationEvent>;

    bool HasSubscriptionsForEvent(string eventName);

    Type GetEventTypeByName(string eventName);

    void Clear();

    IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName);

    string GetEventName<TIntegrationEvent>()
        where TIntegrationEvent : IntegrationEvent;
}