using System;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace RecAll.Infrastructure.EventBus.RabbitMQ;

public class RabbitMQConnection : IRabbitMQConnection {
    private readonly IConnectionFactory _connectionFactory;

    private readonly ILogger<RabbitMQConnection> _logger;

    private readonly int _retryCount;

    private IConnection _connection;

    private bool _isDisposed;

    private object _syncRoot = new();

    public RabbitMQConnection(IConnectionFactory connectionFactory,
        ILogger<RabbitMQConnection> logger, int retryCount = 5) {
        _connectionFactory = connectionFactory ??
            throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retryCount = retryCount;
    }

    public bool IsConnected =>
        _connection != null && _connection.IsOpen && !_isDisposed;

    public bool TryConnect() {
        _logger.LogInformation("RabbitMQ Client is trying to connect");

        lock (_syncRoot) {
            var policy = Policy.Handle<SocketException>()
                .Or<BrokerUnreachableException>().WaitAndRetry(_retryCount,
                    retryAttempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (e, timeSpan) => _logger.LogWarning(e,
                        "RabbitMQ Client could not connect after {TimeOut}s ({ExceptionMessage})",
                        $"{timeSpan.TotalSeconds:n1}", e.Message));

            policy.Execute(() =>
                _connection = _connectionFactory.CreateConnection());

            if (!IsConnected) {
                _logger.LogCritical(
                    "FATAL ERROR: RabbitMQ connections could not be created and opened");
                return false;
            }

            _connection.ConnectionShutdown += OnConnectionShutdown;
            _connection.CallbackException += OnCallbackException;
            _connection.ConnectionBlocked += OnConnectionBlocked;

            _logger.LogInformation(
                "RabbitMQ Client acquired a persistent connection to '{HostName}' and is subscribed to failure events",
                _connection.Endpoint.HostName);

            return true;
        }
    }

    private void OnConnectionShutdown(object sender, ShutdownEventArgs e) {
        if (_isDisposed) {
            return;
        }

        _logger.LogWarning(
            "A RabbitMQ connection is on shutdown. Trying to re-connect...");
        TryConnect();
    }

    private void OnCallbackException(object sender,
        CallbackExceptionEventArgs e) {
        if (_isDisposed) {
            return;
        }

        _logger.LogWarning(
            "A RabbitMQ connection throw exception. Trying to re-connect...");
        TryConnect();
    }

    private void OnConnectionBlocked(object sender,
        ConnectionBlockedEventArgs e) {
        if (_isDisposed) {
            return;
        }

        _logger.LogWarning(
            "A RabbitMQ connection is shutdown. Trying to re-connect...");
        TryConnect();
    }

    public IModel CreateModel() =>
        !IsConnected
            ? throw new InvalidOperationException(
                "No RabbitMQ connections are available to perform this action")
            : _connection.CreateModel();

    public void Dispose() {
        if (_isDisposed) {
            return;
        }

        _isDisposed = true;

        try {
            _connection.Dispose();
        } catch (Exception e) {
            _logger.LogCritical(e.ToString());
        }
    }
}