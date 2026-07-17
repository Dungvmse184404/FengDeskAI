using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Application.Features.Workspace.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.Workspace.Services;

public class WorkspaceProfileService : IWorkspaceProfileService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<WorkspaceProfileService> _logger;

    public WorkspaceProfileService(IUnitOfWork uow, IMapper mapper, ILogger<WorkspaceProfileService> logger)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IServiceResult<List<WorkspaceProfileResponse>>> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var profiles = await _uow.WorkspaceProfiles.GetByUserIdAsync(userId, ct);
        var responses = new List<WorkspaceProfileResponse>(profiles.Count);
        foreach (var profile in profiles)
            responses.Add(await ToResponseAsync(profile, ct));

        return ServiceResult<List<WorkspaceProfileResponse>>.Success(responses);
    }

    public async Task<IServiceResult<WorkspaceProfileResponse>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetByIdForUserAsync(id, userId, ct);
        if (profile is null)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.WorkspaceProfile.NotFound);

        return ServiceResult<WorkspaceProfileResponse>.Success(await ToResponseAsync(profile, ct));
    }

    public async Task<IServiceResult<WorkspaceElementAnalysisResponse>> GetElementAnalysisAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetByIdForUserAsync(id, userId, ct);
        if (profile is null)
            return ServiceResult<WorkspaceElementAnalysisResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.WorkspaceProfile.NotFound);

        var ctx = await LoadAnalysisContextAsync(profile, ct);

        // ── Sản phẩm đã mua đặt trong phòng: build vector từng món (tính lúc đọc, không lưu).
        // Delivered → vào Current thật; chưa giao → chỉ vào vector PREVIEW.
        var placements = await _uow.WorkspaceProfiles.GetPlacementsAsync(profile.Id, ct);
        var placed = new List<PlacedProductResponse>();
        var deliveredContribs = new List<(ElementVector Vector, decimal VoteWeight)>();
        var previewContribs = new List<(ElementVector Vector, decimal VoteWeight)>();

        if (placements.Count > 0)
        {
            var productIds = placements.Select(p => p.ProductId).Distinct().ToList();
            var inputsByProduct = (await _uow.ScoringConfig.GetProductElementInputsAsync(productIds, ct))
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => (IReadOnlyCollection<ProductElementInput>)g.ToList());
            var prms = ScoringParameters.FromRows(await _uow.ScoringConfig.GetScoringParamsAsync(ct));

            foreach (var pl in placements)
            {
                var p = pl.Product;
                ElementVector? overridden = p is { ElementTho: { } t, ElementKim: { } k, ElementThuy: { } w, ElementMoc: { } m, ElementHoa: { } h }
                    ? new ElementVector(t, k, w, m, h)
                    : null;
                var inputs = inputsByProduct.TryGetValue(p.Id, out var list)
                    ? list
                    : Array.Empty<ProductElementInput>();

                var vector = ProductVectorProvider.Build(
                    p.IsVectorOverridden, overridden, inputs, ctx.Resolver,
                    p.Elements.Select(e => (e.Element, e.IsPrimary)), prms);
                if (vector.L1() <= 0m) continue; // sản phẩm chưa có data ngũ hành → bỏ qua

                // Phiếu = Σ weight các DecorItem code của sản phẩm trong element_input_map
                // (đồng bộ với tag hiện trạng cùng tên); không gắn DecorItem → 1 phiếu mặc định.
                var decorCodes = inputs.Where(i => i.InputKind == ElementInputKind.DecorItem).ToList();
                var voteWeight = decorCodes.Count > 0
                    ? decorCodes.Sum(c => ctx.Resolver.Resolve(c.InputKind, c.InputCode).Sum(kv => kv.Value))
                    : 1.0m;
                if (voteWeight <= 0m) voteWeight = 0m; // admin cố tình cho code weight 0 → sản phẩm không ảnh hưởng

                var isDelivered = pl.OrderItem.Delivery?.Status == Domain.Enums.Sales.DeliveryStatus.Delivered;
                var contrib = (vector, voteWeight);
                previewContribs.Add(contrib);
                if (isDelivered) deliveredContribs.Add(contrib);

                placed.Add(new PlacedProductResponse
                {
                    PlacementId = pl.Id,
                    OrderItemId = pl.OrderItemId,
                    ProductId = pl.ProductId,
                    ProductName = pl.OrderItem.ProductName,
                    ProductImage = p.Images.OrderBy(img => img.SortOrder).Select(img => img.Url).FirstOrDefault(),
                    DeliveryStatus = pl.OrderItem.Delivery?.Status.ToString() ?? "Unknown",
                    IsDelivered = isDelivered,
                    VoteWeight = Math.Round(voteWeight, 2),
                });
            }
        }

        // ── 3 vector: ideal/adjusted như cũ; current = hiện trạng + sản phẩm ĐÃ GIAO; preview = + cả đang giao.
        var ideal = WorkspaceVectorBuilder.BuildIdeal(ctx.TypeElements);
        var adjustedIdeal = WorkspaceVectorBuilder.ApplyIntent(ideal, ctx.Modifiers);
        var current = WorkspaceVectorBuilder.BuildCurrentWithProducts(ctx.ProfileInputs, ctx.Resolver, ctx.TypeElements, deliveredContribs);
        var previewCurrent = WorkspaceVectorBuilder.BuildCurrentWithProducts(ctx.ProfileInputs, ctx.Resolver, ctx.TypeElements, previewContribs);
        var gap = adjustedIdeal.Subtract(current);
        var previewGap = adjustedIdeal.Subtract(previewCurrent);
        var hasPreview = previewContribs.Count > deliveredContribs.Count;

        // Sắp giảm dần theo Gap: thiếu nhất (gap dương lớn) → thừa nhất (gap âm).
        var rows = ideal.Enumerate().Select(x => new ElementAnalysisRow
        {
            Element = x.Element.ToString(),
            Ideal = Math.Round(x.Value, 3),
            AdjustedIdeal = Math.Round(adjustedIdeal[x.Element], 3),
            Current = Math.Round(current[x.Element], 3),
            Gap = Math.Round(gap[x.Element], 3),
            PreviewCurrent = Math.Round(previewCurrent[x.Element], 3),
            PreviewGap = Math.Round(previewGap[x.Element], 3),
        })
        .OrderByDescending(r => r.Gap)
        .ToList();

        // compat% = 1 − (Σ|gap_e| / 2), tự chuẩn hóa vì 2 vector đều Σ=1 → sumAbsGap ∈ [0, 2].
        var compatibilityPercent = (int)Math.Round(100m * (1m - gap.L1() / 2m), MidpointRounding.AwayFromZero);
        var previewCompatibilityPercent = (int)Math.Round(100m * (1m - previewGap.L1() / 2m), MidpointRounding.AwayFromZero);

        var user = await _uow.Users.GetByIdAsync(userId, ct);
        var insights = SpaceInsightBuilder.Build(rows, profile.WorkPurpose, ctx.Modifiers, user?.DateOfBirth?.Year);

        var response = new WorkspaceElementAnalysisResponse
        {
            WorkspaceProfileId = profile.Id,
            DominantNeed = gap.Dominant().ToString(),
            Elements = rows,
            CompatibilityPercent = compatibilityPercent,
            Insights = insights,
            HasPreview = hasPreview,
            PreviewCompatibilityPercent = previewCompatibilityPercent,
            PlacedProducts = placed,
        };

        return ServiceResult<WorkspaceElementAnalysisResponse>.Success(response);
    }

    /// <summary>Nạp dữ liệu cấu hình rồi dựng 4 vector ngũ hành cho workspace (dùng chung công thức với engine).</summary>
    /// <summary>Dữ liệu cấu hình cần cho phân tích vector phòng — nạp 1 lần, dùng cho cả current + preview.</summary>
    private sealed record AnalysisContext(
        List<WorkspaceTypeElement> TypeElements,
        ElementInputResolver Resolver,
        List<WorkPurposeElementModifier> Modifiers,
        List<WorkspaceProfileInput> ProfileInputs);

    private async Task<AnalysisContext> LoadAnalysisContextAsync(
        Domain.Entities.Workspace.WorkspaceProfile profile, CancellationToken ct)
    {
        var typeElements = new List<WorkspaceTypeElement>();
        if (profile.WorkspaceTypeId is { } typeId
            && await _uow.WorkspaceTypes.GetByIdAsync(typeId, ct) is not null)
        {
            typeElements = await _uow.ScoringConfig.GetWorkspaceTypeElementsAsync(typeId, ct);
        }

        var resolver = new ElementInputResolver(await _uow.ScoringConfig.GetElementInputMapAsync(ct));
        var modifiers = await _uow.ScoringConfig.GetWorkPurposeModifiersAsync(profile.WorkPurpose, ct);
        var profileInputs = await _uow.ScoringConfig.GetWorkspaceProfileInputsAsync(profile.Id, ct);

        return new AnalysisContext(typeElements, resolver, modifiers, profileInputs);
    }

    // ===== Đặt sản phẩm đã mua vào workspace =====

    public async Task<IServiceResult<List<PurchasedItemResponse>>> GetPurchasedItemsAsync(Guid userId, CancellationToken ct = default)
        => ServiceResult<List<PurchasedItemResponse>>.Success(
            await _uow.WorkspaceProfiles.GetPurchasedItemsAsync(userId, ct));

    public async Task<IServiceResult> PlaceProductAsync(Guid workspaceProfileId, Guid userId, Guid orderItemId, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetByIdForUserAsync(workspaceProfileId, userId, ct);
        if (profile is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.WorkspaceProfile.NotFound);

        // Xác thực order item thuộc user + đủ điều kiện (đi qua cùng query với màn danh sách).
        var purchased = await _uow.WorkspaceProfiles.GetPurchasedItemsAsync(userId, ct);
        var item = purchased.FirstOrDefault(i => i.OrderItemId == orderItemId);
        if (item is null)
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, "Sản phẩm không thuộc lịch sử mua hợp lệ của bạn.");

        var existing = await _uow.WorkspaceProfiles.GetPlacementByOrderItemAsync(orderItemId, userId, ct);
        if (existing is not null)
        {
            if (existing.WorkspaceProfileId == workspaceProfileId)
                return ServiceResult.Success("Sản phẩm đã nằm trong không gian này.");
            // CHUYỂN phòng: giữ nguyên record, đổi FK — radar cả 2 phòng đổi theo ở lần đọc sau.
            existing.WorkspaceProfileId = workspaceProfileId;
            existing.PlacedAt = DateTime.UtcNow;
        }
        else
        {
            await _uow.WorkspaceProfiles.AddPlacementAsync(new Domain.Entities.Workspace.WorkspaceProductPlacement
            {
                UserId = userId,
                WorkspaceProfileId = workspaceProfileId,
                OrderItemId = orderItemId,
                ProductId = item.ProductId,
                PlacedAt = DateTime.UtcNow,
            }, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success(item.IsDelivered
            ? "Đã đặt sản phẩm vào không gian."
            : "Đã đặt sản phẩm vào không gian (hàng đang giao — radar hiển thị dạng xem trước).");
    }

    public async Task<IServiceResult> RemovePlacementAsync(Guid workspaceProfileId, Guid userId, Guid orderItemId, CancellationToken ct = default)
    {
        var placement = await _uow.WorkspaceProfiles.GetPlacementByOrderItemAsync(orderItemId, userId, ct);
        if (placement is null || placement.WorkspaceProfileId != workspaceProfileId)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, "Sản phẩm không nằm trong không gian này.");

        _uow.WorkspaceProfiles.RemovePlacement(placement);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã gỡ sản phẩm khỏi không gian.");
    }

    public async Task<IServiceResult<WorkspaceProfileResponse>> GetDefaultAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetDefaultByUserIdAsync(userId, ct);
        if (profile is null)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.WorkspaceProfile.NoDefault);

        return ServiceResult<WorkspaceProfileResponse>.Success(await ToResponseAsync(profile, ct));
    }

    public async Task<IServiceResult<WorkspaceProfileResponse>> CreateAsync(Guid userId, CreateWorkspaceProfileRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.WorkspaceProfile.NameRequired);
        if (request.DeskArea is <= 0)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.WorkspaceProfile.SurfaceAreaInvalid);
        if (request.WorkspaceTypeId is { } createTypeId && !await _uow.WorkspaceTypes.IsAvailableToUserAsync(createTypeId, userId, ct))
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, "Loại không gian không hợp lệ.");
        if (string.IsNullOrWhiteSpace(request.StyleCode) || await _uow.Styles.GetByIdAsync(request.StyleCode, ct) is null)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, "Mã phong cách (style) không hợp lệ.");

        var entity = _mapper.Map<Domain.Entities.Workspace.WorkspaceProfile>(request);
        entity.UserId = userId;
        entity.Name = request.Name.Trim();

        var anyExisting = (await _uow.WorkspaceProfiles.GetByUserIdAsync(userId, ct)).Count > 0;

        if (request.IsDefault || !anyExisting)
        {
            await _uow.WorkspaceProfiles.ClearDefaultsForUserAsync(userId, ct);
            entity.IsDefault = true;
        }

        await _uow.WorkspaceProfiles.AddAsync(entity, ct);

        if (request.Inputs is { Count: > 0 })
        {
            var validInputs = await ResolveValidInputsAsync(request.Inputs, ct);
            await _uow.ScoringConfig.ReplaceWorkspaceProfileInputsAsync(entity.Id, validInputs, ct);
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Workspace profile created: {ProfileId} for user {UserId}", entity.Id, userId);
        return ServiceResult<WorkspaceProfileResponse>.Success(
            await ToResponseAsync(entity, ct),
            ApiStatusMessages.WorkspaceProfile.Created,
            ApiStatusCodes.Created);
    }

    public async Task<IServiceResult<WorkspaceProfileResponse>> UpdateAsync(Guid id, Guid userId, UpdateWorkspaceProfileRequest request, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetByIdForUserAsync(id, userId, ct);
        if (profile is null)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.WorkspaceProfile.NotFound);

        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.WorkspaceProfile.NameRequired);
        if (request.DeskArea is <= 0)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.WorkspaceProfile.SurfaceAreaInvalid);
        if (request.WorkspaceTypeId is { } updateTypeId && !await _uow.WorkspaceTypes.IsAvailableToUserAsync(updateTypeId, userId, ct))
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, "Loại không gian không hợp lệ.");
        if (string.IsNullOrWhiteSpace(request.StyleCode) || await _uow.Styles.GetByIdAsync(request.StyleCode, ct) is null)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, "Mã phong cách (style) không hợp lệ.");

        _mapper.Map(request, profile);
        profile.Name = request.Name.Trim();
        _uow.WorkspaceProfiles.Update(profile);

        // null = không đổi input hiện có; [] (rỗng nhưng không null) = xóa hết.
        if (request.Inputs is not null)
        {
            var validInputs = await ResolveValidInputsAsync(request.Inputs, ct);
            await _uow.ScoringConfig.ReplaceWorkspaceProfileInputsAsync(profile.Id, validInputs, ct);
        }

        await _uow.SaveChangesAsync(ct);

        return ServiceResult<WorkspaceProfileResponse>.Success(
            await ToResponseAsync(profile, ct),
            ApiStatusMessages.WorkspaceProfile.Updated);
    }

    public async Task<IServiceResult<WorkspaceProfileResponse>> SetDefaultAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetByIdForUserAsync(id, userId, ct);
        if (profile is null)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.WorkspaceProfile.NotFound);

        return await _uow.ExecuteInTransactionAsync(async _ =>
        {
            await _uow.WorkspaceProfiles.ClearDefaultsForUserAsync(userId, ct);
            profile.IsDefault = true;
            _uow.WorkspaceProfiles.Update(profile);
            return ServiceResult<WorkspaceProfileResponse>.Success(
                await ToResponseAsync(profile, ct),
                ApiStatusMessages.WorkspaceProfile.SetDefault);
        }, ct);
    }

    public async Task<IServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetByIdForUserAsync(id, userId, ct);
        if (profile is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.WorkspaceProfile.NotFound);

        _uow.WorkspaceProfiles.Remove(profile); // SaveChangesAsync biến thành soft-delete
        await _uow.SaveChangesAsync(ct);

        return ServiceResult.Success(ApiStatusMessages.WorkspaceProfile.Deleted);
    }

    public async Task<IServiceResult<ElementInputVocabularyResponse>> GetElementInputVocabularyAsync(CancellationToken ct = default)
    {
        var map = await _uow.ScoringConfig.GetElementInputMapAsync(ct);
        var byKind = map.GroupBy(m => m.InputKind)
            .ToDictionary(g => g.Key, g => g.Select(m => m.InputCode).Distinct().OrderBy(c => c).ToList());

        var response = new ElementInputVocabularyResponse(
            byKind.GetValueOrDefault(ElementInputKind.Color, new List<string>()),
            byKind.GetValueOrDefault(ElementInputKind.Material, new List<string>()),
            byKind.GetValueOrDefault(ElementInputKind.DecorItem, new List<string>()));

        return ServiceResult<ElementInputVocabularyResponse>.Success(response);
    }

    /// <summary>Chỉ giữ input có (kind, code) tồn tại trong element_input_map — vd draft AI intake không bịa được.</summary>
    private async Task<List<WorkspaceProfileInput>> ResolveValidInputsAsync(
        IEnumerable<WorkspaceProfileInputDto> inputs, CancellationToken ct)
    {
        var validCodes = (await _uow.ScoringConfig.GetElementInputMapAsync(ct))
            .Select(m => (m.InputKind, m.InputCode))
            .ToHashSet();

        return inputs
            .Where(i => validCodes.Contains((i.InputKind, i.InputCode)))
            .DistinctBy(i => (i.InputKind, i.InputCode))
            .Select(i => new WorkspaceProfileInput { InputKind = i.InputKind, InputCode = i.InputCode })
            .ToList();
    }

    /// <summary>Map entity → response + tính % hoàn thiện hồ sơ (không lưu DB, chỉ để FE hiện progress).</summary>
    private async Task<WorkspaceProfileResponse> ToResponseAsync(
        Domain.Entities.Workspace.WorkspaceProfile profile, CancellationToken ct)
    {
        var response = _mapper.Map<WorkspaceProfileResponse>(profile);
        var (percent, hints) = await ComputeCompletenessAsync(profile, ct);
        response.CompletenessPercent = percent;
        response.MissingFieldHints = hints;

        var profileInputs = await _uow.ScoringConfig.GetWorkspaceProfileInputsAsync(profile.Id, ct);
        response.Inputs = profileInputs
            .Select(i => new WorkspaceProfileInputDto(i.InputKind, i.InputCode))
            .ToList();

        return response;
    }

    private async Task<(int Percent, List<string> Hints)> ComputeCompletenessAsync(
        Domain.Entities.Workspace.WorkspaceProfile profile, CancellationToken ct)
    {
        var hasProfileInput = (await _uow.ScoringConfig.GetWorkspaceProfileInputsAsync(profile.Id, ct)).Count > 0;

        var checks = new (bool HasValue, string Hint)[]
        {
            (profile.WorkspaceTypeId.HasValue, "Chọn loại không gian để tính trọng số cá nhân chính xác hơn"),
            (profile.Lighting.HasValue, "Thêm ánh sáng phòng để gợi ý vật phẩm hợp không gian hơn"),
            (profile.DeskType.HasValue, "Thêm loại bàn để lọc vật phẩm vừa kích thước"),
            (profile.DeskOrientation.HasValue, "Thêm hướng bàn để AI diễn giải sát hơn"),
            (profile.RoomFacingDirection.HasValue, "Thêm hướng phòng để AI diễn giải sát hơn"),
            (profile.DeskArea.HasValue, "Thêm diện tích mặt bàn để lọc vật phẩm vừa kích thước"),
            // EntranceDirection/ToiletDirection: KHÔNG có ô nhập ở form (Create/Update lẫn UI) → hint mãi
            // không thoả được + chặn completeness ở 78%. Bỏ khỏi checklist. RecommendationService vẫn dùng
            // 2 field này nếu có giá trị (nạp từ nguồn khác), chỉ là không nhắc user điền nữa.
            (hasProfileInput, "Mô tả thêm màu sắc/vật liệu không gian để engine tính ngũ hành sát hơn"),
        };

        var filled = checks.Count(c => c.HasValue);
        var percent = (int)Math.Round(filled * 100.0 / checks.Length);
        var hints = checks.Where(c => !c.HasValue).Select(c => c.Hint).ToList();
        return (percent, hints);
    }
}
