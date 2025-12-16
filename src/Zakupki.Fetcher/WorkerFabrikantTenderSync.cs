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

public class FabrikantTenderSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<FabrikantTenderSyncWorker> _logger;
    private readonly IOptionsMonitor<FabrikantOptions> _optionsMonitor;

    public FabrikantTenderSyncWorker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<FabrikantTenderSyncWorker> logger,
        IOptionsMonitor<FabrikantOptions> optionsMonitor)
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
                var syncService = scope.ServiceProvider.GetRequiredService<FabrikantTenderSyncService>();
                await syncService.SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while syncing Fabrikant tenders");
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
