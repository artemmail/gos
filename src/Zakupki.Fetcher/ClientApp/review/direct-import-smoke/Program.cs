using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zakupki.EF2020;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.DirectImport;
using Zakupki.Fetcher.Models;
using Zakupki.Fetcher.Options;
using Zakupki.Fetcher.Services;

var cancellationToken = CancellationToken.None;
using var config = JsonDocument.Parse(await File.ReadAllTextAsync(args[0]));
var connection = new SqlConnectionStringBuilder(
    config.RootElement.GetProperty("DirectImport").GetProperty("ConnectionString").GetString());
// Every run owns a separate disposable database. Application data is never used for writes.
var databaseName = "Codex_DirectImport_Smoke_" + Guid.NewGuid().ToString("N");
connection.InitialCatalog = databaseName;
connection.ConnectTimeout = 15;
using var services = new ServiceCollection()
    .AddLogging()
    .AddDbContextFactory<NoticeDbContext>(options => options.UseSqlServer(connection.ConnectionString))
    .AddSingleton<RegionDeterminationService>()
    .AddSingleton<NoticeProcessor>()
    .BuildServiceProvider();
var factory = services.GetRequiredService<IDbContextFactory<NoticeDbContext>>();
await using var database = await factory.CreateDbContextAsync();
try
{
    await database.Database.EnsureCreatedAsync();
    var processor = services.GetRequiredService<NoticeProcessor>();
    var first = Notification("1234567890123456789", 1, "doc-1", "first");
    var second = Notification("1234567890123456789", 2, "doc-2", "second");
    Check(await processor.ProcessAsync(first, cancellationToken), "first notification is saved");
    Check(await processor.ProcessAsync(second, cancellationToken), "changed external ID updates the same purchase");
    Check(!await processor.ProcessAsync(second, cancellationToken), "identical retry is a no-op");
    Check(!await processor.ProcessAsync(first, cancellationToken), "old identical version is a no-op");
    Check(await processor.ProcessAsync(Notification("1234567890123456789", 0, "doc-0", "old"), cancellationToken),
        "older missing version is retained");
    Check(await database.Notices.CountAsync(n => n.PurchaseNumber == "1234567890123456789") == 1,
        "version imports create one purchase card");
    Check(await database.NoticeVersions.CountAsync() == 3, "all three historical versions are stored");
    Check(await database.NoticeVersions.CountAsync(v => v.IsActive && v.VersionNumber == 2) == 1,
        "newest version stays active after old imports");
    Check(await database.Notices.AnyAsync(n => n.PurchaseObjectInfo == "second"), "card is never downgraded");

    var concurrent = Notification("2234567890123456789", 1, "concurrent-1", "concurrent");
    var results = await Task.WhenAll(Enumerable.Range(0, 8)
        .Select(_ => processor.ProcessAsync(concurrent, cancellationToken)));
    Check(results.Count(changed => changed) == 1, "eight concurrent imports persist once");
    await Task.WhenAll(
        processor.ProcessAsync(Notification("2234567890123456789", 3, "concurrent-3", "newest"), cancellationToken),
        processor.ProcessAsync(Notification("2234567890123456789", 2, "concurrent-2", "middle"), cancellationToken));
    Check(await database.Notices.CountAsync(n => n.PurchaseNumber == "2234567890123456789") == 1,
        "concurrent new versions keep one purchase card");
    Check(await database.Notices.AnyAsync(n => n.PurchaseNumber == "2234567890123456789" && n.PurchaseObjectInfo == "newest"),
        "concurrent versions leave the newest card data");

    var contractExport = new Export
    {
        Contract = new ContractExport
        {
            RegNum = "contract-1", VersionNumber = 2, ContractSubject = "new contract",
            Foundation = new ContractFoundation
            {
                FcsOrder = new ContractFcsOrder
                {
                    Order = new ContractOrder { NotificationNumber = "1234567890123456789" }
                }
            }
        }
    };
    var contract = new NoticeDocument("smoke", "package", 77, DateTime.Today,
        "contract.xml", Encoding.UTF8.GetBytes("test"), contractExport);
    Check(await processor.ProcessAsync(contract, cancellationToken), "contract is saved");
    Check(!await processor.ProcessAsync(contract, cancellationToken), "repeated contract is a no-op");
    contractExport.Contract.VersionNumber = 1;
    contractExport.Contract.ContractSubject = "old contract";
    Check(await processor.ProcessAsync(contract, cancellationToken), "older contract version is retained");
    Check(await database.Contracts.AnyAsync(c => c.VersionNumber == 2 && c.ContractSubject == "new contract"),
        "older contract does not replace the latest contract");
    await processor.ProcessAsync(Notification("1234567890123456789", 3, "doc-3", "latest notification"), cancellationToken);
    Check(await database.Notices.CountAsync(n => n.PurchaseNumber == "1234567890123456789") == 2,
        "notification versions remain grouped independently of their contract");

    var invalid = Notification("3234567890123456789", 1, new string('x', 129), "invalid ID length");
    await ExpectFailureAsync(() => processor.ProcessAsync(invalid, cancellationToken), "failed write is reported");
    Check(!await database.Notices.AnyAsync(n => n.PurchaseNumber == "3234567890123456789"),
        "failed write rolls back the purchase");

    await TestProgressAsync(factory);
    await TestAutoCalculateDaysAsync(factory);
    await TestAutoCalculateDaysSettingsAsync(connection.ConnectionString);
    await TestSoapAsync();
    if (args.Contains("--live"))
    {
        var temporarySettings = Path.Combine(Path.GetTempPath(), databaseName + ".json");
        try
        {
            var directSettings = config.RootElement.GetProperty("DirectImport");
            var token = directSettings.GetProperty("Token").GetString();
            var applicationSettings = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(args[0])!,
                "../Zakupki.Fetcher/appsettings.json"));
            using var appConfig = JsonDocument.Parse(await File.ReadAllTextAsync(applicationSettings));
            if (string.IsNullOrWhiteSpace(token))
                token = appConfig.RootElement.GetProperty("Zakupki").GetProperty("Token").GetString();
            await File.WriteAllTextAsync(temporarySettings, JsonSerializer.Serialize(new
            {
                DirectImport = new
                {
                    Token = token, ConnectionString = connection.ConnectionString,
                    Days = 1, Regions = new byte[] { 77 }, PauseBetweenDatesSeconds = 0,
                    Limit = 1, FetchByPurchaseNumber = false, IntervalMinutes = 60, RunOnce = true
                }
            }));
            var entry = Assembly.Load("Zakupki.Fetcher.DirectImport")
                .GetType("Zakupki.Fetcher.DirectImport.Program")!.GetMethod("Main", BindingFlags.Public | BindingFlags.Static)!;
            var exitCode = await (Task<int>)entry.Invoke(null,
                [new string[] { "--appsettings", applicationSettings, "--import-settings", temporarySettings }])!;
            Check(exitCode == 0, "normal utility entry point imports from live EIS into the temporary database");
            Check(await database.ImportBatches.AnyAsync(b => b.SourceFileName.Contains("epNotification")),
                "normal utility entry point persists live progress");
        }
        finally
        {
            File.Delete(temporarySettings);
        }
    }
    Console.WriteLine("All direct-import regression checks passed.");
}
finally
{
    Check(database.Database.GetDbConnection().Database == databaseName && databaseName.StartsWith("Codex_DirectImport_Smoke_"),
        "cleanup targets this run's temporary database");
    await database.Database.EnsureDeletedAsync();
}

