using System;
using System.Net.Http;
using System.Threading.Tasks;
using Zakupki.MosApi;

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

            try
            {
                var status = await client.GetApiTokenChecktokenAsync();
                Console.WriteLine($"Token check response: {status ?? "<null>"}");
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
    }
}
