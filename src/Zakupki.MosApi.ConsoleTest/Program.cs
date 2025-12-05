using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Zakupki.MosApi;
using TenderDateFilter = Zakupki.MosApi.DateTime;

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
            var client = new MosSwaggerClient(httpClient, baseUrl, token);

            var serializerOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            try
            {
                var status = await client.GetApiTokenChecktokenAsync();
                Console.WriteLine($"Token check response: {status ?? "<null>"}");

                await FetchTodayTenders(client, serializerOptions);
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

        private static async Task FetchTodayTenders(MosSwaggerClient client, JsonSerializerOptions serializerOptions)
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
                order = new List<OrderDto>
                {
                    new OrderDto
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
                var queryString = JsonSerializer.Serialize(query, serializerOptions);
                var tenders = await client.QueriesGettendersAsync(queryString);

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
