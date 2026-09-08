using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;

namespace FengDeskAI.Application.Features.CustomerCare.Services;

public sealed class OccupationService : IOccupationService
{
    private readonly IUnitOfWork _uow;

    public OccupationService(IUnitOfWork uow) => _uow = uow;

    public async Task<IServiceResult<List<OccupationOptionDto>>> GetOptionsAsync(CancellationToken ct = default)
    {
        var rows = await _uow.ScoringConfig.GetOccupationsAsync(includeInactive: false, ct);
        return ServiceResult<List<OccupationOptionDto>>.Success(rows.Select(o => new OccupationOptionDto
        {
            Id = o.Id,
            Code = o.Code,
            NameVi = o.NameVi,
            Description = o.Description,
            SortOrder = o.SortOrder,
        }).ToList());
    }
}
