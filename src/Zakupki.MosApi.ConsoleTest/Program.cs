using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Zakupki.MosApi;
using Zakupki.MosApi.V2;
using TenderDateFilter = Zakupki.MosApi.DateTime;
using NeedDateFilter = Zakupki.MosApi.V2.DateTime2;
using V1OrderDto = Zakupki.MosApi.OrderDto;
using V2OrderDto = Zakupki.MosApi.V2.OrderDto;

namespace Zakupki.MosApi.ConsoleTest
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var baseUrl = GetArgument(args, "--base-url", "-b") ?? Environment.GetEnvironmentVariable("MOS_API_BASE_URL");
            var token = GetArgument(args, "--token", "-t") ?? Environment.GetEnvironmentVariable("MOS_API_TOKEN");

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                Console.Error.WriteLine("Base URL is required. Provide --base-url option or MOS_API_BASE_URL environment variable.");
                return 1;
            }

            using var httpClient = new HttpClient();
            var v1Client = new MosSwaggerClient(httpClient, baseUrl, token);
            var v2Client = new MosSwaggerClientV2(httpClient, baseUrl, token);

            try
            {
                var status = await v1Client.GetApiTokenChecktokenAsync();
                Console.WriteLine($"Token check response: {status ?? "<null>"}");

                await FetchNeedsForLastMonth(v2Client);
                await FetchTodayTenders(v1Client);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Token validation failed: {ex.Message}");
                return 1;
            }
        }

        private static string? GetArgument(string[] args, string longName, string shortName)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], longName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i], shortName, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static async Task FetchNeedsForLastMonth(MosSwaggerClientV2 client)
        {
            var now = DateTimeOffset.Now;
            var monthAgo = now.AddMonths(-1);

            var query = new SearchQuery4
            {
                filter = new SearchQueryFilterDto2
                {
                    publishDate = new NeedDateFilter
                    {
                        start = monthAgo,
                        end = now
                    }
                },
                order = new List<V2OrderDto>
                {
                    new V2OrderDto
                    {
                        field = "PublishDate",
                        desc = true
                    }
                },
                skip = 0,
                take = 1000,
                withCount = true
            };

            Console.WriteLine(
                $"Fetching needs published between {monthAgo:yyyy-MM-dd} and {now:yyyy-MM-dd} in batches of {query.take}...");

            var allNeeds = new List<NeedDto2>();

            while (true)
            {
                var needs = await client.NeedSearchAsync(query);

                if (needs?.items == null || needs.items.Count == 0)
                {
                    Console.WriteLine("No needs found for the specified period.");
                    return;
                }

                allNeeds.AddRange(needs.items);

                var totalAvailable = needs.count ?? allNeeds.Count;

                Console.WriteLine(
                    $"Fetched {allNeeds.Count} of {totalAvailable} need(s)...");

                if (allNeeds.Count >= totalAvailable || needs.items.Count < (query.take ?? 0))
                {
                    break;
                }

                query.skip = (query.skip ?? 0) + (query.take ?? 0);
            }

            var fileName = $"needs_{monthAgo:yyyyMMdd}_{now:yyyyMMdd}.json";
            var json = JsonSerializer.Serialize(
                allNeeds,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

            await File.WriteAllTextAsync(fileName, json);

            Console.WriteLine(
                $"Saved {allNeeds.Count} need(s) to {fileName}.");
        }

        private static async Task FetchTodayTenders(MosSwaggerClient client)
        {
            var todayStart = DateTimeOffset.Now.Date;
            var tomorrowStart = todayStart.AddDays(1);

            var query = new GetTendersQuery
            {
                filter = new TenderFilterDto
                {
                    registrationDate = new TenderDateFilter
                    {
                        start = todayStart,
                        end = tomorrowStart
                    }
                },
                order = new List<V1OrderDto>
                {
                    new V1OrderDto
                    {
                        field = "RegistrationDate",
                        desc = true
                    }
                },
                skip = 0,
                take = 100,
                withCount = true
            };

            Console.WriteLine($"Fetching tenders registered on {todayStart:yyyy-MM-dd}...");

            var totalFetched = 0;
            while (true)
            {
                var tenders = await client.QueriesGettendersAsync(query);

                if (tenders?.items == null || tenders.items.Count == 0)
                {
                    Console.WriteLine("No tenders found for the specified date range.");
                    return;
                }

                foreach (var tender in tenders.items)
                {
                    Console.WriteLine(
                        $"[{tender.registrationDate:yyyy-MM-dd HH:mm}] {tender.registerNumber} {tender.name}");
                }

                totalFetched += tenders.items.Count;
                var totalAvailable = tenders.count ?? totalFetched;

                if (totalFetched >= totalAvailable || tenders.items.Count < (query.take ?? 0))
                {
                    Console.WriteLine($"Fetched {totalFetched} tender(s) for today.");
                    return;
                }

                query.skip = (query.skip ?? 0) + (query.take ?? 0);
            }
        }
    }
}
