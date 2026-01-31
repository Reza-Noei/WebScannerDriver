using ScannerService.Core.DTOs;
using ScannerService.Core.Models;

namespace ScannerService.Core.Interfaces;

public interface IScannerHardware
{
    Task<List<ScannerDto>> GetScannersAsync();
    Task<List<string>> ScanAsync(ScanJobConfiguration request);
}
