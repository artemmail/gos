using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zakupki.Fetcher.Data;

namespace Zakupki.Fetcher.Services;

public sealed class DatabaseMigrationHostedService : IHostedService
{
    private readonly IDbContextFactory<NoticeDbContext> _dbContextFactory;
    private readonly ILogger<DatabaseMigrationHostedService> _logger;

    public DatabaseMigrationHostedService(
        IDbContextFactory<NoticeDbContext> dbContextFactory,
        ILogger<DatabaseMigrationHostedService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        _logger.LogInformation("Applying database migrations");
        await dbContext.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Database migrations applied successfully");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
