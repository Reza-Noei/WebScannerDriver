using Microsoft.EntityFrameworkCore;
using ScannerService.Core.Data;
using ScannerService.Core.DTOs;
using ScannerService.Core.Interfaces;
using ScannerService.Core.Services;

// Load .env file
var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
DotNetEnv.Env.Load(envPath);

var builder = WebApplication.CreateBuilder(args);

// Get port from .env or use default
var port = int.Parse(DotNetEnv.Env.GetString("API_PORT", "5000"));
var host = DotNetEnv.Env.GetString("API_HOST", "localhost");

builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(port));

var dbPath = Path.Combine(AppContext.BaseDirectory, "scanner.db");
builder.Services.AddDbContext<ScannerDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddSingleton<IScannerHardware, ScannerProvider>();
builder.Services.AddScoped<IScannerManager, ScannerService.Core.Services.ScannerManagerService>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IScanService, ScanService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ScannerDbContext>().Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

//Health Check Endpoint//
app.MapGet("/api/health", () => Results.Ok(new HealthDto(true, "1.0.0")));

// Scanner Endpoints//
app.MapGet("/api/scanners", async (IScannerManager svc) => Results.Ok(await svc.GetAllAsync()));
app.MapGet("/api/scanners/default", async (IScannerManager svc) =>
{
    var id = await svc.GetDefaultScannerAsync();
    return id == null ? Results.NotFound() : Results.Ok(new { scannerId = id });
});
app.MapPost("/api/scanners/default", async (SetDefaultRequest req, IScannerManager svc) =>
{
    await svc.SetDefaultScannerAsync(req.ScannerId);
    return Results.Ok();
});

// Profile Endpoints//
app.MapGet("/api/profiles", async (IProfileRepository svc) => Results.Ok(await svc.GetAllAsync()));
app.MapGet("/api/profiles/{id}", async (int id, IProfileRepository svc) =>
{
    var p = await svc.GetByIdAsync(id);
    return p == null ? Results.NotFound() : Results.Ok(p);
});
app.MapPost("/api/profiles", async (UpsertProfile req, IProfileRepository svc) =>
{
    var p = await svc.CreateAsync(req);
    return Results.Created($"/api/profiles/{p.Id}", p);
});
app.MapPut("/api/profiles/{id}", async (int id, UpsertProfile req, IProfileRepository svc) =>
{
    var p = await svc.UpdateAsync(id, req);
    return p == null ? Results.NotFound() : Results.Ok(p);
});
app.MapDelete("/api/profiles/{id}", async (int id, IProfileRepository svc) =>
{
    var deleted = await svc.DeleteAsync(id);
    return deleted ? Results.NoContent() : Results.NotFound();
});

// Scan Endpoint//
app.MapPost("/api/scan", async (ScanRequest req, IScanService svc) =>
{
    var result = await svc.ScanAsync(req);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

// ExportSettings Endpoints//
app.MapGet("/api/export-settings", async (IScanService svc) => Results.Ok(await svc.GetSaveSettingAsync()));
app.MapPut("/api/export-settings", async (ExportSettingsDto dto, IScanService svc) =>
{
    await svc.UpdateSaveSettingAsync(dto);
    return Results.Ok();
});

app.Run();

record SetDefaultRequest(string ScannerId);