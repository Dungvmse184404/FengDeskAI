using System.Text.Json;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Contracts.Recommendation;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Entities.Workspace;
using Microsoft.Extensions.Logging;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Application.Features.CustomerCare.DTOs;

namespace FengDeskAI.Application.Features.CustomerCare.Services;

public sealed class RecommendationService : IRecommendationService
{
    private const int DefaultTopN = 8;
    private const int MaxTopN = 20;
   
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
            ? FengShuiCalculator.BuildPersonalVector(dob.Year, p.SelfShare, p.SupportShare, p.ChildShare)
            : null;

        // ── Vector phòng: ideal → intent → hiện trạng (chung với GetProductFitAsync) ──
        var wctx = await BuildWorkspaceContextAsync(profile, ct);
        var wsType = wctx.WsType;
        var scope = wctx.Scope;
        var resolver = wctx.Resolver; // còn tái dùng cho vector sản phẩm bên dưới
        var ideal = wctx.Analysis.Ideal;
        var adjustedIdeal = wctx.Analysis.AdjustedIdeal;
        var currentVector = wctx.Analysis.Current;

        // ── Ứng viên + vector sản phẩm ──
        var products = await _uow.Products.GetScorableCandidatesAsync(ct);
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

        var context = new ScoringContext
        {
            PersonalVector = personalVector,
            AdjustedIdeal = adjustedIdeal,
            CurrentVector = currentVector,
            Scope = scope,
            Purpose = profile.WorkPurpose,
            ViolatedDirections = violated,
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
                    BuildResponse(rec, top, productById, BuildGap(adjustedIdeal, currentVector)),
                    "Đã chấm điểm nhưng AI diễn giải gặp lỗi.", ApiStatusCodes.Ok);
            }

            ApplyAiResponse(rec, aiResponse, candidates);
            rec.Status = RecommendationStatus.Completed;
            rec.Logs.Add(new RecommendationLog { Stage = "AiResponded", Detail = JsonSerializer.Serialize(aiResponse) });

            return ServiceResult<RecommendationResponse>.Success(
                BuildResponse(rec, top, productById, BuildGap(adjustedIdeal, currentVector)),
                "Tạo gợi ý thành công.", ApiStatusCodes.Created);
        }, ct);
    }

    public async Task<IServiceResult<RecommendationResponse>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var rec = await _uow.Recommendations.GetDetailForUserAsync(id, userId, ct);
        if (rec is null)
            return ServiceResult<RecommendationResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy phiên gợi ý.");

        var productIds = rec.Items.Select(i => i.ProductId).ToList();
        var products = await _uow.Products.GetScorableCandidatesAsync(ct);
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
            ? FengShuiCalculator.BuildPersonalVector(dob.Year, p.SelfShare, p.SupportShare, p.ChildShare)
            : null;

        var wctx = await BuildWorkspaceContextAsync(profile, ct);

        var productInputs = (await _uow.ScoringConfig.GetProductElementInputsAsync(new[] { productId }, ct))
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<ProductElementInput>)g.ToList());
        var facts = ToFacts(product, productInputs, wctx.Resolver, p);

        var violated = new HashSet<CompassDirection>();
        if (profile.EntranceDirection is { } ed) violated.Add(ed);
        if (profile.ToiletDirection is { } td) violated.Add(td);
        foreach (var d in profile.DarkDirections) violated.Add(d);

        var context = new ScoringContext
        {
            PersonalVector = personalVector,
            AdjustedIdeal = wctx.Analysis.AdjustedIdeal,
            CurrentVector = wctx.Analysis.Current,
            Scope = wctx.Scope,
            Purpose = profile.WorkPurpose,
            ViolatedDirections = violated,
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

        var previewCurrent = WorkspaceVectorBuilder.BuildCurrentWithProducts(
            wctx.ProfileInputs, wctx.Resolver, wctx.TypeElements,
            new[] { (facts.Vector, voteWeight) });
        var previewGapVec = wctx.Analysis.AdjustedIdeal.Subtract(previewCurrent);

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
        };

        return ServiceResult<ProductFitResponse>.Success(response);
    }

    // ─────────────────────────── helpers ───────────────────────────

    private sealed record WorkspaceScoringContext(
        WorkspaceType? WsType, WorkspaceScope Scope, ElementInputResolver Resolver, WorkspaceElementAnalysis Analysis,
        IReadOnlyList<WorkspaceProfileInput> ProfileInputs, IReadOnlyList<WorkspaceTypeElement> TypeElements);

    /// <summary>Nạp data phòng + dựng 4 vector ngũ hành — dùng chung bởi GenerateAsync và GetProductFitAsync.</summary>
    private async Task<WorkspaceScoringContext> BuildWorkspaceContextAsync(WorkspaceProfile profile, CancellationToken ct)
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
        var analysis = WorkspaceElementAnalyzer.Analyze(typeElements, modifiers, profileInputs, resolver);

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

        return new ProductFacts(p.Id, vector, p.Vibes.Select(v => v.VibeCode).ToHashSet());
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
            item.FinalRank = explained.FinalRank;
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
        GapBreakdownResponse? gap)
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

        return Compose(rec, items, gap);
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
        Recommendation rec, List<RecommendationItemResponse> items, GapBreakdownResponse? gap) => new()
    {
        Id = rec.Id,
        CustomerElement = rec.CustomerElement?.ToString(),
        KuaNumber = rec.KuaNumber,
        KuaGroup = rec.KuaGroup?.ToString(),
        PersonalWeight = rec.PersonalWeight,
        Status = rec.Status.ToString(),
        Summary = rec.Summary,
        Gap = gap,
        Items = items,
    };

    private static List<string> Deserialize(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
}
