using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Models;

namespace Zakupki.Fetcher.Services;

public interface IQueryVectorQueueService
{
    Task<UserQueryVector> CreateAsync(string userId, CreateUserQueryVectorRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserQueryVector>> GetAllAsync(string userId, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string userId, Guid id, CancellationToken cancellationToken);

    Task ApplyVectorAsync(QueryVectorResult result, CancellationToken cancellationToken);
}
