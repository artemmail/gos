using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Options;

namespace Zakupki.Fetcher.Services;

public sealed class RabbitMqEventBusPublisher : IEventBusPublisher, IDisposable
{
    private readonly EventBusOptions _options;
    private readonly ILogger<RabbitMqEventBusPublisher> _logger;
    private readonly object _syncRoot = new();
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqEventBusPublisher(IOptions<EventBusOptions> options, ILogger<RabbitMqEventBusPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task PublishFavoriteSearchAsync(FavoriteSearchCommand command, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Event bus disabled");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var channel = EnsureChannel();
        var payload = JsonSerializer.Serialize(command);
        var body = Encoding.UTF8.GetBytes(payload);
        var commandQueueName = GetCommandQueueName();

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Headers = properties.Headers ?? new Dictionary<string, object>();
        properties.Headers["x-deduplication-header"] = command.GetDeduplicationKeyBytes();

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: commandQueueName,
            mandatory: false,
            basicProperties: properties,
            body: body);

        _logger.LogDebug(
            "Favorite search task published for user {UserId} (CollectingEnd={CollectingEnd})",
            command.UserId,
            command.CollectingEndLimit);

        return Task.CompletedTask;
    }

    public Task PublishQueryVectorRequestAsync(QueryVectorBatchRequest request, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Event bus disabled");
        }

        var requestQueue = GetQueryVectorRequestQueueName();
        cancellationToken.ThrowIfCancellationRequested();

        var channel = EnsureChannel();
        var payload = JsonSerializer.Serialize(request);
        var body = Encoding.UTF8.GetBytes(payload);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: requestQueue,
            mandatory: false,
            basicProperties: properties,
            body: body);

        _logger.LogDebug(
            "Query vector task batch published for service {ServiceId} ({Count} item(s))",
            request.ServiceId,
            request.Items?.Count ?? 0);

        return Task.CompletedTask;
    }

    public Task PublishNoticeAnalysisAsync(NoticeAnalysisQueueMessage message, CancellationToken cancellationToken)
    {
        var queueName = _options.ResolveNoticeAnalysisRequestQueueName();

        if (!_options.Enabled || string.IsNullOrWhiteSpace(queueName))
        {
            throw new InvalidOperationException("Очередь для задач анализа не настроена");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var channel = EnsureChannel();
        var payload = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(payload);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: body);

        _logger.LogDebug(
            "Notice analysis task published for notice {NoticeId} and user {UserId} (AnalysisId={AnalysisId})",
            message.NoticeId,
            message.UserId,
            message.AnalysisId);

        return Task.CompletedTask;
    }

    public Task PublishNoticeAnalysisResultAsync(NoticeAnalysisResultMessage message, CancellationToken cancellationToken)
    {
        var queueName = _options.ResolveNoticeAnalysisResultQueueName();

        if (!_options.Enabled || string.IsNullOrWhiteSpace(queueName))
        {
            _logger.LogWarning("Очередь для результатов анализа не настроена, сообщение будет пропущено");
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var channel = EnsureChannel();
        var payload = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(payload);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: body);

        _logger.LogDebug(
            "Notice analysis result published for notice {NoticeId} and user {UserId} (Status={Status})",
            message.NoticeId,
            message.UserId,
            message.Status);

        return Task.CompletedTask;
    }

    private IModel EnsureChannel()
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        lock (_syncRoot)
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            DisposeChannel();

            var factory = new ConnectionFactory
            {
                HostName = _options.BusAccess.Host,
                UserName = _options.BusAccess.UserName,
                Password = _options.BusAccess.Password,
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(30)
            };

            if (!string.IsNullOrWhiteSpace(_options.Broker))
            {
                factory.ClientProvidedName = _options.Broker;
            }

            var retries = Math.Max(1, _options.BusAccess.RetryCount);
            Exception? lastException = null;
            for (var attempt = 0; attempt < retries; attempt++)
            {
                try
                {
                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();
                    DeclareInfrastructure(_channel);
                    return _channel;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Не удалось подключиться к RabbitMQ (attempt {Attempt}/{Total})", attempt + 1, retries);
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                }
            }

            throw new InvalidOperationException("Unable to connect to RabbitMQ", lastException);
        }
    }

    private void DeclareInfrastructure(IModel channel)
    {
        var commandQueueName = GetCommandQueueName();

        channel.QueueDeclare(
            queue: commandQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var analysisRequestQueue = _options.ResolveNoticeAnalysisRequestQueueName();
        if (!string.IsNullOrWhiteSpace(analysisRequestQueue))
        {
            channel.QueueDeclare(
                queue: analysisRequestQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }

        var analysisResultQueue = _options.ResolveNoticeAnalysisResultQueueName();
        if (!string.IsNullOrWhiteSpace(analysisResultQueue))
        {
            channel.QueueDeclare(
                queue: analysisResultQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }

        if (!string.IsNullOrWhiteSpace(_options.QueryVectorRequestQueueName))
        {
            channel.QueueDeclare(
                queue: _options.QueryVectorRequestQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }

        if (!string.IsNullOrWhiteSpace(_options.QueryVectorResponseQueueName))
        {
            channel.QueueDeclare(
                queue: _options.QueryVectorResponseQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }
    }

    private string GetCommandQueueName()
    {
        var commandQueueName = _options.ResolveCommandQueueName();
        if (string.IsNullOrWhiteSpace(commandQueueName))
        {
            throw new InvalidOperationException("Command queue name is not configured in EventBus options.");
        }

        return commandQueueName;
    }

    private string GetQueryVectorRequestQueueName()
    {
        if (!string.IsNullOrWhiteSpace(_options.QueryVectorRequestQueueName))
        {
            return _options.QueryVectorRequestQueueName;
        }

        var fallback = _options.ResolveCommandQueueName();
        if (string.IsNullOrWhiteSpace(fallback))
        {
            throw new InvalidOperationException("Query vector request queue is not configured in EventBus options.");
        }

        return fallback;
    }

    private void DisposeChannel()
    {
        try
        {
            _channel?.Dispose();
        }
        catch
        {
            // ignored
        }

        try
        {
            _connection?.Dispose();
        }
        catch
        {
            // ignored
        }

        _channel = null;
        _connection = null;
    }

    public void Dispose()
    {
        DisposeChannel();
        GC.SuppressFinalize(this);
    }
}