static async Task TestProgressAsync(IDbContextFactory<NoticeDbContext> factory)
{
    var today = new DateTime(2026, 1, 3);
    var token = CancellationToken.None;
    await using (var progress = new ImportProgress(await factory.CreateDbContextAsync()))
    {
        await progress.InitializeAsync(token);
        var windows = await progress.PlanAsync([77], ["type"], today, 2, false, token);
        Check(windows.Count == 3 && windows[0].Day == today.AddDays(-2), "initial range is oldest first");
        await progress.BeginAsync(windows[0], token);
        await progress.CompleteAsync(windows[0], token);
        await progress.BeginAsync(windows[1], token);
        await progress.BeginAsync(windows[2], token);
        await progress.CompleteAsync(windows[2], token);
        await using var competing = new ImportProgress(await factory.CreateDbContextAsync());
        await ExpectFailureAsync(() => competing.InitializeAsync(token), "parallel downloader is rejected");
    }

    await using (var resumed = new ImportProgress(await factory.CreateDbContextAsync()))
    {
        await resumed.InitializeAsync(token);
        var windows = await resumed.PlanAsync([77], ["type"], today.AddDays(10), 1, false, token);
        Check(windows.Count == 12, "restart covers the entire ten-day outage");
        Check(windows[0].Day == today.AddDays(-1), "old failed window is retried beyond Days");
        Check(windows.Any(w => w.Day == today), "last open day is retried after the long outage");
        Check(windows.All(w => w.Day != today.AddDays(-2)), "completed historical window is skipped");
        foreach (var window in windows)
        {
            await resumed.BeginAsync(window, token);
            await resumed.CompleteAsync(window, token);
        }
    }

    await using var refresh = new ImportProgress(await factory.CreateDbContextAsync());
    await refresh.InitializeAsync(token);
    var refreshed = await refresh.PlanAsync([77], ["type"], today.AddDays(11), 0, false, token);
    Check(refreshed.Count == 2 && refreshed[0].Day == today.AddDays(10), "last open day is finalized before the new day");
    Check(refreshed.Any(w => w.Day == today.AddDays(10)), "yesterday is finalized with Days=0");
    var extra = await refresh.PlanAsync([78], ["type2"], today, 2, false, token);
    Check(extra.Count == 3, "new region and document type have independent progress");
    var packages = await refresh.PlanAsync([77], ["type"], today, 2, true, token);
    Check(packages.Count == 3, "package fetching has independent progress");
    var changedMode = await refresh.PlanAsync([77], ["type"], today.AddDays(10), 1, true, token);
    Check(changedMode.Count == 13, "enabling packages preserves the boundary across a long outage");
    var wider = await refresh.PlanAsync([77], ["type"], today, 4, false, token);
    Check(wider.Any(w => w.Day == today.AddDays(-4)), "increasing Days extends the baseline");
}

