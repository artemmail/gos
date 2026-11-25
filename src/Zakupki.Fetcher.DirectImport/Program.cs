using System.Text;
using System.Text.RegularExpressions;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zakupki.EF2020;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Options;
using Zakupki.Fetcher.Services;
using Zakupki.Fetcher.Utilities;

namespace Zakupki.Fetcher.DirectImport;

internal static class Program
{
    private static readonly byte[] AllRegions =
    [
        .. Enumerable.Range(1, 79).Select(x => (byte)x),
        83, 86, 87, 89, 90, 91, 92
    ];

    // The document types and text filter mirror fetch.py. The application's
    // XML model currently understands these four 44-FZ notification types.
    private static readonly string[] DocumentTypes =
    [
        "epNotificationEF2020", "epNotificationEOK2020",
        "epNotificationOK2020", "epNotificationEZK2020"
    ];

    private static readonly Regex KeywordPattern = new(
        @"разработ|доработ|модерниз|создан|внедрени|программ|\bПО\b|\bИС\b|software|информационн|автоматизац|портал|веб[- ]?разработ|сайт|мобильн",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private sealed record Settings(
        string Token, string ConnectionString, int Days, byte[] Regions,
        double PauseBetweenDatesSeconds, int Limit, bool FetchByPurchase,
        int IntervalMinutes, bool RunOnce, bool AutoCalculateDays);

    private sealed record ConfigPaths(string Appsettings, string ImportSettings, bool CheckEis);

    public static async Task<int> Main(string[] args)
    {
        ConfigPaths paths;
        try
        {
            paths = ParseArgs(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            PrintUsage();
            return 2;
        }

        if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
        {
            PrintUsage();
            return 0;
        }

        try
        {
            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };
            if (paths.CheckEis)
                return await CheckEisAsync(cancellation.Token);
            return await RunAsync(paths, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Прервано.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ошибка: {ex.Message}");
            return 1;
        }
    }

    private static ConfigPaths ParseArgs(string[] args)
    {
        var appsettings = Path.Combine("Zakupki.Fetcher", "appsettings.json");
        var importSettings = Path.Combine("Zakupki.Fetcher.DirectImport", "appsettings.json");
        var checkEis = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--help") continue;
            if (arg == "--check-eis")
            {
                checkEis = true;
                continue;
            }
            if (i + 1 >= args.Length)
                throw new ArgumentException($"Для {arg} требуется значение.");

            var value = args[++i];
            switch (arg)
            {
                case "--appsettings": appsettings = value; break;
                case "--import-settings": importSettings = value; break;
                default: throw new ArgumentException($"Неизвестный или неверный параметр: {arg} {value}");
            }
        }

        return new ConfigPaths(appsettings, importSettings, checkEis);
    }

    private static Settings LoadSettings(ConfigPaths paths)
    {
        var appsettingsPath = FindConfigPath(paths.Appsettings, "appsettings.json");
        var importSettingsPath = FindConfigPath(paths.ImportSettings, "appsettings.DirectImport.json")
            ?? throw new FileNotFoundException($"Не найден файл настроек импорта: {paths.ImportSettings}");
        var builder = new ConfigurationBuilder();
        if (appsettingsPath is not null)
            builder.AddJsonFile(appsettingsPath, optional: false);
        var config = builder
            .AddJsonFile(importSettingsPath, optional: false)
            .AddEnvironmentVariables()
            .Build();
        var section = config.GetSection("DirectImport");
        var token = string.IsNullOrWhiteSpace(section["Token"])
            ? config["Zakupki:Token"]
            : section["Token"];
        if (string.IsNullOrWhiteSpace(token) || token == "PUT_YOUR_TOKEN_HERE")
            throw new ArgumentException("Укажите DirectImport:Token или Zakupki:Token в appsettings.");

        var connectionString = string.IsNullOrWhiteSpace(section["ConnectionString"])
            ? config.GetConnectionString("Default")
            : section["ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Укажите DirectImport:ConnectionString или ConnectionStrings:Default в appsettings.");

        var days = section.GetValue<int>("Days");
        var pause = section.GetValue<double>("PauseBetweenDatesSeconds");
        var limit = section.GetValue<int>("Limit");
        var interval = section.GetValue<int>("IntervalMinutes");
        var runOnce = section.GetValue<bool>("RunOnce");
        var regions = section.GetSection("Regions").Get<byte[]>() ?? [];
        if (days < 0 || pause < 0 || !double.IsFinite(pause) || limit < 0 || interval < 1 || regions.Any(r => r == 0))
            throw new ArgumentException("Неверные параметры DirectImport: Days/Limit/PauseBetweenDatesSeconds должны быть неотрицательны, IntervalMinutes >= 1, Regions — коды от 1 до 255.");

        return new Settings(token, connectionString, days,
            regions.Length == 0 ? AllRegions : regions.Distinct().ToArray(),
            pause, limit, section.GetValue<bool>("FetchByPurchaseNumber"), interval, runOnce,
            section.GetValue("AutoCalculateDays", true));
    }

    private static string? FindConfigPath(string path, string outputFileName)
    {
        if (Path.IsPathRooted(path))
            return File.Exists(path) ? Path.GetFullPath(path) : null;

        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.GetFullPath(Path.Combine(directory.FullName, path));
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        var outputCandidate = Path.Combine(AppContext.BaseDirectory, outputFileName);
        return File.Exists(outputCandidate) ? outputCandidate : null;
    }

    private static async Task<int> RunAsync(ConfigPaths paths, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var settings = LoadSettings(paths);
            var started = DateTimeOffset.Now;
            Console.WriteLine($"Цикл импорта начат: {started:yyyy-MM-dd HH:mm:ss}; следующий через {settings.IntervalMinutes} мин после завершения.");
            int result;
            try
            {
                result = await RunCycleAsync(settings, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Ошибка цикла: {ex.Message}");
                result = 1;
            }

            if (settings.RunOnce)
                return result;

            await Task.Delay(TimeSpan.FromMinutes(settings.IntervalMinutes), cancellationToken);
        }

        return 0;
    }

    private static async Task<int> RunCycleAsync(Settings settings, CancellationToken cancellationToken)
    {

        using var services = new ServiceCollection()
            .AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Warning))
            .AddDbContextFactory<NoticeDbContext>(options => options.UseSqlServer(settings.ConnectionString))
            .AddSingleton<RegionDeterminationService>()
            .AddSingleton<NoticeProcessor>()
            .BuildServiceProvider();
        var dbFactory = services.GetRequiredService<IDbContextFactory<NoticeDbContext>>();

