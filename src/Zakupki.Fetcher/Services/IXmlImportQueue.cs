using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Zakupki.Fetcher.Services;

public interface IXmlImportQueue
{
    Task EnqueueAsync(Stream archiveStream, string? fileName, CancellationToken cancellationToken);

    IAsyncEnumerable<XmlImportRequest> DequeueAsync(CancellationToken cancellationToken);
}
