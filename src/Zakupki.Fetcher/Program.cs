using AspNet.Security.OAuth.Vkontakte;
using AspNet.Security.OAuth.Yandex;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SpaServices.Extensions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Zakupki.Fetcher;
using Zakupki.Fetcher.Data;
using Zakupki.Fetcher.Data.Entities;
using Zakupki.Fetcher.Options;
using Zakupki.Fetcher.Services;

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
builder.Services.Configure<AttachmentConversionOptions>(builder.Configuration.GetSection("AttachmentConversion"));
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<EventBusOptions>(builder.Configuration.GetSection("EventBus"));
builder.Services.AddMemoryCache();
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
builder.Services.AddHttpClient<NoticeAnalysisService>();
builder.Services.AddSingleton<AttachmentContentExtractor>();
var connectionString = builder.Configuration.GetConnectionString("Default");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'Default' is not configured.");
}

builder.Services.AddDbContext<NoticeDbContext>(
    options => options.UseSqlServer(connectionString),
    contextLifetime: ServiceLifetime.Scoped,
    optionsLifetime: ServiceLifetime.Singleton);

builder.Services.AddDbContextFactory<NoticeDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<NoticeDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<UserCompanyService>();
builder.Services.AddScoped<NoticeAnalysisReportService>();
builder.Services.AddSingleton<IEventBusPublisher, RabbitMqEventBusPublisher>();
builder.Services.AddSingleton<IFavoriteSearchQueueService, FavoriteSearchQueueService>();

builder.Services.ConfigureExternalCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Name = CookieAuthenticationDefaults.CookiePrefix + "External";
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection.GetValue<string>("Key");
var jwtIssuer = jwtSection.GetValue<string>("Issuer");
var jwtAudience = jwtSection.GetValue<string>("Audience");

if (string.IsNullOrWhiteSpace(jwtKey) || string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException("JWT configuration is incomplete. Please configure Jwt:Key, Jwt:Issuer and Jwt:Audience.");
}

var jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKey);
var jwtKeyId = jwtSection.GetValue<string?>("KeyId");

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
});

authBuilder.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes)
        {
            KeyId = jwtKeyId
        },
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ClockSkew = TimeSpan.Zero
    };
});

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";
        options.SaveTokens = true;

        options.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        options.TokenEndpoint = "https://www.googleapis.com/oauth2/v4/token";
        options.UserInformationEndpoint = "https://www.googleapis.com/oauth2/v3/userinfo";

        var backchannelHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        };

        if (backchannelHandler.SupportsAutomaticDecompression)
        {
            backchannelHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        }

        var backchannelClient = new HttpClient(backchannelHandler)
        {
            Timeout = options.BackchannelTimeout,
            DefaultRequestVersion = HttpVersion.Version11,
#if NET8_0_OR_GREATER
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
#endif
            MaxResponseContentBufferSize = 10 * 1024 * 1024
        };

        backchannelClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        backchannelClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Microsoft.AspNetCore.Authentication.OAuth", "1.0"));
        backchannelClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Microsoft.AspNetCore.Authentication.Google", "1.0"));

        options.BackchannelHttpHandler = backchannelHandler;
        options.Backchannel = backchannelClient;

        options.Events.OnRemoteFailure = context =>
        {
            var redirect = $"/api/account/externallogincallback?remoteError={Uri.EscapeDataString(context.Failure?.Message ?? "auth_error")}";
            context.Response.Redirect(redirect);
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });
}

var yandexClientId = builder.Configuration["Authentication:Yandex:ClientId"];
var yandexClientSecret = builder.Configuration["Authentication:Yandex:ClientSecret"];
if (!string.IsNullOrWhiteSpace(yandexClientId) && !string.IsNullOrWhiteSpace(yandexClientSecret))
{
    authBuilder.AddYandex(options =>
    {
        options.ClientId = yandexClientId;
        options.ClientSecret = yandexClientSecret;
        options.Scope.Add("login:email");
        options.SaveTokens = true;

        options.Events.OnRemoteFailure = context =>
        {
            var redirect = $"/api/account/externallogincallback?remoteError={Uri.EscapeDataString(context.Failure?.Message ?? "auth_error")}";
            context.Response.Redirect(redirect);
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });
}

var vkClientId = builder.Configuration["Authentication:Vkontakte:ClientId"];
var vkClientSecret = builder.Configuration["Authentication:Vkontakte:ClientSecret"];
if (!string.IsNullOrWhiteSpace(vkClientId) && !string.IsNullOrWhiteSpace(vkClientSecret))
{
    authBuilder.AddVkontakte(options =>
    {
        options.ClientId = vkClientId;
        options.ClientSecret = vkClientSecret;
        options.Scope.Add("email");
        options.SaveTokens = true;
    });
}

builder.Services.AddAuthorization();

builder.Services.AddHostedService<DatabaseMigrationHostedService>();
builder.Services.AddSingleton<NoticeProcessor>();
builder.Services.AddScoped<XmlFolderImporter>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<AttachmentMarkdownService>();

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

var isDevelopment = app.Environment.IsDevelopment();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Zakupki API v1");
    options.RoutePrefix = "swagger";
});

app.UseStaticFiles();

StaticFileOptions? spaStaticFileOptions = null;

if (!isDevelopment)
{
    var staticFileContentTypeProvider = new FileExtensionContentTypeProvider();
    if (!staticFileContentTypeProvider.Mappings.ContainsKey(".webp"))
    {
        staticFileContentTypeProvider.Mappings[".webp"] = "image/webp";
    }

    var spaRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    if (!Directory.Exists(spaRoot))
    {
        app.Logger.LogWarning("SPA static files directory '{SpaRoot}' was not found. Requests will fall back to API responses only.", spaRoot);
    }
    else
    {
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
}

app.UseRouting();
app.UseCors("ClientApp");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapWhen(context =>
        !context.Request.Path.StartsWithSegments("/api") &&
        !context.Request.Path.StartsWithSegments("/swagger"),
    builder =>
    {
        builder.UseSpa(spa =>
        {
            spa.Options.SourcePath = "ClientApp";

            if (isDevelopment)
            {
                spa.UseProxyToSpaDevelopmentServer("http://localhost:4200");
                return;
            }

            if (spaStaticFileOptions is not null)
            {
                spa.Options.DefaultPage = "/index.html";
                spa.Options.DefaultPageStaticFileOptions = spaStaticFileOptions;
            }
        });
    });

await EnsureDatabaseMigratedAsync(app.Services);
await EnsureDefaultRolesAsync(app.Services);

await app.RunAsync();

static async Task EnsureDatabaseMigratedAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NoticeDbContext>();
    await dbContext.Database.MigrateAsync();
}

static async Task EnsureDefaultRolesAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    var defaultRoles = new[] { "Free" };
    foreach (var role in defaultRoles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}
