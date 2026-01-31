using Microsoft.EntityFrameworkCore;
using ScannerService.Core.Data;
using ScannerService.Core.DTOs;
using ScannerService.Core.Interfaces;

namespace ScannerService.Core.Services;

public class ScannerManagerService : IScannerManager
{
    private readonly IScannerHardware _provider;
    private readonly ScannerDbContext _context;

    public ScannerManagerService(IScannerHardware provider, ScannerDbContext context)
    {
        _provider = provider;
        _context = context;
    }

    public async Task<List<ScannerDto>> GetAllAsync()
    {
        return await _provider.GetScannersAsync();
    }

    public async Task<string?> GetDefaultScannerAsync()
    {
        var setting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "DefaultScanner");
        return setting?.Value;
    }

    public async Task SetDefaultScannerAsync(string scannerId)
    {
        var setting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == "DefaultScanner");

        if (setting == null)
        {
            setting = new Models.AppSettings
            {
                Key = "DefaultScanner",
                Value = scannerId
            };
            _context.AppSettings.Add(setting);
        }
        else
        {
            setting.Value = scannerId;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}