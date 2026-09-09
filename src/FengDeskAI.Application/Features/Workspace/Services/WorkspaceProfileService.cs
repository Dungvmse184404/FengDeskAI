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
        var deliveredContribs = new List<ProductContribution>();
        var previewContribs = new List<ProductContribution>();

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
                var contrib = new ProductContribution(pl.ProductId, pl.OrderItem.ProductName, vector, voteWeight);
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
        // Chủ nhân phòng là một nguồn ngũ hành, cùng cơ chế phiếu với nền phòng và tag. Phải truyền vào
        // CẢ current lẫn preview, nếu không hai lớp radar sẽ ở hai thang khác nhau.
        var scoringParams = ScoringParameters.FromRows(await _uow.ScoringConfig.GetScoringParamsAsync(ct));
        var owner = await _uow.Users.GetByIdAsync(userId, ct);
        var person = PersonPresenceBuilder.Build(owner?.DateOfBirth, ctx.Scope, scoringParams);

        var breakdown = WorkspaceVectorBuilder.BuildCurrentBreakdown(
            ctx.ProfileInputs, ctx.Resolver, ctx.TypeElements, deliveredContribs,
            person, scoringParams.InteriorPriorVotes, scoringParams.EvidenceSaturationAlpha);
        var current = breakdown.Current;
        var previewCurrent = WorkspaceVectorBuilder
            .BuildCurrentBreakdown(ctx.ProfileInputs, ctx.Resolver, ctx.TypeElements, previewContribs,
                person, scoringParams.InteriorPriorVotes, scoringParams.EvidenceSaturationAlpha)
            .Current;
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

        var user = owner;
        // Năm ÂM lịch — dùng .Year (dương) sẽ ra bản mệnh khác với hồ sơ mệnh & engine chấm điểm.
        int? lunarBirthYear = user?.DateOfBirth is { } dob ? FengShuiCalculator.GetLunarYear(dob) : null;
        var insights = SpaceInsightBuilder.Build(
            rows, profile.WorkPurpose, ctx.Modifiers, lunarBirthYear, breakdown, ctx.WorkspaceTypeName);

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
            Contributions = CurrentBreakdownMapping.ToContributionRows(breakdown),
            EvidenceCount = breakdown.EvidenceCount,
            TotalVotes = Math.Round(breakdown.TotalVotes, 3),
            SaturationAlpha = scoringParams.EvidenceSaturationAlpha,
            Confidence = CurrentBreakdownMapping.ConfidenceOf(breakdown),
            PersonalDirection = await BuildPersonalDirectionAsync(
                ctx.Scope, user?.DateOfBirth, adjustedIdeal, gap, ct),
        };

        return ServiceResult<WorkspaceElementAnalysisResponse>.Success(response);
    }

    /// <summary>
    /// Lớp "Ưu tiên của bạn" cho radar phòng — v3.2 §10.3.
    ///
    /// <para>
    /// Dùng đúng <see cref="ElementDirection"/> mà <c>RecommendationScorer</c> dùng để chấm sản phẩm,
    /// nên đa giác trên màn hình phòng và đa giác trong <c>breakdown</c> của một sản phẩm không thể
    /// lệch nhau. Trả <c>null</c> khi không có gì để vẽ — <c>Wp = 0</c> thì <c>d ≡ ĝ</c> và lớp vàng
    /// sẽ trùng khít "Mức lý tưởng", vẽ ra chỉ làm rối biểu đồ.
    /// </para>
    /// </summary>
    private async Task<PersonalDirectionResponse?> BuildPersonalDirectionAsync(
        WorkspaceScope scope, DateTime? dateOfBirth, ElementVector adjustedIdeal, ElementVector gap,
        CancellationToken ct)
    {
        if (dateOfBirth is not { } dob) return null;

        var prms = ScoringParameters.FromRows(await _uow.ScoringConfig.GetScoringParamsAsync(ct));
        decimal wp = prms.PersonalWeightFor(scope, dateOfBirth);
        if (wp <= 0m) return null;

        var destiny = FengShuiCalculator.GetNapAmElement(FengShuiCalculator.GetLunarYear(dob));

        // Bảng luật admin chỉnh được — không dùng thẳng hằng số trong code.
        var rules = (await _uow.Recommendations.GetAllRulesAsync(ct))
            .ToDictionary(r => (r.SubjectElement, r.ObjectElement), r => r.Score);
        decimal RuleScoreOf(FengShuiElement subject, FengShuiElement obj)
            => rules.TryGetValue((subject, obj), out var v)
                ? v
                : FengShuiCalculator.DefaultScore(FengShuiCalculator.GetRelation(subject, obj));

        var direction = ElementDirection.ForWorkspaceGap(gap, destiny, wp, RuleScoreOf);
        var personalVector = FengShuiCalculator.BuildPersonalVector(
            dob, prms.SelfShare, prms.SupportShare, prms.ChildShare);

        return new PersonalDirectionResponse
        {
            PersonalWeight = Math.Round(wp, 3),
            PersonalWeightCode = ScoringParamCodes.PersonalWeightFor(scope),
            Scope = scope.ToString(),
            ReasonVi = ScoreBreakdownMapping.PersonalWeightReason(scope, wp, destiny),
            DestinyElement = destiny.ToString(),
            DestinyLabelVi = ScoreBreakdownMapping.DestinyLabel(destiny, dob)!,
            NormalizedGap = ScoreBreakdownMapping.Rows(direction.NormalizedGap),
            RuleScore = ScoreBreakdownMapping.Rows(direction.RuleScoreVector ?? ElementVector.Zero),
            CombinedDirection = ScoreBreakdownMapping.Rows(direction.CombinedDirection),
            PersonalVector = ScoreBreakdownMapping.Rows(personalVector),
            PriorityVector = ScoreBreakdownMapping.Rows(direction.PriorityVector),
            ConflictResolution = direction.ConflictResolution is { } c ? new ConflictResolutionResponse
            {
                RoomNeed = c.RoomNeed.ToString(),
                Destiny = c.Destiny.ToString(),
                Bridge = c.Bridge.ToString(),
                ReasonVi = c.ReasonVi,
            } : null,
        };
    }

    /// <summary>Nạp dữ liệu cấu hình rồi dựng 4 vector ngũ hành cho workspace (dùng chung công thức với engine).</summary>
    /// <summary>Dữ liệu cấu hình cần cho phân tích vector phòng — nạp 1 lần, dùng cho cả current + preview.</summary>
    private sealed record AnalysisContext(
        List<WorkspaceTypeElement> TypeElements,
        ElementInputResolver Resolver,
        List<WorkPurposeElementModifier> Modifiers,
        List<WorkspaceProfileInput> ProfileInputs,
        string? WorkspaceTypeName,
        WorkspaceScope Scope);

    private async Task<AnalysisContext> LoadAnalysisContextAsync(
        Domain.Entities.Workspace.WorkspaceProfile profile, CancellationToken ct)
    {
        var typeElements = new List<WorkspaceTypeElement>();
        string? typeName = null;
        // Chưa chọn loại phòng ⇒ coi như riêng tư: giả định an toàn hơn, vì đoán nhầm thành Public sẽ
        // âm thầm TẮT trục cá nhân của một phòng đáng lẽ có.
        var scope = WorkspaceScope.Private;
        if (profile.WorkspaceTypeId is { } typeId
            && await _uow.WorkspaceTypes.GetByIdAsync(typeId, ct) is { } workspaceType)
        {
            typeName = workspaceType.Name;
            scope = workspaceType.Scope;
            typeElements = await _uow.ScoringConfig.GetWorkspaceTypeElementsAsync(typeId, ct);
        }

        var resolver = new ElementInputResolver(await _uow.ScoringConfig.GetElementInputMapAsync(ct));
        var modifiers = await _uow.ScoringConfig.GetWorkPurposeModifiersAsync(profile.WorkPurpose, ct);
        var profileInputs = await _uow.ScoringConfig.GetWorkspaceProfileInputsAsync(profile.Id, ct);

        return new AnalysisContext(typeElements, resolver, modifiers, profileInputs, typeName, scope);
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
            : "Đã đặt sản phẩm vào không gian (hàng đang giao - radar hiển thị dạng xem trước).");
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

    public async Task<IServiceResult<ElementInputVocabularyResponse>> GetElementInputVocabularyAsync(
        Guid userId, CancellationToken ct = default)
    {
        var map = await _uow.ScoringConfig.GetElementInputMapAsync(ct);

        // CHỈ lọc ở tầng KHÁM PHÁ (picker + prompt AI): tag chưa duyệt của người khác không hiện ra,
        // tránh 1 user gõ sai là cả cộng đồng học theo. Tầng SỬ DỤNG (resolver/chấm điểm/validate khi lưu)
        // KHÔNG lọc — nếu lọc, tag riêng của user sẽ biến mất khỏi radar của chính họ.
        var visible = map.Where(m => m.IsVisibleTo(userId)).ToList();

        // 1 code có thể có nhiều row (mỗi hành 1 row) → gộp về 1 option, lấy nhãn Việt đầu tiên có giá trị.
        var byKind = visible.GroupBy(m => m.InputKind)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(m => m.InputCode)
                    .Select(codeGroup => new ElementInputOptionDto(
                        codeGroup.Key,
                        codeGroup.Select(m => m.LabelVi).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
                            ?? codeGroup.Key))
                    .OrderBy(o => o.LabelVi, StringComparer.CurrentCulture)
                    .ToList());

        var response = new ElementInputVocabularyResponse(
            byKind.GetValueOrDefault(ElementInputKind.Color, new List<ElementInputOptionDto>()),
            byKind.GetValueOrDefault(ElementInputKind.Material, new List<ElementInputOptionDto>()),
            byKind.GetValueOrDefault(ElementInputKind.Shape, new List<ElementInputOptionDto>()),
            byKind.GetValueOrDefault(ElementInputKind.DecorItem, new List<ElementInputOptionDto>()));

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
