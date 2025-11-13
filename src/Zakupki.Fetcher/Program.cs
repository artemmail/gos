using Microsoft.AspNetCore.SpaServices.Extensions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Zakupki.Fetcher;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Options;
using Zakupki.Fetcher.Services;
using Microsoft.Extensions.FileProviders;
using System.IO;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateScopes = context.HostingEnvironment.IsDevelopment();
    options.ValidateOnBuild = true;
});

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<ZakupkiOptions>(builder.Configuration.GetSection("Zakupki"));
builder.Services.AddSingleton(new CookieContainer());
builder.Services
    .AddHttpClient<AttachmentDownloadService>()
    .ConfigurePrimaryHttpMessageHandler(sp => new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 10,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        UseCookies = true,
        CookieContainer = sp.GetRequiredService<CookieContainer>()
    });

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
builder.Services.AddScoped<XmlFolderImporter>();
builder.Services.AddHostedService<Worker>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApp", policy =>
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Zakupki API",
        Version = "v1",
        Description = "REST API для работы с закупками"
    });
});
builder.Services.AddSpaStaticFiles(configuration =>
{
    configuration.RootPath = "wwwroot";
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Zakupki API v1");
    options.RoutePrefix = "swagger";
});

app.UseStaticFiles();

StaticFileOptions? spaStaticFileOptions = null;

if (!app.Environment.IsDevelopment())
{
    var staticFileContentTypeProvider = new FileExtensionContentTypeProvider();
    if (!staticFileContentTypeProvider.Mappings.ContainsKey(".webp"))
    {
        staticFileContentTypeProvider.Mappings[".webp"] = "image/webp";
    }

    var spaRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");

    spaStaticFileOptions = new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(spaRoot),
        ContentTypeProvider = staticFileContentTypeProvider
    };

    var defaultFilesOptions = new DefaultFilesOptions
    {
        FileProvider = spaStaticFileOptions.FileProvider
    };

    app.UseDefaultFiles(defaultFilesOptions);
    app.UseStaticFiles(spaStaticFileOptions);
    app.UseSpaStaticFiles(spaStaticFileOptions);
}

app.UseRouting();
app.UseCors("ClientApp");

app.MapControllers();

app.MapWhen(context =>
        !context.Request.Path.StartsWithSegments("/api") &&
        !context.Request.Path.StartsWithSegments("/swagger"),
    builder =>
    {
        builder.UseSpa(spa =>
        {
            spa.Options.SourcePath = "ClientApp";

            if (app.Environment.IsDevelopment())
            {
                spa.UseProxyToSpaDevelopmentServer("http://localhost:4200");
            }
            else if (spaStaticFileOptions is not null)
            {
                spa.Options.DefaultPage = "/index.html";
                spa.Options.DefaultPageStaticFileOptions = spaStaticFileOptions;
            }
        });
    });

await app.RunAsync();
