using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FabrikantGrabber.Parsers;
using FabrikantGrabber.Services;

namespace FabrikantGrabber
{
    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return;
            }

            var idOrUrl = args.Length > 1 ? args[0] : string.Empty;
            var outputFolder = args.Length == 1 ? args[0] : args[1];

            Directory.CreateDirectory(outputFolder);

            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true
            };

            using var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FabrikantGrabber/1.0");

            var scraper = new FabrikantScraper(
                httpClient,
                new ProcedurePageParser(),
                new DocumentationParser(),
                new SearchPageParser());

            try
            {
                var cts = new CancellationTokenSource();

                if (string.IsNullOrWhiteSpace(idOrUrl))
                {
                    Console.WriteLine("[*] Начинаю обработку поисковой выдачи по умолчанию.");

                    var searchResult = await scraper.DownloadDefaultSearchResultsAsync(
                        outputFolder,
                        cts.Token);

                    Console.WriteLine();
                    Console.WriteLine("[OK] Готово.");
                    Console.WriteLine(" JSON: " + searchResult.JsonPath);
                    Console.WriteLine(" Всего заявок: " + searchResult.SearchResult.TotalCount);
                    Console.WriteLine(" Процедур на странице: " + searchResult.SearchResult.Procedures.Count);

                    foreach (var p in searchResult.SearchResult.Procedures)
                    {
                        Console.WriteLine($"   - {p.ProcedureId}: {p.Title}");
                    }
                }
                else if (IsSearchUrl(idOrUrl))
                {
                    Console.WriteLine("[*] Начинаю обработку: " + idOrUrl);

                    var searchResult = await scraper.DownloadSearchResultsAsync(
                        idOrUrl,
                        outputFolder,
                        cts.Token);

                    Console.WriteLine();
                    Console.WriteLine("[OK] Готово.");
                    Console.WriteLine(" JSON: " + searchResult.JsonPath);
                    Console.WriteLine(" Всего заявок: " + searchResult.SearchResult.TotalCount);
                    Console.WriteLine(" Процедур на странице: " + searchResult.SearchResult.Procedures.Count);

                    foreach (var p in searchResult.SearchResult.Procedures)
                    {
                        Console.WriteLine($"   - {p.ProcedureId}: {p.Title}");
                    }
                }
                else
                {
                    var result = await scraper.DownloadProcedureAndDocsAsync(
                        idOrUrl,
                        outputFolder,
                        cts.Token);

                    Console.WriteLine();
                    Console.WriteLine("[OK] Готово.");
                    Console.WriteLine(" JSON: " + result.JsonPath);
                    Console.WriteLine(" Документы: " + result.DocumentsFolder);
                    Console.WriteLine(" Файлов: " + result.DownloadedFiles.Count);

                    foreach (var f in result.DownloadedFiles)
                    {
                        Console.WriteLine("   - " + f);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERR] " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Использование:");
            Console.WriteLine("  FabrikantGrabber <outputFolder>");
            Console.WriteLine("  FabrikantGrabber <procedureIdOrUrl> <outputFolder>");
            Console.WriteLine("  FabrikantGrabber <searchUrl> <outputFolder>");
            Console.WriteLine();
            Console.WriteLine("Пример:");
            Console.WriteLine("  FabrikantGrabber C\\data\\fabrikant");
            Console.WriteLine("  FabrikantGrabber C5b_EKEJiiyvLHpZij08zg C\\data\\fabrikant");
            Console.WriteLine(
                "  FabrikantGrabber https://www.fabrikant.ru/v2/trades/procedure/view/C5b_EKEJiiyvLHpZij08zg C\\data\\fabrikant");
            Console.WriteLine(
                "  FabrikantGrabber https://www.fabrikant.ru/procedure/search/purchases?... C\\data\\fabrikant");
        }

        private static bool IsSearchUrl(string value) =>
            value.Contains("/procedure/search", StringComparison.OrdinalIgnoreCase);
    }
}