        await using var progress = new ImportProgress(await dbFactory.CreateDbContextAsync(cancellationToken));
        await progress.InitializeAsync(cancellationToken);
        var windows = await progress.PlanAsync(settings.Regions, DocumentTypes, DateTime.Now.Date,
            settings.Days, settings.FetchByPurchase, cancellationToken, settings.AutoCalculateDays);
        var mode = settings.AutoCalculateDays ? "автоматически" : "по Days";
        var dates = windows.Select(w => w.Day).Distinct().OrderBy(day => day).ToArray();
        if (dates.Length > 0)
            Console.WriteLine($"Период определён {mode}: {dates[0]:yyyy-MM-dd} — {dates[^1]:yyyy-MM-dd}; дат к проверке: {dates.Length}; окон загрузки: {windows.Count}.");

        using var http = CreateEisHttpClient();
        var client = new ZakupkiClient(http, Microsoft.Extensions.Options.Options.Create(new ZakupkiOptions { Token = settings.Token }),
            services.GetRequiredService<ILogger<ZakupkiClient>>());
        var processor = services.GetRequiredService<NoticeProcessor>();
        var processedDocuments = new HashSet<string>(StringComparer.Ordinal);
        var completedPackages = new HashSet<string>(StringComparer.Ordinal);
        var changedPurchases = new HashSet<string>(StringComparer.Ordinal);
        var errors = 0;

