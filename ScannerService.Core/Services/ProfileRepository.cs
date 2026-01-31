using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ScannerService.Core.Data;
using ScannerService.Core.DTOs;
using ScannerService.Core.Interfaces;
using ScannerService.Core.Models;

namespace ScannerService.Core.Services;

public class ProfileRepository : IProfileRepository
{
    private readonly ScannerDbContext _context;

    public ProfileRepository(ScannerDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProfileDto>> GetAllAsync() =>
        await _context.Profiles
            .Select(DtoProjection)
            .ToListAsync();

    public async Task<ProfileDto?> GetByIdAsync(int id) =>
        await _context.Profiles
            .Where(x => x.Id == id)
            .Select(DtoProjection)
            .FirstOrDefaultAsync();

    public async Task<ProfileDto> CreateAsync(UpsertProfile req)
    {
        var entity = new Profile
        {
            Name = req.Name,
            DeviceId = req.DeviceId,
            PaperSource = req.PaperSource,
            BitDepth = req.BitDepth,
            PageSize = req.PageSize,
            HorizontalAlign = req.HorizontalAlign,
            Resolution = req.Resolution,
            Scale = req.Scale,
            Brightness = req.Brightness,
            Contrast = req.Contrast,
            ImageQuality = req.ImageQuality,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Profiles.Add(entity);
        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<ProfileDto?> UpdateAsync(int id, UpsertProfile req)
    {
        var entity = await _context.Profiles.FindAsync(id);
        if (entity is null) return null;

        entity.Name = req.Name;
        entity.DeviceId = req.DeviceId;
        entity.PaperSource = req.PaperSource;
        entity.BitDepth = req.BitDepth;
        entity.PageSize = req.PageSize;
        entity.HorizontalAlign = req.HorizontalAlign;
        entity.Resolution = req.Resolution;
        entity.Scale = req.Scale;
        entity.Brightness = req.Brightness;
        entity.Contrast = req.Contrast;
        entity.ImageQuality = req.ImageQuality;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.Profiles.FindAsync(id);
        if (entity is null) return false;

        _context.Profiles.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    // For in-Memory entities
    private static ProfileDto ToDto(Profile p) => new(
        p.Id,
        p.Name,
        p.DeviceId,
        p.PaperSource,
        p.BitDepth,
        p.PageSize,
        p.HorizontalAlign,
        p.Resolution,
        p.Scale,
        p.Brightness,
        p.Contrast,
        p.ImageQuality,
        p.CreatedAt,
        p.UpdatedAt
    );
    // For EF Core
    private static readonly Expression<Func<Profile, ProfileDto>> DtoProjection = p => new ProfileDto(
        p.Id,
        p.Name,
        p.DeviceId,
        p.PaperSource,
        p.BitDepth,
        p.PageSize,
        p.HorizontalAlign,
        p.Resolution,
        p.Scale,
        p.Brightness,
        p.Contrast,
        p.ImageQuality,
        p.CreatedAt,
        p.UpdatedAt
    );
}