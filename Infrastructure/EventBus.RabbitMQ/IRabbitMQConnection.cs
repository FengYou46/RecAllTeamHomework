using System;
using RabbitMQ.Client;

namespace RecAll.Infrastructure.EventBus.RabbitMQ;

public interface IRabbitMQConnection : IDisposable {
    bool IsConnected { get; }

    bool TryConnect();

    IModel CreateModel();
}