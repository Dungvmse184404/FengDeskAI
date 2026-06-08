using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class LocationRepository : ILocationRepository
{
    private readonly AppDbContext _context;

    public LocationRepository(AppDbContext context) => _context = context;

    public Task<List<Province>> GetProvincesAsync(CancellationToken ct = default)
        => _context.Set<Province>().AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

    public Task<List<District>> GetDistrictsByProvinceAsync(Guid provinceId, CancellationToken ct = default)
        => _context.Set<District>().AsNoTracking()
            .Where(d => d.ProvinceId == provinceId)
            .OrderBy(d => d.Name).ToListAsync(ct);

    public Task<List<Ward>> GetWardsByDistrictAsync(Guid districtId, CancellationToken ct = default)
        => _context.Set<Ward>().AsNoTracking()
            .Where(w => w.DistrictId == districtId)
            .OrderBy(w => w.Name).ToListAsync(ct);

    public Task<bool> WardExistsAsync(Guid wardId, CancellationToken ct = default)
        => _context.Set<Ward>().AnyAsync(w => w.Id == wardId, ct);
}
