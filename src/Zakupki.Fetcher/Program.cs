using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zakupki.Fetcher;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Options;
using Zakupki.Fetcher.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<ZakupkiOptions>(builder.Configuration.GetSection("Zakupki"));
builder.Services.AddHttpClient<ZakupkiClient>();
var connectionString = builder.Configuration.GetConnectionString("Default");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'Default' is not configured.");
}

builder.Services.AddDbContextFactory<NoticeDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddSingleton<NoticeProcessor>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
