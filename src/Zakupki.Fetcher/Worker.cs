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
    private ZakupkiOptions Options { get; }

    public Worker(
        ILogger<Worker> logger,
        ZakupkiClient client,
        NoticeProcessor processor,
        IOptions<ZakupkiOptions> options)
    {
        _logger = logger;
        _client = client;
        _processor = processor;
        Options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(Options.Token))
        {
            _logger.LogWarning("Zakupki token is not configured. Set Zakupki:Token in appsettings.json or environment variables.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FetchOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during fetch cycle");
            }

            if (Options.IntervalMinutes <= 0)
            {
                break;
            }

            _logger.LogInformation("Waiting {Interval} minutes before next polling cycle", Options.IntervalMinutes);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(Options.IntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task FetchOnceAsync(CancellationToken cancellationToken)
    {
        var daysToFetch = Math.Max(1, Options.Days);
        var regions = Options.Regions?.Count > 0 ? Options.Regions : ZakupkiOptions.DefaultRegions;
        var documentTypes = Options.DocumentTypes?.Count > 0 ? Options.DocumentTypes : ZakupkiOptions.DefaultDocumentTypes;
        var subsystem = string.IsNullOrWhiteSpace(Options.Subsystem) ? ZakupkiOptions.DefaultSubsystem : Options.Subsystem!;

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

        if (Options.FetchByPurchaseNumber && Options.PurchaseNumbers.Count > 0)
        {
            foreach (var purchase in Options.PurchaseNumbers)
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
