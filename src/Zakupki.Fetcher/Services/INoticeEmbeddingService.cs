using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zakupki.Fetcher.Models;

namespace Zakupki.Fetcher.Services;

public interface INoticeEmbeddingService
{
    Task ApplyVectorAsync(IReadOnlyList<QueryVectorResult> results, CancellationToken cancellationToken);
}
