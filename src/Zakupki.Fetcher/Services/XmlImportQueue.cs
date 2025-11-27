using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Zakupki.Fetcher.Services;

public sealed record XmlImportRequest(string ArchivePath, string OriginalFileName);

public sealed class XmlImportQueue : IXmlImportQueue
{
    private readonly ILogger<XmlImportQueue> _logger;
    private readonly Channel<XmlImportRequest> _channel;
    private readonly string _storageDirectory;

    public XmlImportQueue(ILogger<XmlImportQueue> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<XmlImportRequest>(
            new BoundedChannelOptions(10)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });

        _storageDirectory = Path.Combine(Path.GetTempPath(), "zakupki-upload-cache");
        Directory.CreateDirectory(_storageDirectory);
    }

    public async Task EnqueueAsync(Stream archiveStream, string? fileName, CancellationToken cancellationToken)
    {
        var safeName = string.IsNullOrWhiteSpace(fileName)
            ? "archive.zip"
            : Path.GetFileName(fileName);

        var tempPath = Path.Combine(_storageDirectory, $"{Guid.NewGuid():N}_{safeName}");

        await using (var output = File.Create(tempPath))
        {
            await archiveStream.CopyToAsync(output, cancellationToken);
        }

        var request = new XmlImportRequest(tempPath, safeName);
        await _channel.Writer.WriteAsync(request, cancellationToken);
        _logger.LogInformation("Архив {Archive} принят в очередь импорта", safeName);
    }

    public IAsyncEnumerable<XmlImportRequest> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
