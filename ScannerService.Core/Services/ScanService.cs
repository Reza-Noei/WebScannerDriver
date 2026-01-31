using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScannerService.Core.Data;
using ScannerService.Core.DTOs;
using ScannerService.Core.Interfaces;
using ScannerService.Core.Models;

namespace ScannerService.Core.Services;

public class ScanService : IScanService
{
    private readonly IScannerHardware _scannerProvider;
    private readonly ScannerDbContext _context;
    private readonly ILogger<ScanService>? _logger;

    public ScanService(
        IScannerHardware scannerProvider,
        ScannerDbContext context,
        ILogger<ScanService>? logger = null)
    {
        _scannerProvider = scannerProvider;
        _context = context;
        _logger = logger;
    }

    public async Task<ScanResult> ScanAsync(ScanRequest request)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var profile = await _context.Profiles.FindAsync(request.ProfileId);
            if (profile == null)
                throw new InvalidOperationException($"Profile {request.ProfileId} not found");

            if (string.IsNullOrEmpty(profile.DeviceId))
                throw new InvalidOperationException("Profile does not have a scanner device assigned");

            var settings = await GetSaveSettingsInternalAsync();

            var outputPath = request.OutputPath ?? settings.OutputDirectory;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Scans"
                );
                Directory.CreateDirectory(outputPath);
            }

            var scanRequest = new ScanJobConfiguration
            {
                DeviceId = profile.DeviceId,
                PaperSource = profile.PaperSource,
                BitDepth = profile.BitDepth,
                Resolution = profile.Resolution,
                Brightness = profile.Brightness,
                Contrast = profile.Contrast,
                ImageQuality = profile.ImageQuality,
                OutputFormat = request.OutputFormat ?? settings.OutputFormat,
                OutputPath = outputPath,
                FileName = settings.FileName
            };

            _logger?.LogInformation("Starting scan with profile {ProfileName}", profile.Name);

            var files = await _scannerProvider.ScanAsync(scanRequest);

            _logger?.LogInformation("Scan completed. {Count} files created", files.Count);

            return new ScanResult(
                Success: true,
                Files: files,
                ErrorMessage: null,
                Duration: DateTime.UtcNow - startTime
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Scan failed");

            return new ScanResult(
                Success: false,
                Files: new List<string>(),
                ErrorMessage: ex.Message,
                Duration: DateTime.UtcNow - startTime
            );
        }
    }

    public async Task<ExportSettingsDto> GetSaveSettingAsync()
    {
        var settings = await GetSaveSettingsInternalAsync();

        return new ExportSettingsDto(
            settings.OutputFormat,
            settings.OutputDirectory,
            settings.FileName
        );
    }

    public async Task UpdateSaveSettingAsync(ExportSettingsDto dto)
    {
        var settings = await GetSaveSettingsInternalAsync();

        if (dto.OutputFormat != null) settings.OutputFormat = dto.OutputFormat;
        if (dto.OutputDirectory != null) settings.OutputDirectory = dto.OutputDirectory;
        if (dto.FileName != null) settings.FileName = dto.FileName;

        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    private async Task<ExportSettings> GetSaveSettingsInternalAsync()
    {
        var settings = await _context.ExportSettings.FirstOrDefaultAsync();

        if (settings == null)
        {
            settings = new ExportSettings
            {
                OutputFormat = "PDF",
                OutputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Scans"
                )
            };

            _context.ExportSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        return settings;
    }
}