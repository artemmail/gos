using Zakupki.Fetcher.Models;

namespace Zakupki.Fetcher.Services;

public interface IEventBusPublisher
{
    Task PublishFavoriteSearchAsync(FavoriteSearchCommand command, CancellationToken cancellationToken);

    Task PublishQueryVectorRequestAsync(QueryVectorBatchRequest request, CancellationToken cancellationToken);

    Task PublishNoticeAnalysisAsync(NoticeAnalysisQueueMessage message, CancellationToken cancellationToken);

    Task PublishNoticeAnalysisResultAsync(NoticeAnalysisResultMessage message, CancellationToken cancellationToken);
}
