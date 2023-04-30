using RecAll.Infrastructure.EventBus.Events;

namespace RecAll.Infrastructure.EventBus.Abstractions;

public interface IEventBus {
    void Publish(IntegrationEvent @event);

    void Subscribe<TIntegrationEvent, TIntegrationEventHandler>()
        where TIntegrationEvent : IntegrationEvent
        where TIntegrationEventHandler :
        IIntegrationEventHandler<TIntegrationEvent>;

    void Unsubscribe<TIntegrationEvent, TIntegrationEventHandler>()
        where TIntegrationEvent : IntegrationEvent
        where TIntegrationEventHandler :
        IIntegrationEventHandler<TIntegrationEvent>;
}