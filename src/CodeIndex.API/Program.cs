using CodeIndex.Infrastructure;
using CodeIndex.Infrastructure.OpenAI;
using CodeIndex.Workers;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

static string ValidatePg(string raw, ILogger logger)
{
    try
    {
        // Convert postgres:// URL to key=value format if needed
        if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(raw);
            var userInfo = uri.UserInfo.Split(':', 2);
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort ? 5432 : uri.Port,
                Username = Uri.UnescapeDataString(userInfo[0]),
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
                Database = uri.AbsolutePath.TrimStart('/'),
                SslMode = SslMode.Disable // local dev; change as needed
            };
            raw = builder.ToString();
        }

        var b = new NpgsqlConnectionStringBuilder(raw); // will throw if bad
        // Log a safe version (password omitted)
        logger.LogInformation("Using Postgres: Host={Host};Port={Port};Database={Db};SSLMode={SslMode}",
            b.Host, b.Port, b.Database, b.SslMode);
        return b.ToString();
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Invalid Postgres connection string. Value was: '{raw}'. Tip: ensure it's key=value pairs separated by ';' and has no extra quotes/newlines.", ex);
    }
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DB: SQLite by default, toggle Postgres via env FeatureFlags__UsePostgres=true
var usePg = builder.Configuration.GetValue<bool>("FeatureFlags:UsePostgres", false);
if (usePg)
{
    var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup");
    var cs = builder.Configuration.GetConnectionString("Default") ?? "";
    cs = ValidatePg(cs, logger);

    builder.Services.AddDbContext<CodeIndexDbContext>(opt =>
        opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
}
else
{
    builder.Services.AddDbContext<CodeIndexDbContext>(opt =>
        opt.UseSqlite(builder.Configuration.GetConnectionString("Default")));
}

builder.Services.AddHttpClient(nameof(OpenAIChatClient));
builder.Services.AddScoped<IOpenAIChatClient, OpenAIChatClient>();

// Hangfire: in-memory by default; PG optional
var hangfireUsePg = builder.Configuration.GetValue<bool>("FeatureFlags:HangfirePostgres", false);
if (hangfireUsePg)
{
    // Requires Hangfire.PostgreSql package if you enable it later
    builder.Services.AddHangfire(cfg => cfg.UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseMemoryStorage()); // swap to Postgres storage when package added
}
else
{
    builder.Services.AddHangfire(cfg => cfg.UseMemoryStorage());
}
builder.Services.AddHangfireServer();

builder.Services.AddScoped<IndexProjectJob>();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseAuthorization();

var dash = builder.Configuration.GetValue<bool>("FeatureFlags:EnableHangfireDashboard", true);
if (dash) app.UseHangfireDashboard("/jobs");

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { ok = true, version = app.Configuration["Versioning:Release"] }));

// In Program.cs after DI:
app.MapGet("/diag/openai", async (IOpenAIChatClient ai, CancellationToken ct) =>
{
    try
    {
        var thread = await ai.EnsureThreadAsync(null, ct);
        return Results.Ok(new { ok = true, thread });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// Auto-migrate on startup for dev
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CodeIndexDbContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();
