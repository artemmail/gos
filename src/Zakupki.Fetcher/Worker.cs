using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zakupki.Fetcher.Options;
using Zakupki.Fetcher.Services;

namespace Zakupki.Fetcher;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ZakupkiClient _client;
    private readonly NoticeProcessor _processor;
    private readonly XmlFolderImporter _xmlImporter;
    private readonly IOptionsMonitor<ZakupkiOptions> _optionsMonitor;

    public Worker(
        ILogger<Worker> logger,
        ZakupkiClient client,
        NoticeProcessor processor,
        XmlFolderImporter xmlImporter,
        IOptionsMonitor<ZakupkiOptions> options)
    {
        _logger = logger;
        _client = client;
        _processor = processor;
        _xmlImporter = xmlImporter;
        _optionsMonitor = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hasImportedFromFolder = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            var importedFromFolder = false;

            if (!hasImportedFromFolder)
            {
                importedFromFolder = await _xmlImporter.ImportAsync(options.XmlImportDirectory, stoppingToken);
                hasImportedFromFolder = true;
            }

            if (string.IsNullOrWhiteSpace(options.Token))
            {
                if (!importedFromFolder)
                {
                    _logger.LogWarning("Zakupki token is not configured. Set Zakupki:Token in appsettings.json or environment variables.");
                }
                else
                {
                    _logger.LogInformation("Zakupki token is not configured. Remote fetch will be skipped until the token is provided.");
                }

                if (!await WaitForNextCycleAsync(Math.Max(1, options.IntervalMinutes), stoppingToken, true))
                {
                    break;
                }

                continue;
            }

            try
            {
                await FetchOnceAsync(options, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during fetch cycle");
            }

            if (!await WaitForNextCycleAsync(options.IntervalMinutes, stoppingToken, false))
            {
                break;
            }
        }
    }

    private async Task<bool> WaitForNextCycleAsync(int intervalMinutes, CancellationToken stoppingToken, bool isWaitingForToken)
    {
        if (intervalMinutes <= 0)
        {
            return false;
        }

        if (isWaitingForToken)
        {
            _logger.LogInformation("Waiting {Interval} minute(s) before re-checking Zakupki token configuration", intervalMinutes);
        }
        else
        {
            _logger.LogInformation("Waiting {Interval} minute(s) before next polling cycle", intervalMinutes);
        }

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task FetchOnceAsync(ZakupkiOptions options, CancellationToken cancellationToken)
    {
        var daysToFetch = Math.Max(1, options.Days);
        var regions = options.Regions?.Count > 0 ? options.Regions : ZakupkiOptions.DefaultRegions;
        var documentTypes = options.DocumentTypes?.Count > 0 ? options.DocumentTypes : ZakupkiOptions.DefaultDocumentTypes;
        var subsystem = string.IsNullOrWhiteSpace(options.Subsystem) ? ZakupkiOptions.DefaultSubsystem : options.Subsystem!;

        _logger.LogInformation("Starting fetch cycle for {Days} day(s), subsystem {Subsystem}, regions {Regions} and document types {DocTypes}",
            daysToFetch,
            subsystem,
            string.Join(",", regions),
            string.Join(",", documentTypes));

        foreach (var dayOffset in Enumerable.Range(0, daysToFetch))
        {
            var date = DateTime.UtcNow.Date.AddDays(-dayOffset);
            foreach (var region in regions)
            {
                foreach (var documentType in documentTypes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _logger.LogInformation("Fetching notices for {Date} region {Region} document type {DocType}", date, region, documentType);

                    try
                    {
                        var documents = await _client.FetchByOrgRegionAsync(region, documentType, subsystem, date, cancellationToken);
                        foreach (var document in documents)
                        {
                            await _processor.ProcessAsync(document, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to fetch notices for {Date} region {Region} document type {DocType}", date, region, documentType);
                    }
                }
            }
        }

        if (options.FetchByPurchaseNumber && options.PurchaseNumbers.Count > 0)
        {
            foreach (var purchase in options.PurchaseNumbers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation("Fetching package for purchase {PurchaseNumber}", purchase);
                try
                {
                    var documents = await _client.FetchByReestrNumberAsync(purchase, cancellationToken);
                    foreach (var document in documents)
                    {
                        await _processor.ProcessAsync(document, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch package for purchase {PurchaseNumber}", purchase);
                }
            }
        }
    }
}
