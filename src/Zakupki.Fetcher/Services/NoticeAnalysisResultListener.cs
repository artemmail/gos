using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Zakupki.Fetcher.Hubs;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Options;

namespace Zakupki.Fetcher.Services;

public sealed class NoticeAnalysisResultListener : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EventBusOptions _options;
    private readonly ILogger<NoticeAnalysisResultListener> _logger;
    private readonly IHubContext<NoticeAnalysisHub> _hubContext;
    private IConnection? _connection;
    private IModel? _channel;

    public NoticeAnalysisResultListener(
        IServiceScopeFactory scopeFactory,
        IOptions<EventBusOptions> options,
        ILogger<NoticeAnalysisResultListener> logger,
        IHubContext<NoticeAnalysisHub> hubContext)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled())
        {
            _logger.LogInformation("Notice analysis result listener is disabled because the event bus is not configured.");
            return Task.CompletedTask;
        }

        stoppingToken.Register(CloseConnection);
        return Task.Run(() => ListenAsync(stoppingToken), stoppingToken);
    }

    private bool IsEnabled()
    {
        return _options.Enabled && !string.IsNullOrWhiteSpace(_options.ResolveNoticeAnalysisResultQueueName());
    }

    private async Task ListenAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                EnsureChannel();

                if (_channel == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.Received += async (_, args) => await HandleMessageAsync(args, stoppingToken);

                var queueName = _options.ResolveNoticeAnalysisResultQueueName();
                _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
                _logger.LogInformation("Listening for notice analysis results on queue {Queue}", queueName);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in notice analysis result listener loop. Reconnecting in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs args, CancellationToken cancellationToken)
    {
        try
        {
            var payload = Encoding.UTF8.GetString(args.Body.Span);
            var message = JsonSerializer.Deserialize<NoticeAnalysisResultMessage>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (message is null)
            {
                _logger.LogWarning("Received empty notice analysis result payload: {Payload}", payload);
                _channel?.BasicAck(args.DeliveryTag, false);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var analysisService = scope.ServiceProvider.GetRequiredService<NoticeAnalysisService>();
            var response = await analysisService.GetStatusAsync(message.NoticeId, message.UserId, cancellationToken);

            await _hubContext
                .Clients
                .User(message.UserId)
                .SendAsync("AnalysisUpdated", response, cancellationToken);

            _channel?.BasicAck(args.DeliveryTag, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle notice analysis result message");
            try
            {
                _channel?.BasicAck(args.DeliveryTag, false);
            }
            catch
            {
                // ignored
            }
        }
    }

    private void EnsureChannel()
    {
        if (_channel is { IsOpen: true })
        {
            return;
        }

        CloseConnection();

        var factory = new ConnectionFactory
        {
            HostName = _options.BusAccess.Host,
            UserName = _options.BusAccess.UserName,
            Password = _options.BusAccess.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            RequestedHeartbeat = TimeSpan.FromSeconds(30)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        var queueName = _options.ResolveNoticeAnalysisResultQueueName();
        _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
    }

    private void CloseConnection()
    {
        try
        {
            _channel?.Close();
        }
        catch
        {
        }

        try
        {
            _connection?.Close();
        }
        catch
        {
        }
    }

    public override void Dispose()
    {
        CloseConnection();
        base.Dispose();
    }
}
