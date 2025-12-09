using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zakupki.Fetcher.Options;
using Zakupki.Fetcher.Services;

namespace Zakupki.Fetcher;

public class MosTenderSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<MosTenderSyncWorker> _logger;
    private readonly IOptionsMonitor<MosApiOptions> _optionsMonitor;

    public MosTenderSyncWorker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<MosTenderSyncWorker> logger,
        IOptionsMonitor<MosApiOptions> optionsMonitor)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _optionsMonitor = optionsMonitor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<MosTenderSyncService>();
                await syncService.SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while syncing MOS tenders");
            }

            var intervalMinutes = Math.Max(1, _optionsMonitor.CurrentValue.SyncIntervalMinutes);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