        foreach (var window in windows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await progress.BeginAsync(window, cancellationToken);
            var windowErrors = errors;
            Console.WriteLine($"[{window.Region:D2}] {window.Day:yyyy-MM-dd} PRIZ:{window.DocumentType} запрос к ЕИС...");
            try
            {
                var documents = await client.FetchByOrgRegionAsync(window.Region, window.DocumentType,
                    "PRIZ", window.Day, cancellationToken);
                foreach (var document in documents)
                {
                    if (!document.EntryName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                        !KeywordPattern.IsMatch(Encoding.UTF8.GetString(document.Content)))
                        continue;

                    try
                    {
                        var export = document.ExportModel ?? LoadExport(document.Content);
                        var notification = export.AnyNotification
                            ?? throw new InvalidDataException("XML не содержит поддерживаемого извещения.");
                        var number = notification.CommonInfo?.PurchaseNumber?.Trim();
                        if (string.IsNullOrWhiteSpace(number))
                            throw new InvalidDataException("В извещении отсутствует номер закупки.");

                        var hash = HashUtilities.ComputeSha256Hex(document.Content);
                        if (!processedDocuments.Contains(hash))
                        {
                            if (await processor.ProcessAsync(document, cancellationToken))
                            {
                                changedPurchases.Add(number);
                                Console.WriteLine($"  {number} | {notification.CommonInfo?.PurchaseObjectInfo}");
                            }
                            processedDocuments.Add(hash);
                        }

                        // Retry the package even when the notice itself was already imported.
                        if (settings.FetchByPurchase && !completedPackages.Contains(number))
                        {
                            var package = await client.FetchByReestrNumberAsync(number, cancellationToken);
                            if (package.Count == 0)
                                throw new InvalidDataException($"ЕИС вернула пустой пакет для закупки {number}.");
                            foreach (var part in package.Where(x => x.EntryName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
                            {
                                var packageExport = part.ExportModel ?? LoadExport(part.Content);
                                if (packageExport.AnyNotification is not null || packageExport.Contract is not null)
                                    await processor.ProcessAsync(part, cancellationToken);
                            }
                            completedPackages.Add(number);
                        }
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        errors++;
                        Console.Error.WriteLine($"  XML {document.EntryName}: {ex.Message}");
                    }
                }

                if (errors == windowErrors)
                    await progress.CompleteAsync(window, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                errors++;
                Console.Error.WriteLine($"[{window.Region:D2}] {window.Day:yyyy-MM-dd} {window.DocumentType}: {ex.Message}");
                if (ex.Message.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("401", StringComparison.OrdinalIgnoreCase))
                    return 1;
            }

            // Finish the current archive before honoring the limit so no entries are lost.
            if (settings.Limit > 0 && changedPurchases.Count >= settings.Limit)
                break;
            if (settings.PauseBetweenDatesSeconds > 0)
                await Task.Delay(TimeSpan.FromSeconds(settings.PauseBetweenDatesSeconds), cancellationToken);
        }

        Console.WriteLine($"Итог: сохранено закупок: {changedPurchases.Count}; ошибок: {errors}.");
        return errors == 0 ? 0 : 1;
    }

    private static Export LoadExport(byte[] content)
    {
        using var stream = new MemoryStream(content);
        return ZakupkiLoader.LoadFromStream(stream);
    }

    private static HttpClient CreateEisHttpClient() => new(new HttpClientHandler
    {
        UseProxy = false,
        ServerCertificateCustomValidationCallback = ValidateEisCertificate
    })
    {
        Timeout = TimeSpan.FromMinutes(6)
    };

    private static bool ValidateEisCertificate(
        HttpRequestMessage request, X509Certificate2? certificate,
        X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
            return true;

        var uri = request.RequestUri;
        var isEis = uri?.Scheme == Uri.UriSchemeHttps &&
            (uri.Host.Equals("zakupki.gov.ru", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.EndsWith(".zakupki.gov.ru", StringComparison.OrdinalIgnoreCase));
        var untrustedRootOnly = chain is { ChainStatus.Length: > 0 } &&
            chain.ChainStatus.All(status => status.Status == X509ChainStatusFlags.UntrustedRoot);
        if (isEis && errors == SslPolicyErrors.RemoteCertificateChainErrors && untrustedRootOnly)
            return true;

        Console.Error.WriteLine($"TLS {uri?.Host}: {errors}; chain: " +
            string.Join(", ", chain?.ChainStatus.Select(x => x.Status.ToString()) ?? []));
        return false;
    }

    private static async Task<int> CheckEisAsync(CancellationToken cancellationToken)
    {
        using var http = CreateEisHttpClient();
        foreach (var host in new[] { "int.zakupki.gov.ru", "zakupki.gov.ru", "int44.zakupki.gov.ru" })
        {
            try
            {
                using var response = await http.GetAsync(
                    $"https://{host}/eis-integration/services/getDocsIP?xsd=getDocsIP-ws-api.xsd",
                    cancellationToken);
                Console.WriteLine($"{host}: HTTP {(int)response.StatusCode}");
                if (response.IsSuccessStatusCode)
                    return 0;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Console.Error.WriteLine($"{host}: {ex.GetBaseException().Message}");
            }
        }

        return 1;
    }

    private static void PrintUsage() => Console.WriteLine(
        """
        ЕИС → SQL Server: поиск IT-закупок и импорт в таблицы приложения.
        dotnet run --project Zakupki.Fetcher.DirectImport

        --appsettings PATH        конфигурация приложения (по умолчанию Zakupki.Fetcher/appsettings.json)
        --import-settings PATH    параметры опроса (по умолчанию Zakupki.Fetcher.DirectImport/appsettings.json)
        --check-eis               проверить HTTPS ЕИС без подключения к БД
        --help                    показать справку

        Параметры поиска и интервал задаются в секции DirectImport файла appsettings.json.
        """);
}
