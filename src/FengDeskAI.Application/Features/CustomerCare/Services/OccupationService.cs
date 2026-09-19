using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Catalog;

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

    /// <summary>
    /// Mặt A (ADR §4.3). Trọng số truyền vào <c>OccupationAxis.Build</c> là 1 chỉ để trục không bị coi là
    /// tắt — mặt này không trộn với gì nên <c>Wo</c> vô nghĩa; con số trả về là <c>ô · p</c> thuần.
    /// </summary>
    public async Task<IServiceResult<ProductOccupationFitResponse>> GetProductFitAsync(
        Guid productId, string? occupationCode, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetDetailAsync(productId, ct);
        if (product is null || !product.IsActive)
            return ServiceResult<ProductOccupationFitResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy sản phẩm.");
        if (product.Elements.Count == 0)
            return ServiceResult<ProductOccupationFitResponse>.Failure(
                ApiStatusCodes.NotFound, "Sản phẩm chưa gắn thuộc tính phong thủy nên chưa xét được theo nghề.");

        var p = ScoringParameters.FromRows(await _uow.ScoringConfig.GetScoringParamsAsync(ct));
        var resolver = new ElementInputResolver(await _uow.ScoringConfig.GetElementInputMapAsync(ct));
        var productInputs = (await _uow.ScoringConfig.GetProductElementInputsAsync(new[] { productId }, ct))
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<ProductElementInput>)g.ToList());
        var facts = RecommendationService.ToFacts(product, productInputs, resolver, p);

        var response = new ProductOccupationFitResponse
        {
            ProductId = productId,
            FormulaVersion = ScoringFormulaVersions.Current,
            Placement = product.Placement.ToString(),
            ProductVector = ScoreBreakdownMapping.Rows(facts.Vector),
        };

        // Hàng tiêu hao không vào bất kỳ luồng gợi ý nào (PlacementPolicy) — mặt A cũng không ngoại lệ.
        if (product.Placement == ProductPlacement.Consumable)
            return ServiceResult<ProductOccupationFitResponse>.Success(response with
            {
                NoteVi = "Hàng tiêu hao không xét phong thủy theo nghề.",
            });

        var occupations = await _uow.ScoringConfig.GetOccupationsAsync(includeInactive: false, ct);
        if (occupationCode is { Length: > 0 })
        {
            occupations = occupations
                .Where(o => string.Equals(o.Code, occupationCode, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (occupations.Count == 0)
                return ServiceResult<ProductOccupationFitResponse>.Failure(
                    ApiStatusCodes.NotFound, $"Không tìm thấy nghề '{occupationCode}' hoặc nghề đang tắt.");
        }

        var fits = occupations
            .Select(o => ToFitRow(o, facts.Vector))
            .Where(r => r is not null)
            .Select(r => r!)
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.NameVi)
            .ToList();

        return ServiceResult<ProductOccupationFitResponse>.Success(response with
        {
            Fits = fits,
            NoteVi = fits.Count == 0 ? "Chưa nghề nào có hồ sơ ngũ hành để so sánh." : null,
        });
    }

    /// <summary><c>null</c> khi nghề chưa có hồ sơ hoặc hồ sơ đều (OTHER) — không phải "50%", mà là "không xét".</summary>
    private static OccupationFitRow? ToFitRow(Occupation occupation, ElementVector productVector)
    {
        if (occupation.Profile.Count == 0) return null;

        var profile = OccupationProfileRules.ToVector(occupation.Profile.Select(r => (r.Element, r.Share)));
        var axis = OccupationAxis.Build(
            profile, destiny: null, weight: 1m,
            ScoringParamCodes.OccupationWeight, occupation.Code, occupation.NameVi);
        if (axis is null) return null;

        decimal score = Math.Round(Math.Clamp(axis.RawDirection.Dot(productVector), -1m, 1m), 3);
        return new OccupationFitRow
        {
            Code = occupation.Code,
            NameVi = occupation.NameVi,
            Score = score,
            DisplayPercent = ScoreBreakdownMapping.DisplayPercentOf(score),
            TierVi = ScoreBreakdownMapping.TierVi(score),
            Direction = ScoreBreakdownMapping.Rows(axis.RawDirection),
            ReasonVi = RecommendationScorer.DescribeVectorMatch(
                axis.RawDirection, productVector, $"hành nghề {occupation.NameVi} cần", $"hành nghề {occupation.NameVi} nên tránh"),
        };
    }
}
