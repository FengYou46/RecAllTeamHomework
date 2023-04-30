using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RecAll.Infrastructure.EventBus.Abstractions;
using RecAll.Infrastructure.EventBus.Events;

namespace RecAll.Infrastructure.EventBus.RabbitMQ;

public class RabbitMQEventBus : IEventBus, IDisposable {
    private const string BrokerName = "PadQEventBus";

    private const string AutofacScopeName = "PadQEventBus";

    private readonly IRabbitMQConnection _connection;

    private readonly ILogger<RabbitMQEventBus> _logger;

    private readonly IEventBusSubscriptionsManager _subscriptionsManager;

    private readonly ILifetimeScope _autofac;

    private readonly int _retryCount;

    private IModel _consumerChannel;

    private string _queueName;

    public RabbitMQEventBus(IRabbitMQConnection connection,
        ILogger<RabbitMQEventBus> logger, ILifetimeScope autofac,
        IEventBusSubscriptionsManager subscriptionsManager,
        string queueName = null, int retryCount = 5) {
        _connection = connection ??
            throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _subscriptionsManager = subscriptionsManager ??
            new InMemoryEventBusSubscriptionsManager();
        _queueName = queueName;
        _consumerChannel = CreateConsumerChannel();
        _autofac = autofac;
        _retryCount = retryCount;
        _subscriptionsManager.OnEventRemoved +=
            SubscriptionsManagerOnOnEventRemoved;
    }

    private IModel CreateConsumerChannel() {
        if (!_connection.IsConnected) {
            _connection.TryConnect();
        }

        _logger.LogInformation("Creating RabbitMQ consumer channel");

        var channel = _connection.CreateModel();
        channel.ExchangeDeclare(BrokerName, "direct");
        channel.QueueDeclare(_queueName, true, false, false, null);
        channel.CallbackException += (_, e) => {
            _logger.LogWarning(e.Exception,
                "Recreating RabbitMQ consumer channel");
            _consumerChannel.Dispose();
            _consumerChannel = CreateConsumerChannel();
            StartBasicConsume();
        };
        return channel;
    }

    private void StartBasicConsume() {
        _logger.LogInformation("Starting RabbitMQ basic consume");

        if (_consumerChannel is null) {
            _logger.LogError(
                "StartBasicConsume can't call on _consumerChannel is null");
            return;
        }

        var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
        consumer.Received += ConsumerOnReceived;
        _consumerChannel.BasicConsume(_queueName, false, consumer);
    }

    private async Task ConsumerOnReceived(object _, BasicDeliverEventArgs e) {
        var eventName = e.RoutingKey;
        var message = Encoding.UTF8.GetString(e.Body.Span);

        try {
            if (message.ToLowerInvariant().Contains("throw-fake-exception")) {
                throw new InvalidOperationException(
                    $"Fake exception requested: \"{message}\"");
            }

            await ProcessEvent(eventName, message);
        } catch (Exception ex) {
            _logger.LogWarning(ex,
                "----- ERROR Processing message \"{Message}\"", message);
        }

        _consumerChannel.BasicAck(e.DeliveryTag, false);
    }

    private async Task ProcessEvent(string eventName, string message) {
        _logger.LogInformation("Processing RabbitMQ event: {EventName}",
            eventName);

        if (!_subscriptionsManager.HasSubscriptionsForEvent(eventName)) {
            _logger.LogWarning(
                "No subscription for RabbitMQ event: {EventName}", eventName);
            return;
        }

        var subscriptions =
            _subscriptionsManager.GetHandlersForEvent(eventName);
        using var scope = _autofac.BeginLifetimeScope(AutofacScopeName);
        foreach (var subscription in subscriptions) {
            var handler = scope.ResolveOptional(subscription.HandlerType);
            if (handler is null) {
                continue;
            }

            var eventType = _subscriptionsManager.GetEventTypeByName(eventName);
            var integrationEvent = JsonSerializer.Deserialize(message,
                eventType,
                new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true
                });
            var concreteType = typeof(IIntegrationEventHandler<>)
                .MakeGenericType(eventType);
            await Task.Yield();
            await (Task)concreteType.GetMethod("Handle")
                .Invoke(handler, new[] { integrationEvent });
        }
    }

    private void SubscriptionsManagerOnOnEventRemoved(object _,
        string eventName) {
        if (!_connection.IsConnected) {
            _connection.TryConnect();
        }

        using var channel = _connection.CreateModel();
        channel.QueueUnbind(_queueName, BrokerName, eventName);

        if (_subscriptionsManager.IsEmpty) {
            _queueName = string.Empty;
            _consumerChannel.Close();
        }
    }

    public void Publish(IntegrationEvent @event) {
        if (!_connection.IsConnected) {
            _connection.TryConnect();
        }

        var policy = Policy.Handle<BrokerUnreachableException>()
            .Or<SocketException>().WaitAndRetry(_retryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (e, timeSpan) => _logger.LogWarning(e,
                    "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage})",
                    @event.Id, $"{timeSpan.TotalSeconds:n1}", e.Message));

        var eventName = @event.GetType().Name;
        _logger.LogInformation(
            "Creating RabbitMQ channel to publish event: {EventId} ({EventName})",
            @event.Id, eventName);

        using var channel = _connection.CreateModel();
        _logger.LogInformation(
            "Declaring RabbitMQ exchange to publish event: {EventId}",
            @event.Id);

        channel.ExchangeDeclare(BrokerName, "direct");
        var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = false });
        policy.Execute(() => {
            var properties = channel.CreateBasicProperties();
            properties.DeliveryMode = 2;

            _logger.LogInformation("Publishing event to RabbitMQ: {EventId}",
                @event.Id);

            channel.BasicPublish(BrokerName, eventName, true, properties, body);
        });
    }

    public void Subscribe<TIntegrationEvent, TIntegrationEventHandler>()
        where TIntegrationEvent : IntegrationEvent
        where TIntegrationEventHandler :
        IIntegrationEventHandler<TIntegrationEvent> {
        var eventName = _subscriptionsManager.GetEventName<TIntegrationEvent>();

        var containsKey =
            _subscriptionsManager.HasSubscriptionsForEvent(eventName);
        if (!containsKey) {
            if (!_connection.IsConnected) {
                _connection.TryConnect();
            }

            _consumerChannel.QueueBind(_queueName, BrokerName, eventName);
        }

        _logger.LogInformation(
            "Subscribing to event {EventName} with {EventHandler}", eventName,
            typeof(TIntegrationEventHandler).Name);

        _subscriptionsManager
            .AddSubscription<TIntegrationEvent, TIntegrationEventHandler>();
        StartBasicConsume();
    }

    public void Unsubscribe<TIntegrationEvent, TIntegrationEventHandler>()
        where TIntegrationEvent : IntegrationEvent
        where TIntegrationEventHandler :
        IIntegrationEventHandler<TIntegrationEvent> {
        var eventName = _subscriptionsManager.GetEventName<TIntegrationEvent>();
        _logger.LogInformation("Unsubscribing from event {EventName}",
            eventName);
        _subscriptionsManager
            .RemoveSubscription<TIntegrationEvent, TIntegrationEventHandler>();
    }

    public void Dispose() {
        _consumerChannel?.Dispose();
        _subscriptionsManager.Clear();
    }
}