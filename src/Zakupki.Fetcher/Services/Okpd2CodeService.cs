using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;

namespace Zakupki.Fetcher.Services;

public sealed class Okpd2CodeService
{
    private static readonly string CacheKey = "okpd2-codes";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly IMemoryCache _memoryCache;

    public Okpd2CodeService(IDbContextFactory<NoticeDbContext> dbContextFactory, IMemoryCache memoryCache)
    {
        _dbContextFactory = dbContextFactory;
        _memoryCache = memoryCache;
    }

    public async Task<IReadOnlyList<Okpd2Code>> GetCodesAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(CacheKey, out IReadOnlyList<Okpd2Code>? cached) && cached is not null)
        {
            return cached;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var codes = await dbContext.Okpd2Codes
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);

        _memoryCache.Set(CacheKey, codes, CacheDuration);

        return codes;
    }

    public async Task<Dictionary<string, Okpd2Code>> GetCodesByCodeAsync(CancellationToken cancellationToken)
    {
        var codes = await GetCodesAsync(cancellationToken);

        return codes.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
    }
}