static async Task TestSoapAsync()
{
    using var handler = new StubHandler();
    using var http = new HttpClient(handler);
    var client = new ZakupkiClient(http, Options.Create(new ZakupkiOptions { Token = "test" }), NullLogger<ZakupkiClient>.Instance);
    handler.Soap = "<dataInfo><archiveUrl>https://archive.test/1</archiveUrl><archiveUrl>https://archive.test/2</archiveUrl></dataInfo>";
    var docs = await client.FetchByOrgRegionAsync(77, "type", "PRIZ", DateTime.Today, CancellationToken.None);
    Check(docs.Count == 2 && handler.Downloads == 2, "all SOAP archive URLs are downloaded");
    var package = await client.FetchByReestrNumberAsync("123", CancellationToken.None);
    Check(package.Count == 2, "purchase package downloads every archive");
    handler.Soap = "<dataInfo><noData>true</noData></dataInfo>";
    Check((await client.FetchByOrgRegionAsync(77, "type", "PRIZ", DateTime.Today, CancellationToken.None)).Count == 0,
        "explicit noData is a successful empty window");
    handler.Soap = "<dataInfo><errorInfo><code>5</code><message>temporary error</message></errorInfo></dataInfo>";
    await ExpectFailureAsync(() => client.FetchByOrgRegionAsync(77, "type", "PRIZ", DateTime.Today, CancellationToken.None),
        "SOAP errorInfo is reported for retry");
    handler.Soap = "<dataInfo/>";
    await ExpectFailureAsync(() => client.FetchByOrgRegionAsync(77, "type", "PRIZ", DateTime.Today, CancellationToken.None),
        "unknown empty response is reported for retry");
}

