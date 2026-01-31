using ScannerService.Core.DTOs;

namespace ScannerService.Core.Interfaces
{
    public interface IProfileRepository
    {
        Task<List<ProfileDto>> GetAllAsync();
        Task<ProfileDto?> GetByIdAsync(int id);
        Task<ProfileDto> CreateAsync(UpsertProfile dto);
        Task<ProfileDto?> UpdateAsync(int id, UpsertProfile dto);
        Task<bool> DeleteAsync(int id);
    }
}
