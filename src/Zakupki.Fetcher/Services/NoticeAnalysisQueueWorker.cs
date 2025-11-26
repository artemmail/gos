using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Options;

namespace Zakupki.Fetcher.Services;

public sealed class NoticeAnalysisQueueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EventBusOptions _options;
    private readonly ILogger<NoticeAnalysisQueueWorker> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public NoticeAnalysisQueueWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<EventBusOptions> options,
        ILogger<NoticeAnalysisQueueWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled())
        {
            _logger.LogInformation("Notice analysis worker is disabled because the event bus is not configured.");
            return;
        }

        stoppingToken.Register(CloseConnection);

        while (!stoppingToken.IsCancellationRequested)
        {
            BasicGetResult? message = null;

            try
            {
                EnsureChannel();

                if (_channel == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    continue;
                }

                message = _channel.BasicGet(_options.ResolveNoticeAnalysisRequestQueueName(), false);

                if (message is null)
                {
                    await ResetStuckAnalysesAsync(stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                var payload = Encoding.UTF8.GetString(message.Body.Span);
                var queueMessage = JsonSerializer.Deserialize<NoticeAnalysisQueueMessage>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (queueMessage is null)
                {
                    _logger.LogWarning("Не удалось разобрать сообщение очереди анализа: {Payload}", payload);
                    _channel.BasicAck(message.DeliveryTag, false);
                    continue;
                }

                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<NoticeAnalysisService>();
                await service.ProcessQueueMessageAsync(queueMessage, stoppingToken);

                _channel.BasicAck(message.DeliveryTag, false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке очереди анализа");

                try
                {
                    if (message != null)
                    {
                        _channel?.BasicAck(message.DeliveryTag, false);
                    }
                }
                catch
                {
                    // ignored
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        CloseConnection();
    }

    private bool IsEnabled()
    {
        return _options.Enabled && !string.IsNullOrWhiteSpace(_options.ResolveNoticeAnalysisRequestQueueName());
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

        var queueName = _options.ResolveNoticeAnalysisRequestQueueName();
        _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
    }

    private async Task ResetStuckAnalysesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<NoticeAnalysisService>();
            var resetCount = await service.ResetStuckAnalysesAsync(cancellationToken);

            if (resetCount > 0)
            {
                _logger.LogWarning("Сброшено зависших задач анализа: {Count}", resetCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сбросе зависших задач анализа");
        }
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