static async Task TestAutoCalculateDaysAsync(IDbContextFactory<NoticeDbContext> factory)
{
    var today = new DateTime(2026, 1, 31);
    var lastImportDay = today.AddDays(-24);
    var token = CancellationToken.None;
    await using var db = await factory.CreateDbContextAsync();
    foreach (var (region, date) in new[] { ((byte)88, lastImportDay), ((byte)88, today.AddDays(-40)), ((byte)89, today.AddDays(-3)) })
    {
        var utcDate = DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime();
        db.Notices.Add(new Zakupki.Fetcher.Data.Entities.Notice
        {
            Id = Guid.NewGuid(), Region = region, PurchaseNumber = Guid.NewGuid().ToString("N"),
            Versions = new List<Zakupki.Fetcher.Data.Entities.NoticeVersion>
            {
                new()
                {
                    Id = Guid.NewGuid(), ExternalId = Guid.NewGuid().ToString("N"), VersionNumber = 1,
                    IsActive = true, LastSeenAt = utcDate, InsertedAt = utcDate, VersionReceivedAt = utcDate
                }
            }
        });
    }
    await db.SaveChangesAsync();

    await using (var progress = new ImportProgress(await factory.CreateDbContextAsync()))
    {
        await progress.InitializeAsync(token);
        var windows = await progress.PlanAsync([88, 89, 90], ["legacy"], today, 14, false, token);
        Check(windows.Where(w => w.Region == 88).Min(w => w.Day) == lastImportDay,
            "default automatic calculation starts from the last regional import before the journal existed");
        Check(windows.Count(w => w.Region == 88) == 25,
            "automatic calculation covers an outage longer than Days");
        Check(windows.Where(w => w.Region == 89).Min(w => w.Day) == today.AddDays(-14),
            "Days remains the minimum period when the last import was recent");
        Check(windows.Count(w => w.Region == 90) == 15,
            "a region without imported data falls back to Days");

        var manual = await progress.PlanAsync([88], ["legacy"], today, 2, false, token, autoCalculateDays: false);
        Check(manual.Count == 3 && manual.Min(w => w.Day) == today.AddDays(-2),
            "manual mode limits the period to Days");
    }

    // A newer imported document must never move the saved recovery boundary forward.
    await db.NoticeVersions.Where(v => v.Notice.Region == 88)
        .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.LastSeenAt, DateTime.UtcNow));
    await using var resumed = new ImportProgress(await factory.CreateDbContextAsync());
    await resumed.InitializeAsync(token);
    var restored = await resumed.PlanAsync([88], ["legacy"], today.AddDays(10), 2, false, token);
    Check(restored.Min(w => w.Day) == lastImportDay && restored.Count == 35,
        "reenabling automatic mode restores pending history and the entire subsequent outage");
}

static async Task TestAutoCalculateDaysSettingsAsync(string connectionString)
{
    var temporarySettings = Path.Combine(Path.GetTempPath(), "Codex_DirectImport_Settings_" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        var program = Assembly.Load("Zakupki.Fetcher.DirectImport").GetType("Zakupki.Fetcher.DirectImport.Program")!;
        var pathsType = program.GetNestedType("ConfigPaths", BindingFlags.NonPublic)!;
        var paths = Activator.CreateInstance(pathsType, temporarySettings, temporarySettings, false)!;
        var loadSettings = program.GetMethod("LoadSettings", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var configuredValue in new bool?[] { null, false, true })
        {
            var section = new Dictionary<string, object>
            {
                ["Token"] = "test", ["ConnectionString"] = connectionString,
                ["Days"] = 14, ["IntervalMinutes"] = 60
            };
            if (configuredValue.HasValue)
                section["AutoCalculateDays"] = configuredValue.Value;
            await File.WriteAllTextAsync(temporarySettings, JsonSerializer.Serialize(new { DirectImport = section }));
            var settings = loadSettings.Invoke(null, [paths])!;
            var automatic = (bool)settings.GetType().GetProperty("AutoCalculateDays")!.GetValue(settings)!;
            Check(automatic == (configuredValue ?? true),
                "configuration honors AutoCalculateDays=" + (configuredValue?.ToString() ?? "missing (default true)"));
        }
    }
    finally
    {
        File.Delete(temporarySettings);
    }
}

static NoticeDocument Notification(string number, int version, string externalId, string subject)
{
    var export = new Export
    {
        EpNotification = new EpNotificationEf2020
        {
            ExternalId = externalId, VersionNumber = version,
            CommonInfo = new CommonInfo { PurchaseNumber = number, PurchaseObjectInfo = subject }
        }
    };
    return new NoticeDocument("smoke", "epNotificationEF2020", 77, DateTime.Today,
        externalId + ".xml", Encoding.UTF8.GetBytes("test"), export);
}

static async Task ExpectFailureAsync(Func<Task> action, string message)
{
    try { await action(); }
    catch { Console.WriteLine("PASS " + message); return; }
    throw new InvalidOperationException("FAIL " + message);
}

static void Check(bool success, string message)
{
    if (!success) throw new InvalidOperationException("FAIL " + message);
    Console.WriteLine("PASS " + message);
}

sealed class StubHandler : HttpMessageHandler
{
    public string Soap { get; set; } = "";
    public int Downloads { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpContent content;
        if (request.Method == HttpMethod.Post)
            content = new StringContent("<Envelope>" + Soap + "</Envelope>");
        else
        {
            Downloads++;
            using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            using (var writer = new StreamWriter(zip.CreateEntry("notice.xml").Open()))
                writer.Write("<export/>");
            content = new ByteArrayContent(buffer.ToArray());
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
