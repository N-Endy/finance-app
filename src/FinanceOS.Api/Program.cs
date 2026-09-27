using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FinanceOS.Application.Abstractions;
using FinanceOS.Domain;
using FinanceOS.Domain.Entities;
using FinanceOS.Infrastructure.Data;
using FinanceOS.Infrastructure.Feeds;
using FinanceOS.Infrastructure.Jobs;
using FinanceOS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
LoadDotEnv(builder.Environment.ContentRootPath);
LoadDotEnv(Directory.GetCurrentDirectory());
var listenPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(listenPort))
{
    builder.WebHost.UseUrls($"http://+:{listenPort}");
}

var connection = Environment.GetEnvironmentVariable("FINANCEOS_CONNECTION")
                 ?? Environment.GetEnvironmentVariable("DATABASE_URL")
                 ?? builder.Configuration.GetConnectionString("Finance")
                 ?? "Data Source=financeos.db";

builder.Services.AddDbContext<FinanceDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing") || connection == "InMemory")
    {
        options.UseInMemoryDatabase("financeos-tests");
    }
    else if (IsPostgres(connection))
    {
        options.UseNpgsql(NormalizePostgres(connection));
    }
    else
    {
        options.UseSqlite(connection);
    }
});
builder.Services.AddScoped<FinanceOsService>();
builder.Services.AddScoped<DomainExceptionFilter>();
builder.Services.AddSingleton<IPasswordHasher<Owner>, PasswordHasher<Owner>>();
builder.Services.AddSingleton<IAccountFeed, ManualAccountFeed>();
builder.Services.AddHostedService<GuidanceRefreshJob>();
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "financeos";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 8;
        limiter.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
var extraOrigins = (Environment.GetEnvironmentVariable("FINANCEOS_WEB_ORIGINS") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
        policy.WithOrigins(["http://localhost:3000", "http://127.0.0.1:3000", ..extraOrigins])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
    await db.Database.EnsureCreatedAsync();
    await PlanSeeder.EnsureLatestSnapshotAsync(db);
}

app.MapOpenApi();
app.UseCors("web");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/v1/health", () => Results.Ok(new { ok = true, service = "FinanceOS" }));

app.Run();

static string Unquote(string value)
{
    value = value.Trim();
    if (value.Length >= 2 && value[0] is '"' or '\'' && value[0] == value[^1])
        return value[1..^1];
    return value;
}

static bool IsPostgres(string connection)
{
    connection = Unquote(connection);
    return connection.Contains("Host=", StringComparison.OrdinalIgnoreCase)
        || connection.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || connection.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
}

static string NormalizePostgres(string connection)
{
    connection = Unquote(connection);
    if (connection.Contains("Host=", StringComparison.OrdinalIgnoreCase))
        return connection;
    if (!Uri.TryCreate(connection, UriKind.Absolute, out var uri)
        || (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        return connection;

    var userInfo = Uri.UnescapeDataString(uri.UserInfo);
    var colon = userInfo.IndexOf(':');
    var username = colon >= 0 ? userInfo[..colon] : userInfo;
    var password = colon >= 0 ? userInfo[(colon + 1)..] : "";
    var database = uri.AbsolutePath.Trim('/');
    var port = uri.IsDefaultPort ? 5432 : uri.Port;
    return $"Host={uri.Host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
}

static void LoadDotEnv(string start)
{
    var dir = new DirectoryInfo(start);
    for (var i = 0; i < 5 && dir is not null; i++, dir = dir.Parent)
    {
        foreach (var name in new[] { ".env.local", ".env" })
        {
            var path = Path.Combine(dir.FullName, name);
            if (!File.Exists(path)) continue;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#') || !line.Contains('=')) continue;
                var eq = line.IndexOf('=');
                var key = line[..eq].Trim();
                if (key.StartsWith("export ", StringComparison.Ordinal)) key = key["export ".Length..].Trim();
                var value = line[(eq + 1)..].Trim();
                if (value.Length >= 2 && (value[0] is '"' or '\'') && value[0] == value[^1])
                    value = value[1..^1];
                if (key.Length > 0 && Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}

public partial class Program;

public sealed class DomainExceptionFilter : Microsoft.AspNetCore.Mvc.Filters.IExceptionFilter
{
    public void OnException(Microsoft.AspNetCore.Mvc.Filters.ExceptionContext context)
    {
        if (context.Exception is not DomainException ex) return;
        context.Result = new Microsoft.AspNetCore.Mvc.ObjectResult(new { error = ex.Message }) { StatusCode = 400 };
        context.ExceptionHandled = true;
    }
}
