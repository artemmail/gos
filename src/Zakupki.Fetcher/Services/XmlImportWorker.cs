using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Zakupki.Fetcher.Services;

public sealed class XmlImportWorker : BackgroundService
{
    private readonly IXmlImportQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<XmlImportWorker> _logger;

    public XmlImportWorker(
        IXmlImportQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<XmlImportWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.DequeueAsync(stoppingToken))
        {
            var extractDirectory = Path.Combine(Path.GetTempPath(), "zakupki-import", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractDirectory);

            try
            {
                ZipFile.ExtractToDirectory(request.ArchivePath, extractDirectory, overwriteFiles: true);
                using var scope = _scopeFactory.CreateScope();
                var importer = scope.ServiceProvider.GetRequiredService<XmlFolderImporter>();
                await importer.ImportAsync(extractDirectory, stoppingToken);
                _logger.LogInformation("Импорт архива {Archive} завершён", request.OriginalFileName);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при импорте архива {Archive}", request.OriginalFileName);
            }
            finally
            {
                TryCleanup(request.ArchivePath);
                TryCleanup(extractDirectory, isDirectory: true);
            }
        }
    }

    private void TryCleanup(string path, bool isDirectory = false)
    {
        try
        {
            if (isDirectory && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось очистить временный ресурс {Path}", path);
        }
    }
}
