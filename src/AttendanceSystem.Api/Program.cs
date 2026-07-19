using AttendanceSystem.Api.Data;
using AttendanceSystem.Api.Middleware;
using AttendanceSystem.Api.Services;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using Polly;

// ---- NLog: early initialization to also capture failures during server startup ----
var logger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();

try
{
var builder = WebApplication.CreateBuilder(args);

// ---- NLog: write logs to file according to nlog.config ----
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// ---- Service registration (Dependency Injection) ----
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // JSON field names in camelCase style (a common convention for the Frontend).
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core against SQL Server (the connection string comes from appsettings.json).
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Dedicated HttpClient for the external time provider, with a short retry for transient failures.
builder.Services.AddHttpClient<IZurichTimeProvider, ZurichTimeProvider>()
    .AddTransientHttpErrorPolicy(p =>
        p.WaitAndRetryAsync(2, attempt => TimeSpan.FromMilliseconds(200 * attempt)));

builder.Services.AddScoped<IAttendanceService, AttendanceService>();

// CORS — allows the Frontend (default: localhost:5173) to call the API.
const string CorsPolicy = "frontend";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
    p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  ?? new[] { "http://localhost:5173" })
     .AllowAnyHeader()
     .AllowAnyMethod()));

var app = builder.Build();

// ---- Request pipeline (Pipeline) ----
// Centralized exception handling and mapping to HTTP codes — first in the pipeline to catch everything.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })); // liveness check

// ---- Database: run migrations + seeding on server startup ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.MigrateAndSeedAsync(db);
}

app.Run();
}
catch (Exception ex)
{
    // Logs a critical startup failure before exiting.
    logger.Error(ex, "השרת נכשל בעלייה.");
    throw;
}
finally
{
    // Ensures all accumulated logs were written to disk (especially important with async targets).
    LogManager.Shutdown();
}
