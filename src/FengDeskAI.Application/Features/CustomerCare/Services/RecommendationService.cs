using System.Text.Json;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Contracts.Recommendation;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Entities.Workspace;
using Microsoft.Extensions.Logging;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Enums;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Application.Features.CustomerCare.DTOs;

namespace FengDeskAI.Application.Features.CustomerCare.Services;

public sealed class RecommendationService : IRecommendationService
{
    private const int DefaultTopN = 8;
    private const int MaxTopN = 20;

    /// <summary>Placement được chấm theo gap của PHÒNG (xem ADR product-placement §3).</summary>
    private static readonly ProductPlacement[] WorkspacePlacements =
        { ProductPlacement.Desk, ProductPlacement.Living };

    /// <summary>Placement chấm theo bản mệnh NGƯỜI — không gắn workspace.</summary>
    private static readonly ProductPlacement[] CarryPlacements = { ProductPlacement.Carry };


    private readonly IUnitOfWork _uow;
    private readonly IRecommendationScorer _scorer;
    private readonly IAiRecommendationClient _ai;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        IUnitOfWork uow,
        IRecommendationScorer scorer,
        IAiRecommendationClient ai,
        ILogger<RecommendationService> logger)
    {
        _uow = uow;
        _scorer = scorer;
        _ai = ai;
        _logger = logger;
    }

    public async Task<IServiceResult<RecommendationResponse>> GenerateAsync(
        Guid userId, GenerateRecommendationRequest request, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetByIdForUserAsync(request.WorkspaceProfileId, userId, ct);
        if (profile is null)
            return ServiceResult<RecommendationResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy hồ sơ không gian.");

        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null)
            return ServiceResult<RecommendationResponse>.Failure(ApiStatusCodes.Unauthorized, "Người dùng không hợp lệ.");

        // Hồ sơ cá nhân (mệnh Nạp Âm + Kua) — cho AI diễn giải + lưu rec (null nếu thiếu ngày sinh).
        var personal = FengShuiCalculator.BuildPersonalProfile(user.DateOfBirth, user.Gender);

        // ── Tham số engine (thiếu row → default trong code) ──
        var p = ScoringParameters.FromRows(await _uow.ScoringConfig.GetScoringParamsAsync(ct));

        // ── Vector mệnh (null → bỏ bộ lọc mệnh) ──
        ElementVector? personalVector = user.DateOfBirth is { } dob
            ? FengShuiCalculator.BuildPersonalVector(dob, p.SelfShare, p.SupportShare, p.ChildShare)
            : null;

        // ── Vector phòng: ideal → intent → hiện trạng (chung với GetProductFitAsync) ──
        var wctx = await BuildWorkspaceContextAsync(profile, ct, user.DateOfBirth, p);
        var wsType = wctx.WsType;
        var scope = wctx.Scope;
        var resolver = wctx.Resolver; // còn tái dùng cho vector sản phẩm bên dưới
        var ideal = wctx.Analysis.Ideal;
        var adjustedIdeal = wctx.Analysis.AdjustedIdeal;
        var currentVector = wctx.Analysis.Current;

        // ── Trục cá nhân v3.1: trọng số theo scope + bảng luật ngũ hành + hướng theo mục tiêu ──
        decimal personalWeight = ResolvePersonalWeight(scope, user.DateOfBirth, p);
        var ruleScores = await LoadRuleScoresAsync(ct);
        var aspirationDirs = BuildAspirationDirections(request.Aspiration, user.DateOfBirth, user.Gender);

        // ── Ứng viên + vector sản phẩm (chỉ đồ đặt trong không gian — vật đeo/hàng tiêu hao loại từ query) ──
        var (products, aspirationNote) = await LoadCandidatesAsync(WorkspacePlacements, request.Aspiration, ct);
        if (products.Count == 0)
            return ServiceResult<RecommendationResponse>.Failure(
                ApiStatusCodes.UnprocessableEntity,
                "Chưa có sản phẩm nào được gắn thuộc tính phong thủy để gợi ý.");

        var productInputs = (await _uow.ScoringConfig.GetProductElementInputsAsync(products.Select(x => x.Id).ToList(), ct))
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<ProductElementInput>)g.ToList());

        var candidates = products.Select(prod => ToFacts(prod, productInputs, resolver, p)).ToList();

        // ── Hướng bị chắn (cửa vào ∪ WC ∪ góc tối) ──
        var violated = new HashSet<CompassDirection>();
        if (profile.EntranceDirection is { } ed) violated.Add(ed);
        if (profile.ToiletDirection is { } td) violated.Add(td);
        foreach (var d in profile.DarkDirections) violated.Add(d);

        var occupation = await LoadOccupationAsync(user, p, ct);

        var context = new ScoringContext
        {
            PersonalVector = personalVector,
            AdjustedIdeal = adjustedIdeal,
            CurrentVector = currentVector,
            Scope = scope,
            Purpose = profile.WorkPurpose,
            ViolatedDirections = violated,
            PersonalWeight = personalWeight,
            RuleScores = ruleScores,
            Aspiration = request.Aspiration,
            AspirationDirections = aspirationDirs,
            OccupationDelta = occupation.Delta,
            OccupationCode = occupation.Code,
            OccupationNameVi = occupation.NameVi,
            Params = p,
        };

        int topN = Math.Clamp(request.TopN ?? DefaultTopN, 1, MaxTopN);
        var top = _scorer.Score(context, candidates).Take(topN).ToList();
        var productById = products.ToDictionary(x => x.Id);

        if (top.Count == 0)
            return ServiceResult<RecommendationResponse>.Failure(
                ApiStatusCodes.UnprocessableEntity,
                "Không có sản phẩm nào phù hợp với mục đích/bản mệnh của không gian này.");

        decimal legacyWeight = wsType?.PersonalWeight ?? 1.0m;

        return await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            var rec = new Recommendation
            {
                UserId = userId,
                WorkspaceProfileId = profile.Id,
                WorkspaceTypeId = profile.WorkspaceTypeId,
                CustomerElement = personal?.Element,
                KuaNumber = personal?.KuaNumber,
                KuaGroup = personal?.Group,
                PersonalWeight = legacyWeight,
                Kind = RecommendationKind.Workspace,
                Status = RecommendationStatus.Scored,
            };

            int rank = 1;
            foreach (var s in top)
            {
                rec.Items.Add(new RecommendationItem
                {
                    ProductId = s.ProductId,
                    BaseScore = s.Score,
                    BaseRank = rank,
                    FinalRank = rank,
                    MatchFacts = JsonSerializer.Serialize(MatchFactsWithHint(s)),
                    CautionFacts = s.CautionFacts.Count > 0 ? JsonSerializer.Serialize(s.CautionFacts) : null,
                });
                rank++;
            }

            await _uow.Recommendations.AddAsync(rec, innerCt);
            rec.Logs.Add(new RecommendationLog
            {
                Stage = "EngineScored",
                Detail = JsonSerializer.Serialize(new
                {
                    topN,
                    candidates = candidates.Count,
                    ideal,
                    adjustedIdeal,
                    current = currentVector,
                    gap = adjustedIdeal.Subtract(currentVector),
                    scope = scope.ToString(),
                    hasPersonalVector = personalVector is not null,
                    personalWeight,
                    aspiration = request.Aspiration?.ToString(),
                    aspirationRelaxed = aspirationNote is not null,
                }),
            });

            var aiRequest = BuildAiRequest(profile, wsType, personal, legacyWeight, top, productById);
            rec.Logs.Add(new RecommendationLog { Stage = "AiRequested", Detail = JsonSerializer.Serialize(aiRequest) });

            AiRecommendationResponse aiResponse;
            try
            {
                aiResponse = await _ai.ExplainAsync(aiRequest, innerCt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI explain thất bại cho recommendation {RecId}.", rec.Id);
                rec.Status = RecommendationStatus.Failed;
                // Detail là cột jsonb → PHẢI serialize JSON; ex.Message trần sẽ gây 22P02 invalid input syntax for type json.
                rec.Logs.Add(new RecommendationLog { Stage = "Error", Detail = JsonSerializer.Serialize(new { error = ex.Message }) });
                return ServiceResult<RecommendationResponse>.Success(
                    BuildResponse(rec, top, productById, BuildGap(adjustedIdeal, currentVector), note: aspirationNote),
                    "Đã chấm điểm nhưng AI diễn giải gặp lỗi.", ApiStatusCodes.Ok);
            }

            ApplyAiResponse(rec, aiResponse, candidates);
            rec.Status = RecommendationStatus.Completed;
            rec.Logs.Add(new RecommendationLog { Stage = "AiResponded", Detail = JsonSerializer.Serialize(aiResponse) });

            return ServiceResult<RecommendationResponse>.Success(
                BuildResponse(rec, top, productById, BuildGap(adjustedIdeal, currentVector), note: aspirationNote),
                "Tạo gợi ý thành công.", ApiStatusCodes.Created);
        }, ct);
    }

    public async Task<IServiceResult<RecommendationResponse>> GeneratePersonalAsync(
        Guid userId, GeneratePersonalRecommendationRequest request, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null)
            return ServiceResult<RecommendationResponse>.Failure(ApiStatusCodes.Unauthorized, "Người dùng không hợp lệ.");

        var p = ScoringParameters.FromRows(await _uow.ScoringConfig.GetScoringParamsAsync(ct));

        // ── Vector mục tiêu = NGƯỜI (dụng thần Tứ Trụ, fallback Nạp Âm). Thiếu ngày sinh → từ chối chấm:
        //    không có căn cứ cá nhân thì gợi ý vật đeo mất hết ý nghĩa (xem ADR §4).
        var target = PersonalTargetBuilder.Build(user.DateOfBirth, user.BirthTime, p);
        if (target is null)
            return ServiceResult<RecommendationResponse>.Failure(
                ApiStatusCodes.UnprocessableEntity,
                "Cần ngày sinh của bạn để gợi ý vật phẩm mang theo người. Bổ sung ngày sinh (và giờ sinh nếu có) trong hồ sơ rồi thử lại.");

        var personal = FengShuiCalculator.BuildPersonalProfile(user.DateOfBirth, user.Gender);

        // Vector mệnh Nạp Âm vẫn cần riêng cho BỘ LỌC khắc mệnh (khác vai trò với vector mục tiêu ở trên).
        ElementVector? personalVector = user.DateOfBirth is { } dob
            ? FengShuiCalculator.BuildPersonalVector(dob, p.SelfShare, p.SupportShare, p.ChildShare)
            : null;

        var (products, aspirationNote) = await LoadCandidatesAsync(CarryPlacements, request.Aspiration, ct);
        if (products.Count == 0)
            return ServiceResult<RecommendationResponse>.Failure(
                ApiStatusCodes.UnprocessableEntity,
                "Cửa hàng chưa có vật phẩm mang theo người nào được gắn thuộc tính phong thủy.");

        var resolver = new ElementInputResolver(await _uow.ScoringConfig.GetElementInputMapAsync(ct));
        var productInputs = (await _uow.ScoringConfig.GetProductElementInputsAsync(products.Select(x => x.Id).ToList(), ct))
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<ProductElementInput>)g.ToList());

        var candidates = products.Select(prod => ToFacts(prod, productInputs, resolver, p)).ToList();

        var context = new ScoringContext
        {
            PersonalVector = personalVector,
            PersonalNeedVector = target.Vector,
            // Nghề nghiệp KHÔNG áp cho luồng Carry: phương án N1 bẻ vector `r`, mà nhánh dụng thần
            // không dựng `r` (mục tiêu vốn đã 100% cá nhân). Muốn nghề nghiệp tác động ở đây thì phải
            // bẻ chính vector dụng thần — một quyết định nghiệp vụ khác, chưa chốt. Xem ADR v3.2 §11.2.
            // Không có phòng: gap = Zero − Zero. Policy của Carry không đọc hai vector này.
            AdjustedIdeal = ElementVector.Zero,
            CurrentVector = ElementVector.Zero,
            Scope = WorkspaceScope.Private,
            Purpose = WorkPurpose.Other, // Other → TargetVibe null → không lọc theo vibe không gian
            Params = p,
        };

        int topN = Math.Clamp(request.TopN ?? DefaultTopN, 1, MaxTopN);
        var top = _scorer.Score(context, candidates).Take(topN).ToList();
        if (top.Count == 0)
            return ServiceResult<RecommendationResponse>.Failure(
                ApiStatusCodes.UnprocessableEntity,
                "Không có vật phẩm mang theo người nào hợp bản mệnh của bạn.");

        var productById = products.ToDictionary(x => x.Id);

        return await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            var rec = new Recommendation
            {
                UserId = userId,
                WorkspaceProfileId = null,
                Kind = RecommendationKind.PersonalCarry,
                CustomerElement = personal?.Element,
                KuaNumber = personal?.KuaNumber,
                KuaGroup = personal?.Group,
                PersonalWeight = 1.0m, // cột legacy NOT NULL — phiên cá nhân luôn 1.0
                Status = RecommendationStatus.Scored,
            };

            int rank = 1;
            foreach (var s in top)
            {
                rec.Items.Add(new RecommendationItem
                {
                    ProductId = s.ProductId,
                    BaseScore = s.Score,
                    BaseRank = rank,
                    FinalRank = rank,
                    MatchFacts = JsonSerializer.Serialize(MatchFactsWithHint(s)),
                    CautionFacts = s.CautionFacts.Count > 0 ? JsonSerializer.Serialize(s.CautionFacts) : null,
                });
                rank++;
            }

            await _uow.Recommendations.AddAsync(rec, innerCt);
            rec.Logs.Add(new RecommendationLog
            {
                Stage = "EngineScored",
                Detail = JsonSerializer.Serialize(new
                {
                    kind = nameof(RecommendationKind.PersonalCarry),
                    topN,
                    candidates = candidates.Count,
                    targetSource = target.Source.ToString(),
                    targetElements = target.Elements,
                    target = target.Vector,
                    hasPersonalVector = personalVector is not null,
                    aspiration = request.Aspiration?.ToString(),
                    aspirationRelaxed = aspirationNote is not null,
                }),
            });

            // KHÔNG gọi AI microservice: Contracts/Recommendation bắt buộc có AiWorkspaceInfo — gửi workspace
            // giả là dữ liệu sai cho AI. LLM chat tự diễn giải từ matchFacts/cautionFacts (ADR §7).
            return ServiceResult<RecommendationResponse>.Success(
                BuildResponse(rec, top, productById, null, target, aspirationNote),
                "Tạo gợi ý vật phẩm mang theo người thành công.", ApiStatusCodes.Created);
        }, ct);
    }

    public async Task<IServiceResult<RecommendationResponse>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var rec = await _uow.Recommendations.GetDetailForUserAsync(id, userId, ct);
        if (rec is null)
            return ServiceResult<RecommendationResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy phiên gợi ý.");

        var productIds = rec.Items.Select(i => i.ProductId).ToList();
        // Không lọc placement: phiên PersonalCarry cũng phải đọc lại được đủ tên/giá sản phẩm.
        var products = await _uow.Products.GetScorableCandidatesAsync(null, null, ct);
        var productById = products.Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id);

        return ServiceResult<RecommendationResponse>.Success(BuildResponseFromEntity(rec, productById));
    }

    public async Task<IServiceResult<ProductFitResponse>> GetProductFitAsync(
        Guid productId, Guid workspaceProfileId, Guid userId, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetByIdForUserAsync(workspaceProfileId, userId, ct);
        if (profile is null)
            return ServiceResult<ProductFitResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy hồ sơ không gian.");

        var product = await _uow.Products.GetDetailAsync(productId, ct);
        if (product is null || !product.IsActive || product.Elements.Count == 0)
            return ServiceResult<ProductFitResponse>.Failure(
                ApiStatusCodes.NotFound, "Không tìm thấy sản phẩm hoặc sản phẩm chưa gắn thuộc tính phong thủy.");

        var user = await _uow.Users.GetByIdAsync(userId, ct);
        var p = ScoringParameters.FromRows(await _uow.ScoringConfig.GetScoringParamsAsync(ct));

        // Vector mệnh (null → bỏ bộ lọc mệnh) — không hard-fail nếu thiếu profile cá nhân, fit vẫn trả điểm.
        ElementVector? personalVector = user?.DateOfBirth is { } dob
            ? FengShuiCalculator.BuildPersonalVector(dob, p.SelfShare, p.SupportShare, p.ChildShare)
            : null;

        var wctx = await BuildWorkspaceContextAsync(profile, ct, user?.DateOfBirth, p);

        var productInputs = (await _uow.ScoringConfig.GetProductElementInputsAsync(new[] { productId }, ct))
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<ProductElementInput>)g.ToList());
        var facts = ToFacts(product, productInputs, wctx.Resolver, p);

        var violated = new HashSet<CompassDirection>();
        if (profile.EntranceDirection is { } ed) violated.Add(ed);
        if (profile.ToiletDirection is { } td) violated.Add(td);
        foreach (var d in profile.DarkDirections) violated.Add(d);

        // Trang Fit phải dùng CÙNG công thức với danh sách gợi ý, nếu không user thấy hai điểm khác nhau
        // cho cùng một sản phẩm × cùng một phòng.
        var fitOccupation = await LoadOccupationAsync(user, p, ct);

        var context = new ScoringContext
        {
            PersonalVector = personalVector,
            AdjustedIdeal = wctx.Analysis.AdjustedIdeal,
            CurrentVector = wctx.Analysis.Current,
            Scope = wctx.Scope,
            Purpose = profile.WorkPurpose,
            ViolatedDirections = violated,
            PersonalWeight = ResolvePersonalWeight(wctx.Scope, user?.DateOfBirth, p),
            RuleScores = await LoadRuleScoresAsync(ct),
            OccupationDelta = fitOccupation.Delta,
            OccupationCode = fitOccupation.Code,
            OccupationNameVi = fitOccupation.NameVi,
            Params = p,
        };

        var scored = _scorer.ScoreSingle(context, facts);

        // Preview ĐÚNG ENGINE: dựng lại Current như khi THÊM đúng sản phẩm này vào phòng — cùng cơ chế
        // "phiếu" (voteWeight) mà workspace dùng cho previewCurrent, nên radar khớp thang với card workspace
        // (không phóng đại kiểu cộng thẳng 2 vector đã normalize). voteWeight = Σ weight DecorItem code, mặc định 1.
        var productElementInputs = productInputs.TryGetValue(productId, out var pin)
            ? pin
            : (IReadOnlyCollection<ProductElementInput>)Array.Empty<ProductElementInput>();
        var decorCodes = productElementInputs.Where(i => i.InputKind == ElementInputKind.DecorItem).ToList();
        var voteWeight = decorCodes.Count > 0
            ? decorCodes.Sum(c => wctx.Resolver.Resolve(c.InputKind, c.InputCode).Sum(kv => kv.Value))
            : 1.0m;
        if (voteWeight < 0m) voteWeight = 0m;

        // Chủ nhân phòng cũng là một nguồn ngũ hành — phải có mặt ở CẢ current lẫn preview, nếu không
        // hai lớp radar lệch thang và "xem trước" trông như đã gỡ chủ nhân ra khỏi phòng.
        var person = PersonPresenceBuilder.Build(user?.DateOfBirth, wctx.Scope, p);

        var previewCurrent = WorkspaceVectorBuilder.BuildCurrentBreakdown(
                wctx.ProfileInputs, wctx.Resolver, wctx.TypeElements,
                new[] { new ProductContribution(Guid.Empty, string.Empty, facts.Vector, voteWeight) },
                person, p.InteriorPriorVotes, p.EvidenceSaturationAlpha)
            .Current;
        var previewGapVec = wctx.Analysis.AdjustedIdeal.Subtract(previewCurrent);

        // Hiện trạng phòng có kèm "nguồn nào đóng góp bao nhiêu" — cùng phép tính với element-analysis,
        // chỉ khác là ở đây không tính sản phẩm đang xem vào (nó đã có mặt trong previewCurrent).
        var currentBreakdown = WorkspaceVectorBuilder.BuildCurrentBreakdown(
            wctx.ProfileInputs, wctx.Resolver, wctx.TypeElements, Array.Empty<ProductContribution>(),
            person, p.InteriorPriorVotes, p.EvidenceSaturationAlpha);

        var response = new ProductFitResponse
        {
            ProductId = productId,
            WorkspaceProfileId = profile.Id,
            Score = scored.Score,
            MatchFacts = scored.MatchFacts.ToList(),
            CautionFacts = scored.CautionFacts.ToList(),
            PlacementHint = scored.PlacementHint,
            Gap = wctx.Analysis.Ideal.Enumerate().Select(x => new ElementAnalysisRow
            {
                Element = x.Element.ToString(),
                Ideal = Math.Round(x.Value, 3),
                AdjustedIdeal = Math.Round(wctx.Analysis.AdjustedIdeal[x.Element], 3),
                Current = Math.Round(wctx.Analysis.Current[x.Element], 3),
                Gap = Math.Round(wctx.Analysis.Gap[x.Element], 3),
                PreviewCurrent = Math.Round(previewCurrent[x.Element], 3),
                PreviewGap = Math.Round(previewGapVec[x.Element], 3),
            }).ToList(),
            ProductVector = facts.Vector.Enumerate().Select(x => new ProductElementRow
            {
                Element = x.Element.ToString(),
                Value = Math.Round(x.Value, 3),
            }).ToList(),

            // v3.2 §9 — mọi số hạng đã tạo ra Score, kèm lý do tiếng Việt cho từng số.
            Breakdown = scored.Breakdown is { } bd
                ? ScoreBreakdownMapping.ToResponse(bd, scored.Score, user?.DateOfBirth)
                : null,

            // Cùng dữ liệu tooltip của element-analysis: trả lời "vì sao phòng được cho là thiếu hành đó",
            // không chỉ "phòng thiếu hành đó". Dựng lại breakdown KHÔNG kèm sản phẩm đang xem — đây là
            // hiện trạng phòng, còn ảnh hưởng của sản phẩm đã nằm ở previewCurrent.
            Contributions = CurrentBreakdownMapping.ToContributionRows(currentBreakdown),
            EvidenceCount = currentBreakdown.EvidenceCount,
            Confidence = CurrentBreakdownMapping.ConfidenceOf(currentBreakdown),
        };

        return ServiceResult<ProductFitResponse>.Success(response);
    }

    public async Task<IServiceResult<PersonalFitResponse>> GetPersonalFitAsync(
        Guid productId, Guid userId, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null)
            return ServiceResult<PersonalFitResponse>.Failure(ApiStatusCodes.Unauthorized, "Người dùng không hợp lệ.");

        var product = await _uow.Products.GetDetailAsync(productId, ct);
        if (product is null || !product.IsActive || product.Elements.Count == 0)
            return ServiceResult<PersonalFitResponse>.Failure(
                ApiStatusCodes.NotFound, "Không tìm thấy sản phẩm hoặc sản phẩm chưa gắn thuộc tính phong thủy.");

        var p = ScoringParameters.FromRows(await _uow.ScoringConfig.GetScoringParamsAsync(ct));

        // Không có ngày sinh thì KHÔNG chấm bừa: khác luồng phòng (ở đó còn gap để dựa vào), ở đây mục
        // tiêu 100% là con người — thiếu căn cứ cá nhân là điểm mất hết ý nghĩa. Cùng lý lẽ với
        // GeneratePersonalAsync, và trả 422 kèm hướng dẫn thay vì một con số vô nghĩa.
        var target = PersonalTargetBuilder.Build(user.DateOfBirth, user.BirthTime, p);
        if (target is null || user.DateOfBirth is not { } dob)
            return ServiceResult<PersonalFitResponse>.Failure(
                ApiStatusCodes.UnprocessableEntity,
                "Cần ngày sinh của bạn để chấm vật phẩm mang theo người. "
                + "Bổ sung ngày sinh (và giờ sinh nếu có) trong hồ sơ rồi thử lại.");

        var resolver = new ElementInputResolver(await _uow.ScoringConfig.GetElementInputMapAsync(ct));
        var productInputs = (await _uow.ScoringConfig.GetProductElementInputsAsync(new[] { productId }, ct))
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<ProductElementInput>)g.ToList());
        var facts = ToFacts(product, productInputs, resolver, p);

        var context = new ScoringContext
        {
            PersonalVector = FengShuiCalculator.BuildPersonalVector(dob, p.SelfShare, p.SupportShare, p.ChildShare),
            PersonalNeedVector = target.Vector,
            // Không có phòng — policy của Carry không đọc hai vector này.
            AdjustedIdeal = ElementVector.Zero,
            CurrentVector = ElementVector.Zero,
            Scope = WorkspaceScope.Private,
            Purpose = WorkPurpose.Other, // Other → TargetVibe null → không lọc theo vibe không gian
            RuleScores = await LoadRuleScoresAsync(ct),
            Params = p,
        };

        var scored = _scorer.ScoreSinglePersonal(context, facts);

        var cautions = scored.CautionFacts.ToList();
        if (product.Placement != ProductPlacement.Carry)
            cautions.Insert(0, "Sản phẩm này không phải vật mang theo người - điểm dưới đây chấm theo bản mệnh "
                + "của bạn, để chọn đúng nên xem độ hợp với phòng.");

        var response = new PersonalFitResponse
        {
            ProductId = productId,
            Score = scored.Score,
            MatchFacts = scored.MatchFacts.ToList(),
            CautionFacts = cautions,
            PlacementHint = scored.PlacementHint,
            Breakdown = scored.Breakdown is { } bd
                ? ScoreBreakdownMapping.ToResponse(bd, scored.Score, user.DateOfBirth)
                : null,
            PersonalNeedVector = target.Vector.Enumerate().Select(x => new ProductElementRow
            {
                Element = x.Element.ToString(),
                Value = Math.Round(x.Value, 3),
            }).ToList(),
            ProductVector = facts.Vector.Enumerate().Select(x => new ProductElementRow
            {
                Element = x.Element.ToString(),
                Value = Math.Round(x.Value, 3),
            }).ToList(),
            DestinyElement = FengShuiCalculator.GetNapAmElement(FengShuiCalculator.GetLunarYear(dob)).ToString(),
            DestinyLabelVi = $"{FengShuiCalculator.GetNapAmElement(FengShuiCalculator.GetLunarYear(dob))} - "
                + $"{FengShuiCalculator.GetNapAmName(FengShuiCalculator.GetLunarYear(dob))} "
                + $"({FengShuiCalculator.GetLunarYear(dob)})",
        };

        return ServiceResult<PersonalFitResponse>.Success(response);
    }

    // ─────────────────────────── helpers ───────────────────────────

    /// <summary>Nghề nghiệp đã quy về đại lượng engine hiểu. Ba trường luôn cùng có hoặc cùng vắng.</summary>
    private sealed record OccupationInfluence(ElementVector? Delta, string? Code, string? NameVi)
    {
        public static readonly OccupationInfluence None = new(null, null, null);
    }

    /// <summary>
    /// Nạp delta ngũ hành theo nghề của user (P5/N1). Trả <see cref="OccupationInfluence.None"/> khi
    /// kill-switch tắt, user chưa khai nghề, nghề bị tắt, hoặc nghề đó chưa được nhập delta nào —
    /// <b>thiếu dữ liệu thì đừng đoán</b>, và cũng đừng trả mã nghề ra breakdown khi nó không đổi được điểm.
    /// </summary>
    private async Task<OccupationInfluence> LoadOccupationAsync(User? user, ScoringParameters p, CancellationToken ct)
    {
        if (p.OccupationShare <= 0m || user?.OccupationId is not { } occupationId)
            return OccupationInfluence.None;

        var occupation = (await _uow.ScoringConfig.GetOccupationsAsync(includeInactive: true, ct))
            .FirstOrDefault(o => o.Id == occupationId);
        if (occupation is null || !occupation.IsActive || occupation.Modifiers.Count == 0)
            return OccupationInfluence.None;

        // KHÔNG dùng ElementVector.FromContributions: nó normalize về Σ=1, mà delta là đại lượng CÓ DẤU
        // và có độ lớn riêng — chuẩn hoá là bóp méo đúng thứ tham số OCCUPATION_SHARE dùng để hiệu chỉnh.
        var delta = ElementVector.Zero;
        foreach (var m in occupation.Modifiers)
            delta = delta.Add(ElementVector.Single(m.Element).Scale(m.Delta));

        return new OccupationInfluence(delta, occupation.Code, occupation.NameVi);
    }

    private sealed record WorkspaceScoringContext(
        WorkspaceType? WsType, WorkspaceScope Scope, ElementInputResolver Resolver, WorkspaceElementAnalysis Analysis,
        IReadOnlyList<WorkspaceProfileInput> ProfileInputs, IReadOnlyList<WorkspaceTypeElement> TypeElements);

    /// <summary>Nạp data phòng + dựng 4 vector ngũ hành — dùng chung bởi GenerateAsync và GetProductFitAsync.</summary>
    // ─────────────────────────── v3.1 — trục cá nhân & mục tiêu ───────────────────────────

    /// <summary>
    /// Tỉ trọng <c>personalScore</c> cho phiên này. Trả 0 (tắt trục cá nhân, giữ nguyên luật cũ) khi user
    /// chưa có ngày sinh — không có mệnh thì không có gì để trộn. Xem ADR v3.1 §3.2.
    /// </summary>
    private static decimal ResolvePersonalWeight(WorkspaceScope scope, DateTime? dateOfBirth, ScoringParameters p)
        => p.PersonalWeightFor(scope, dateOfBirth);

    /// <summary>Bảng điểm quan hệ ngũ hành từ <c>feng_shui_rules</c> (admin chỉnh được, seed 25 cặp).</summary>
    private async Task<IReadOnlyDictionary<(FengShuiElement Subject, FengShuiElement Object), decimal>>
        LoadRuleScoresAsync(CancellationToken ct)
    {
        var rules = await _uow.Recommendations.GetAllRulesAsync(ct);
        var map = new Dictionary<(FengShuiElement, FengShuiElement), decimal>();
        foreach (var r in rules)
            map[(r.SubjectElement, r.ObjectElement)] = r.Score;
        return map;
    }

    /// <summary>
    /// Hướng Bát Trạch xếp theo mục tiêu user nêu: cung khớp mục tiêu đứng trước, các cung tốt còn lại
    /// giữ nguyên thứ tự. Rỗng khi user không nêu mục tiêu, thiếu ngày sinh, hoặc giới tính không Nam/Nữ
    /// (không tính được cung mệnh) — khi đó <c>placementHint</c> giữ hành vi cũ.
    /// </summary>
    private static IReadOnlyList<AspirationDirection> BuildAspirationDirections(
        Aspiration? aspiration, DateTime? dateOfBirth, Gender gender)
    {
        if (aspiration is not { } asp || dateOfBirth is not { } dob)
            return Array.Empty<AspirationDirection>();

        var destiny = DestinyCalculator.ComputeFromSolar(DateOnly.FromDateTime(dob), gender);
        if (destiny.FavorableDirections is not { } favorable || favorable.Count == 0)
            return Array.Empty<AspirationDirection>();

        string cung = FengShuiCalculator.GetCungForAspiration(asp);

        return favorable
            .OrderByDescending(f => string.Equals(f.CungName, cung, StringComparison.OrdinalIgnoreCase))
            .Select(f => (Direction: FengShuiCalculator.ParseDirectionVi(f.Direction), f.CungName))
            .Where(x => x.Direction is not null)
            .Select(x => new AspirationDirection(x.Direction!.Value, x.CungName))
            .ToList();
    }

    /// <summary>
    /// Nạp ứng viên có lọc theo mục tiêu. <b>Fallback bắt buộc:</b> catalog đầu chưa ai được duyệt thẻ nào,
    /// lọc cứng sẽ trả rỗng cho mọi câu "tôi muốn tiền tài" → bỏ lọc và ghi <c>Note</c> để AI nói lại cho user.
    /// </summary>
    private async Task<(List<Product> Products, string? Note)> LoadCandidatesAsync(
        IReadOnlyCollection<ProductPlacement> placements, Aspiration? aspiration, CancellationToken ct)
    {
        if (aspiration is null)
            return (await _uow.Products.GetScorableCandidatesAsync(placements, null, ct), null);

        var filtered = await _uow.Products.GetScorableCandidatesAsync(placements, aspiration, ct);
        if (filtered.Count > 0)
            return (filtered, null);

        var all = await _uow.Products.GetScorableCandidatesAsync(placements, null, ct);
        return (all, $"Chưa có vật phẩm nào được duyệt thẻ mục tiêu \"{aspiration}\" - danh sách dưới đây "
            + "gợi ý theo không gian và bản mệnh, chưa lọc theo mục tiêu đó.");
    }

    private async Task<WorkspaceScoringContext> BuildWorkspaceContextAsync(
        WorkspaceProfile profile, CancellationToken ct,
        DateTime? ownerDateOfBirth = null, ScoringParameters? prms = null)
    {
        WorkspaceType? wsType = null;
        var typeElements = new List<WorkspaceTypeElement>();
        var scope = WorkspaceScope.Private;
        if (profile.WorkspaceTypeId is { } typeId)
        {
            wsType = await _uow.WorkspaceTypes.GetByIdAsync(typeId, ct);
            if (wsType is not null)
            {
                scope = wsType.Scope;
                typeElements = await _uow.ScoringConfig.GetWorkspaceTypeElementsAsync(typeId, ct);
            }
        }

        var resolver = new ElementInputResolver(await _uow.ScoringConfig.GetElementInputMapAsync(ct));
        var modifiers = await _uow.ScoringConfig.GetWorkPurposeModifiersAsync(profile.WorkPurpose, ct);
        var profileInputs = await _uow.ScoringConfig.GetWorkspaceProfileInputsAsync(profile.Id, ct);
        var person = prms is null ? null : PersonPresenceBuilder.Build(ownerDateOfBirth, scope, prms);
        var analysis = WorkspaceElementAnalyzer.Analyze(
            typeElements, modifiers, profileInputs, resolver, person,
            prms?.InteriorPriorVotes, prms?.EvidenceSaturationAlpha);

        return new WorkspaceScoringContext(wsType, scope, resolver, analysis, profileInputs, typeElements);
    }

    /// <summary>MatchFacts + placementHint (gộp để không đổi schema RecommendationItem).</summary>
    private static List<string> MatchFactsWithHint(ScoredProduct s)
    {
        var list = s.MatchFacts.ToList();
        if (!string.IsNullOrWhiteSpace(s.PlacementHint))
            list.Add(s.PlacementHint!);
        return list;
    }

    private static ProductFacts ToFacts(
        Product p,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<ProductElementInput>> inputsByProduct,
        ElementInputResolver resolver,
        ScoringParameters prms)
    {
        // Vector override chỉ dùng khi đủ 5 cột.
        ElementVector? overridden = p is { ElementTho: { } t, ElementKim: { } k, ElementThuy: { } w, ElementMoc: { } m, ElementHoa: { } h }
            ? new ElementVector(t, k, w, m, h)
            : null;

        var inputs = inputsByProduct.TryGetValue(p.Id, out var list)
            ? list
            : Array.Empty<ProductElementInput>();

        var vector = ProductVectorProvider.Build(
            p.IsVectorOverridden, overridden, inputs, resolver,
            p.Elements.Select(e => (e.Element, e.IsPrimary)), prms);

        return new ProductFacts(p.Id, vector, p.Vibes.Select(v => v.VibeCode).ToHashSet(), p.Placement);
    }

    private static AiRecommendationRequest BuildAiRequest(
        WorkspaceProfile profile, WorkspaceType? wsType, PersonalProfile? personal, decimal legacyWeight,
        List<ScoredProduct> top, IReadOnlyDictionary<Guid, Product> productById)
    {
        return new AiRecommendationRequest
        {
            Customer = new AiCustomerInfo
            {
                Element = personal?.Element.ToString(),
                KuaNumber = personal?.KuaNumber,
                KuaGroup = personal?.Group.ToString(),
                FavorableDirections = personal?.FavorableDirections.Select(d => d.ToString()).ToList()
                    ?? new List<string>(),
            },
            Workspace = new AiWorkspaceInfo
            {
                Type = wsType?.Name ?? "Personal Desk",
                IsPublic = wsType?.IsPublic ?? false,
                Purpose = profile.WorkPurpose.ToString(),
                Style = profile.StyleCode,
                Lighting = profile.Lighting?.ToString(),
                DeskOrientation = profile.DeskOrientation?.ToString(),
                DeskArea = profile.DeskArea,
                PersonalWeight = legacyWeight,
            },
            Candidates = top.Select((s, i) =>
            {
                var p = productById[s.ProductId];
                return new AiCandidate
                {
                    ProductId = s.ProductId,
                    Name = p.Name,
                    Description = p.Description,
                    Score = s.Score,
                    BaseRank = i + 1,
                    MatchFacts = MatchFactsWithHint(s),
                    CautionFacts = s.CautionFacts.ToList(),
                };
            }).ToList(),
        };
    }

    /// <summary>Áp diễn giải + thứ hạng AI lên item, bỏ qua sản phẩm lạ (luật contract).</summary>
    private void ApplyAiResponse(Recommendation rec, AiRecommendationResponse response, List<ProductFacts> candidates)
    {
        var allowed = candidates.Select(c => c.ProductId).ToHashSet();
        var byProduct = rec.Items.ToDictionary(i => i.ProductId);

        var unknown = response.Items.Where(i => !allowed.Contains(i.ProductId)).ToList();
        if (unknown.Count > 0)
        {
            _logger.LogWarning("[Contract] AI trả {Count} sản phẩm ngoài danh sách — bỏ qua.", unknown.Count);
            rec.Logs.Add(new RecommendationLog
            {
                Stage = "ContractViolation",
                Detail = JsonSerializer.Serialize(unknown.Select(u => u.ProductId)),
            });
        }

        foreach (var explained in response.Items)
        {
            if (!byProduct.TryGetValue(explained.ProductId, out var item)) continue;
            item.AiExplanation = explained.Explanation;
        }

        // AI chỉ được HOÁN VỊ thứ hạng trong topN, không được đổi tập sản phẩm. Nếu tập finalRank nó trả về
        // không phải hoán vị hợp lệ của 1..N (trùng / thiếu / ngoài biên) thì bỏ qua toàn bộ và giữ BaseRank —
        // thà giữ thứ tự engine còn hơn hiển thị thứ hạng vô nghĩa. Xem ADR v3.1 §7.2.
        int n = rec.Items.Count;
        var proposed = response.Items
            .Where(i => byProduct.ContainsKey(i.ProductId))
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.First().FinalRank);

        bool validPermutation = proposed.Count == n
            && proposed.Values.All(r => r >= 1 && r <= n)
            && proposed.Values.Distinct().Count() == n;

        if (validPermutation)
        {
            foreach (var (productId, finalRank) in proposed)
                byProduct[productId].FinalRank = finalRank;
        }
        else if (proposed.Count > 0)
        {
            _logger.LogWarning("[Contract] finalRank của AI không phải hoán vị hợp lệ của 1..{N} — giữ BaseRank.", n);
            rec.Logs.Add(new RecommendationLog
            {
                Stage = "ContractViolation",
                Detail = JsonSerializer.Serialize(new
                {
                    reason = "finalRank không phải hoán vị hợp lệ",
                    expectedCount = n,
                    received = proposed.Values.ToList(),
                }),
            });

            foreach (var item in rec.Items)
                item.FinalRank = item.BaseRank;
        }

        rec.Summary = response.Summary;
    }

    private static GapBreakdownResponse BuildGap(ElementVector adjustedIdeal, ElementVector current)
    {
        var gap = adjustedIdeal.Subtract(current);
        return new GapBreakdownResponse
        {
            Elements = adjustedIdeal.Enumerate().Select(x => new GapElementRow
            {
                Element = x.Element.ToString(),
                Ideal = Math.Round(x.Value, 3),
                Current = Math.Round(current[x.Element], 3),
                Gap = Math.Round(gap[x.Element], 3),
            }).ToList(),
        };
    }

    private static RecommendationResponse BuildResponse(
        Recommendation rec, List<ScoredProduct> top, IReadOnlyDictionary<Guid, Product> productById,
        GapBreakdownResponse? gap, PersonalTarget? personalTarget = null, string? note = null)
    {
        var factsByProduct = top.ToDictionary(t => t.ProductId);

        var items = rec.Items.Select(it =>
        {
            productById.TryGetValue(it.ProductId, out var p);
            factsByProduct.TryGetValue(it.ProductId, out var facts);

            return new RecommendationItemResponse
            {
                ProductId = it.ProductId,
                ProductName = p?.Name ?? "(unknown)",
                Price = p is { Items.Count: > 0 } ? p.Items.Min(i => i.Price) : null,
                ImageUrl = p?.Images.OrderBy(im => im.SortOrder).FirstOrDefault()?.Url,
                Score = it.BaseScore,
                Rank = it.FinalRank,
                MatchFacts = facts?.MatchFacts.ToList() ?? new(),
                CautionFacts = facts?.CautionFacts.ToList() ?? new(),
                PlacementHint = facts?.PlacementHint,
                Explanation = it.AiExplanation,
            };
        })
        .OrderBy(i => i.Rank)
        .ToList();

        return Compose(rec, items, gap, personalTarget, note);
    }

    private static RecommendationResponse BuildResponseFromEntity(Recommendation rec, IReadOnlyDictionary<Guid, Product> productById)
    {
        var items = rec.Items.Select(it =>
        {
            productById.TryGetValue(it.ProductId, out var p);
            return new RecommendationItemResponse
            {
                ProductId = it.ProductId,
                ProductName = p?.Name ?? "(unknown)",
                Price = p is { Items.Count: > 0 } ? p.Items.Min(i => i.Price) : null,
                ImageUrl = p?.Images.OrderBy(im => im.SortOrder).FirstOrDefault()?.Url,
                Score = it.BaseScore,
                Rank = it.FinalRank,
                MatchFacts = Deserialize(it.MatchFacts),
                CautionFacts = Deserialize(it.CautionFacts),
                Explanation = it.AiExplanation,
            };
        })
        .OrderBy(i => i.Rank)
        .ToList();

        return Compose(rec, items, null);
    }

    private static RecommendationResponse Compose(
        Recommendation rec, List<RecommendationItemResponse> items, GapBreakdownResponse? gap,
        PersonalTarget? personalTarget = null, string? note = null) => new()
    {
        Id = rec.Id,
        Kind = rec.Kind.ToString(),
        CustomerElement = rec.CustomerElement?.ToString(),
        KuaNumber = rec.KuaNumber,
        KuaGroup = rec.KuaGroup?.ToString(),
        PersonalWeight = rec.PersonalWeight,
        Status = rec.Status.ToString(),
        Summary = rec.Summary,
        Gap = gap,
        PersonalTarget = personalTarget is null ? null : new PersonalTargetResponse
        {
            Source = personalTarget.Source.ToString(),
            Elements = personalTarget.Elements.ToList(),
            Note = personalTarget.Note,
        },
        Note = note,
        Items = items,
    };

    private static List<string> Deserialize(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
}
