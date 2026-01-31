using ScannerService.Core.DTOs;

namespace ScannerService.Core.Interfaces
{
    public interface IScanService
    {
        Task<ScanResult> ScanAsync(ScanRequest request);
        Task<ExportSettingsDto> GetSaveSettingAsync();
        Task UpdateSaveSettingAsync(ExportSettingsDto dto);
    }
}
