using System.Threading;
using System.Threading.Tasks;
using Zakupki.Fetcher.Models;

namespace Zakupki.Fetcher.Services;

public interface INoticeEmbeddingService
{
    Task ApplyVectorAsync(QueryVectorResult result, CancellationToken cancellationToken);
}
