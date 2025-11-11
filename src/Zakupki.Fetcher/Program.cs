using Microsoft.EntityFrameworkCore;
using Zakupki.Fetcher;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Options;
using Zakupki.Fetcher.Services;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddHostedService<DatabaseMigrationHostedService>();
builder.Services.AddSingleton<NoticeProcessor>();
builder.Services.AddSingleton<XmlFolderImporter>();
builder.Services.AddHostedService<Worker>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.MapControllers();

await app.RunAsync();
