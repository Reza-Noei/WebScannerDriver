using ScannerService.Core.DTOs;

namespace ScannerService.Core.Interfaces
{
    public interface IScannerManager
    {
        Task<List<ScannerDto>> GetAllAsync();
        Task<string?> GetDefaultScannerAsync();
        Task SetDefaultScannerAsync(string scannerId);
    }
}
