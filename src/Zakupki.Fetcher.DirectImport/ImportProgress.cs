using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;

namespace Zakupki.Fetcher.DirectImport;

internal sealed record ImportWindow(byte Region, string DocumentType, DateTime Day, string Key);

internal sealed class ImportProgress : IAsyncDisposable
{
    private const string Prefix = "DirectImport:v1:";
    private readonly NoticeDbContext _db;
    private readonly Dictionary<string, ImportBatch> _batches = new(StringComparer.Ordinal);
    private bool _lockAcquired;
    private DateTime _today;

    public ImportProgress(NoticeDbContext db) => _db = db;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _db.Database.OpenConnectionAsync(cancellationToken);
        await _db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = 'Zakupki.DirectImport',
                @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;
            IF @result < 0 THROW 51000, N'Другой экземпляр DirectImport уже выполняет загрузку в эту БД.', 1;
            """, cancellationToken);
        _lockAcquired = true;

        var batches = await _db.ImportBatches
            .Where(b => b.SourceFileName.StartsWith(Prefix))
            .ToListAsync(cancellationToken);
        foreach (var batch in batches)
            _batches.Add(batch.SourceFileName, batch);
    }

    public async Task<IReadOnlyList<ImportWindow>> PlanAsync(
        IEnumerable<byte> regions, IEnumerable<string> documentTypes, DateTime today,
        int days, bool fetchPackages, CancellationToken cancellationToken, bool autoCalculateDays = true)
    {
        today = today.Date;
        _today = today;
        var windows = new List<ImportWindow>();
        var initialStart = today.AddDays(-days);
        var selectedRegions = regions.Distinct().ToArray();
        var selectedDocumentTypes = documentTypes.Distinct(StringComparer.Ordinal).ToArray();
        var bootstrapRegions = selectedRegions.Where(region => selectedDocumentTypes.Any(documentType =>
            !_batches.ContainsKey($"{Prefix}{region:D2}:{documentType}:start"))).ToArray();
        var lastImports = new Dictionary<byte, DateTime>();
        if (autoCalculateDays && bootstrapRegions.Length > 0)
        {
            lastImports = await _db.NoticeVersions
                .Where(v => bootstrapRegions.Contains(v.Notice.Region) && v.LastSeenAt > DateTime.MinValue)
                .GroupBy(v => v.Notice.Region)
                .Select(group => new { Region = group.Key, LastSeenAt = group.Max(v => v.LastSeenAt) })
                .ToDictionaryAsync(item => item.Region, item => item.LastSeenAt, cancellationToken);
        }

        // Recheck yesterday even with Days=0 so a partial scan of today is finalized.
        var refreshStart = today.AddDays(-Math.Max(1, days));
        foreach (var region in selectedRegions)
        foreach (var documentType in selectedDocumentTypes)
        {
            var stream = $"{Prefix}{region:D2}:{documentType}";
            var bootstrapStart = initialStart;
            if (autoCalculateDays && lastImports.TryGetValue(region, out var lastImport))
            {
                var lastImportDay = DateTime.SpecifyKind(lastImport, DateTimeKind.Utc).ToLocalTime().Date;
                if (lastImportDay < bootstrapStart)
                    bootstrapStart = lastImportDay;
            }

            var baseline = GetOrAdd(stream + ":start", bootstrapStart);
            var savedStart = DateTime.ParseExact(baseline.Period!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (initialStart < savedStart)
            {
                savedStart = initialStart;
                baseline.Period = FormatDate(savedStart);
            }

            var start = autoCalculateDays ? savedStart : initialStart;
            for (var day = start; day <= today; day = day.AddDays(1))
            {
                var key = $"{stream}:{fetchPackages}:{FormatDate(day)}";
                if (!IsCompleted(key) || day >= refreshStart)
                    windows.Add(new ImportWindow(region, documentType, day, key));
            }
        }

        // Persist the initial boundary before any requests, including empty/error cycles.
        await _db.SaveChangesAsync(cancellationToken);
        return windows.OrderBy(w => IsCompleted(w.Key)).ThenBy(w => w.Day)
            .ThenBy(w => w.Region).ThenBy(w => w.DocumentType, StringComparer.Ordinal).ToArray();
    }

    public async Task BeginAsync(ImportWindow window, CancellationToken cancellationToken)
    {
        var batch = GetOrAdd(window.Key, window.Day);
        batch.Status = "Pending";
        batch.CompletedAt = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(ImportWindow window, CancellationToken cancellationToken)
    {
        var batch = _batches[window.Key];
        // Today's archive can grow after this successful scan, including while we are offline.
        var isClosedDay = window.Day < _today;
        batch.Status = isClosedDay ? "Completed" : "Pending";
        batch.CompletedAt = isClosedDay ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private bool IsCompleted(string key) =>
        _batches.TryGetValue(key, out var batch) && batch.Status == "Completed";

    private ImportBatch GetOrAdd(string key, DateTime day)
    {
        if (_batches.TryGetValue(key, out var batch))
            return batch;

        batch = new ImportBatch
        {
            Id = Guid.NewGuid(), SourceFileName = key, Period = FormatDate(day),
            Status = "Pending", CreatedAt = DateTime.UtcNow
        };
        _batches.Add(key, batch);
        _db.ImportBatches.Add(batch);
        return batch;
    }

    private static string FormatDate(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_lockAcquired)
                await _db.Database.ExecuteSqlRawAsync("""
                    EXEC sys.sp_releaseapplock @Resource = 'Zakupki.DirectImport', @LockOwner = 'Session';
                    """);
        }
        finally
        {
            await _db.DisposeAsync();
        }
    }
}
