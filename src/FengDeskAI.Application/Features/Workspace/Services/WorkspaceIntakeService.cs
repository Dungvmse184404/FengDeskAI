using System.Globalization;
using System.Text;
using System.Text.Json;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Workspace.DTOs;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Application.Features.Workspace.Services;

/// <summary>
/// Workspace AI intake: Ollama trích xuất field từ mô tả tự do → BE NORMALIZE lại từng giá trị bằng
/// code deterministic (whitelist/enum). AI chỉ map text → code, không bao giờ tự quyết định giá trị cuối.
/// </summary>
public sealed class WorkspaceIntakeService : IWorkspaceIntakeService
{
    private const int MinDescriptionLength = 10;
    private const int MaxDescriptionLength = 2000;
    private const int MinDeskAreaCm2 = 400;
    private const int MaxDeskAreaCm2 = 100_000;
    private static readonly TimeSpan VocabularyCacheTtl = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions RawJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IAiChatClient _client;
    private readonly IUnitOfWork _uow;
    private readonly IMemoryCache _cache;
    private readonly WorkspaceIntakeOptions _options;
    private readonly ILogger<WorkspaceIntakeService> _logger;

    public WorkspaceIntakeService(
        IAiChatClient client,
        IUnitOfWork uow,
        IMemoryCache cache,
        IOptions<WorkspaceIntakeOptions> options,
        ILogger<WorkspaceIntakeService> logger)
    {
        _client = client;
        _uow = uow;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IServiceResult<WorkspaceProfileDraftResponse>> ParseAsync(
        Guid userId, ParseWorkspaceDescriptionRequest request, CancellationToken ct = default)
    {
        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length is < MinDescriptionLength or > MaxDescriptionLength)
            return ServiceResult<WorkspaceProfileDraftResponse>.Failure(
                ApiStatusCodes.BadRequest, $"Mô tả phải từ {MinDescriptionLength} đến {MaxDescriptionLength} ký tự.");

        try
        {
            var vocab = await GetVocabularyAsync(userId, ct);

            var messages = new List<AiChatMessage>
            {
                new(AiChatRoles.System, BuildSystemPrompt(vocab)),
                new(AiChatRoles.User, description),
            };

            // Model + temperature riêng cho intake (Ai:Intake) — không dùng chung với chatbox.
            var completion = await _client.CompleteAsync(
                _options.Model, messages, tools: null,
                options: new AiCompletionOptions(_options.Temperature, _options.JsonMode), ct: ct);

            var raw = ParseRaw(completion.Content);
            var draft = Normalize(raw, vocab);
            return ServiceResult<WorkspaceProfileDraftResponse>.Success(draft);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[WorkspaceIntake] Parse thất bại cho user {UserId}.", userId);
            return ServiceResult<WorkspaceProfileDraftResponse>.Failure(
                ApiStatusCodes.ServiceUnavailable, "Trợ lý đang bận, bạn có thể điền form thủ công.");
        }
    }

    // ── Vocabulary (cache 10') ──────────────────────────────────────────────

    private sealed record Vocabulary(
        List<(Guid Id, string Name)> WorkspaceTypes,
        List<string> StyleCodes,
        List<ElementInputMap> ElementInputs);

    private async Task<Vocabulary> GetVocabularyAsync(Guid userId, CancellationToken ct)
    {
        var cacheKey = $"workspace-intake-vocab:{userId}";
        if (_cache.TryGetValue(cacheKey, out Vocabulary? cached) && cached is not null)
            return cached;

        var types = await _uow.WorkspaceTypes.GetAvailableForUserAsync(userId, ct);
        var styles = await _uow.Styles.GetAllAsync(ct);
        var inputMap = await _uow.ScoringConfig.GetElementInputMapAsync(ct);

        var vocab = new Vocabulary(
            types.Select(t => (t.Id, t.Name)).ToList(),
            styles.Where(s => s.IsActive).Select(s => s.Code).ToList(),
            inputMap);

        _cache.Set(cacheKey, vocab, VocabularyCacheTtl);
        return vocab;
    }

    // ── Prompt ───────────────────────────────────────────────────────────────

    private static string BuildSystemPrompt(Vocabulary vocab)
    {
        var workspaceTypeNames = string.Join(", ", vocab.WorkspaceTypes.Select(t => t.Name));
        var styleCodes = string.Join(", ", vocab.StyleCodes);
        var byKind = vocab.ElementInputs
            .GroupBy(m => m.InputKind)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(m => m.InputCode).Distinct()));

        var colors = byKind.GetValueOrDefault(ElementInputKind.Color, "(không có)");
        var materials = byKind.GetValueOrDefault(ElementInputKind.Material, "(không có)");
        var shapes = byKind.GetValueOrDefault(ElementInputKind.Shape, "(không có)");

        return
            "Bạn là bộ trích xuất dữ liệu (data extractor) cho form \"không gian làm việc\" của FengDeskAI. " +
            "Nhiệm vụ DUY NHẤT: đọc mô tả tiếng Việt của khách và trả về CHÍNH XÁC MỘT đối tượng JSON theo schema bên dưới — " +
            "KHÔNG kèm markdown, KHÔNG giải thích, KHÔNG chữ nào ngoài JSON.\n\n" +

            "## QUY TẮC TỐI QUAN TRỌNG\n" +
            "- KHÔNG được đoán hay suy diễn. Field nào không được nhắc rõ ràng → để null.\n" +
            "- Chỉ điền field khi văn bản nói TƯỜNG MINH. Vd \"cạnh cửa sổ\" KHÔNG có nghĩa là biết hướng; " +
            "chỉ điền deskOrientation/roomFacingDirection khi user nói rõ như \"hướng đông\", \"bàn quay mặt về hướng tây\", \"nhìn về phía nam\".\n" +
            "- CHỈ dùng đúng giá trị trong danh sách cho phép bên dưới — không tự bịa từ khác, không dịch nghĩa.\n" +
            "- mentionedFields: liệt kê CHÍNH XÁC những field-key (theo tên trong schema) mà bạn tin user CÓ nhắc đến trong văn bản, " +
            "kể cả khi bạn không map được ra giá trị hợp lệ (dùng để tính độ tự tin) — đừng liệt kê field user không hề nhắc.\n\n" +

            "## SCHEMA (trả đúng các key sau, đúng kiểu, thiếu thì null)\n" +
            "{\n" +
            "  \"name\": string|null,                 // tên gợi nhớ ngắn cho không gian, vd \"Bàn làm việc tại nhà\"\n" +
            "  \"locationType\": string|null,          // MỘT trong: " + string.Join(", ", Enum.GetNames<LocationType>()) + "\n" +
            "  \"workspaceType\": string|null,         // TÊN loại không gian, MỘT trong: " + workspaceTypeNames + "\n" +
            "  \"styleCode\": string|null,             // MỘT trong: " + styleCodes + "\n" +
            "  \"lighting\": string|null,              // MỘT trong: " + string.Join(", ", Enum.GetNames<LightingType>()) + "\n" +
            "  \"deskType\": string|null,              // MỘT trong: " + string.Join(", ", Enum.GetNames<DeskType>()) + " — null nếu không có bàn\n" +
            "  \"deskOrientation\": string|null,       // hướng bàn quay mặt về, MỘT trong: " + string.Join(", ", Enum.GetNames<CompassDirection>()) + "\n" +
            "  \"roomFacingDirection\": string|null,   // hướng cửa/phòng, cùng danh sách hướng trên\n" +
            "  \"workPurpose\": string|null,           // MỘT trong: " + string.Join(", ", Enum.GetNames<WorkPurpose>()) + "\n" +
            "  \"deskArea\": number|null,              // diện tích MẶT BÀN, đơn vị cm² (vd bàn 1.2m x 0.6m = 7200), chỉ điền khi user nói rõ kích thước\n" +
            "  \"inputs\": [ { \"kind\": \"Color\"|\"Material\"|\"Shape\", \"code\": string } ],  // màu/vật liệu/hình khối user nhắc\n" +
            "        // Color MỘT trong: " + colors + "\n" +
            "        // Material MỘT trong: " + materials + "\n" +
            "        // Shape MỘT trong: " + shapes + "\n" +
            "  \"mentionedFields\": string[]           // các field-key ở trên mà user CÓ nhắc đến (bất kể map được hay không)\n" +
            "}\n\n" +

            "## VÍ DỤ 1\n" +
            "User: \"Bàn làm việc ở nhà cạnh cửa sổ hướng đông, nhiều nắng sáng, bàn gỗ màu nâu, tôi hay ngồi học bài\"\n" +
            "{\"name\":\"Bàn làm việc tại nhà\",\"locationType\":\"Home\",\"workspaceType\":null,\"styleCode\":null," +
            "\"lighting\":\"Natural\",\"deskType\":null,\"deskOrientation\":null,\"roomFacingDirection\":\"East\"," +
            "\"workPurpose\":\"Study\",\"deskArea\":null," +
            "\"inputs\":[{\"kind\":\"Material\",\"code\":\"Wood\"},{\"kind\":\"Color\",\"code\":\"Brown\"}]," +
            "\"mentionedFields\":[\"name\",\"locationType\",\"lighting\",\"roomFacingDirection\",\"workPurpose\",\"inputs\"]}\n\n" +

            "## VÍ DỤ 2 (mơ hồ — hầu hết null)\n" +
            "User: \"Phòng tôi khá đẹp\"\n" +
            "{\"name\":null,\"locationType\":null,\"workspaceType\":null,\"styleCode\":null,\"lighting\":null," +
            "\"deskType\":null,\"deskOrientation\":null,\"roomFacingDirection\":null,\"workPurpose\":null,\"deskArea\":null," +
            "\"inputs\":[],\"mentionedFields\":[]}\n\n" +

            "Chỉ trả JSON, không thêm bất kỳ ký tự nào khác.";
    }

    // ── Raw AI output (chưa tin) ─────────────────────────────────────────────

    private sealed class RawDraft
    {
        public string? Name { get; set; }
        public string? LocationType { get; set; }
        public string? WorkspaceType { get; set; }
        public string? StyleCode { get; set; }
        public string? Lighting { get; set; }
        public string? DeskType { get; set; }
        public string? DeskOrientation { get; set; }
        public string? RoomFacingDirection { get; set; }
        public string? WorkPurpose { get; set; }
        public int? DeskArea { get; set; }
        public List<RawInput>? Inputs { get; set; }
        public List<string>? MentionedFields { get; set; }
    }

    private sealed class RawInput
    {
        public string? Kind { get; set; }
        public string? Code { get; set; }
    }

    private static RawDraft ParseRaw(string content)
    {
        var json = StripCodeFence(content);
        return JsonSerializer.Deserialize<RawDraft>(json, RawJsonOptions)
            ?? throw new JsonException("AI trả về JSON rỗng.");
    }

    private static string StripCodeFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0) return trimmed;
        trimmed = trimmed[(firstNewline + 1)..];

        var fenceEnd = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return (fenceEnd >= 0 ? trimmed[..fenceEnd] : trimmed).Trim();
    }

    // ── Normalize (chốt chặn thật — deterministic) ────────────────────────────

    private static WorkspaceProfileDraftResponse Normalize(RawDraft raw, Vocabulary vocab)
    {
        var draft = new WorkspaceProfileDraftResponse();
        var unrecognized = new List<string>();
        var resolved = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(raw.Name))
        {
            var name = raw.Name.Trim();
            draft.Name = name.Length > 100 ? name[..100] : name;
        }
        resolved["name"] = draft.Name is not null;

        if (TryParseEnum<LocationType>(raw.LocationType, out var locationType))
            draft.LocationType = locationType;
        else if (!string.IsNullOrWhiteSpace(raw.LocationType))
            unrecognized.Add($"Vị trí: \"{raw.LocationType}\"");
        resolved["locationType"] = draft.LocationType is not null;

        if (TryParseEnum<LightingType>(raw.Lighting, out var lighting))
            draft.Lighting = lighting;
        else if (!string.IsNullOrWhiteSpace(raw.Lighting))
            unrecognized.Add($"Ánh sáng: \"{raw.Lighting}\"");
        resolved["lighting"] = draft.Lighting is not null;

        if (TryParseEnum<DeskType>(raw.DeskType, out var deskType))
            draft.DeskType = deskType;
        else if (!string.IsNullOrWhiteSpace(raw.DeskType))
            unrecognized.Add($"Loại bàn: \"{raw.DeskType}\"");
        resolved["deskType"] = draft.DeskType is not null;

        if (TryParseEnum<CompassDirection>(raw.DeskOrientation, out var deskOrientation))
            draft.DeskOrientation = deskOrientation;
        else if (!string.IsNullOrWhiteSpace(raw.DeskOrientation))
            unrecognized.Add($"Hướng bàn: \"{raw.DeskOrientation}\"");
        resolved["deskOrientation"] = draft.DeskOrientation is not null;

        if (TryParseEnum<CompassDirection>(raw.RoomFacingDirection, out var roomFacing))
            draft.RoomFacingDirection = roomFacing;
        else if (!string.IsNullOrWhiteSpace(raw.RoomFacingDirection))
            unrecognized.Add($"Hướng phòng: \"{raw.RoomFacingDirection}\"");
        resolved["roomFacingDirection"] = draft.RoomFacingDirection is not null;

        if (TryParseEnum<WorkPurpose>(raw.WorkPurpose, out var workPurpose))
            draft.WorkPurpose = workPurpose;
        else if (!string.IsNullOrWhiteSpace(raw.WorkPurpose))
            unrecognized.Add($"Mục đích: \"{raw.WorkPurpose}\"");
        resolved["workPurpose"] = draft.WorkPurpose is not null;

        if (!string.IsNullOrWhiteSpace(raw.WorkspaceType))
        {
            var match = MatchWorkspaceType(raw.WorkspaceType, vocab.WorkspaceTypes);
            if (match is { } id) draft.WorkspaceTypeId = id;
            else unrecognized.Add($"Loại không gian: \"{raw.WorkspaceType}\"");
        }
        resolved["workspaceType"] = draft.WorkspaceTypeId is not null;

        if (!string.IsNullOrWhiteSpace(raw.StyleCode))
        {
            var code = vocab.StyleCodes.FirstOrDefault(
                c => string.Equals(c, raw.StyleCode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (code is not null) draft.StyleCode = code;
            else unrecognized.Add($"Phong cách: \"{raw.StyleCode}\"");
        }
        resolved["styleCode"] = draft.StyleCode is not null;

        if (raw.DeskArea is { } area)
        {
            if (area is >= MinDeskAreaCm2 and <= MaxDeskAreaCm2) draft.DeskArea = area;
            else unrecognized.Add($"Diện tích bàn: {area} (ngoài khoảng hợp lệ)");
        }
        resolved["deskArea"] = draft.DeskArea is not null;

        foreach (var rawInput in raw.Inputs ?? new List<RawInput>())
        {
            if (string.IsNullOrWhiteSpace(rawInput.Code)) continue;

            if (TryParseEnum<ElementInputKind>(rawInput.Kind, out var kind))
            {
                var match = vocab.ElementInputs.FirstOrDefault(
                    m => m.InputKind == kind && string.Equals(m.InputCode, rawInput.Code.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                    draft.Inputs.Add(new WorkspaceProfileInputDto(kind, match.InputCode));
                else
                    unrecognized.Add($"{rawInput.Kind}: \"{rawInput.Code}\"");
            }
            else
            {
                unrecognized.Add(rawInput.Code);
            }
        }
        draft.Inputs = draft.Inputs.DistinctBy(i => (i.InputKind, i.InputCode)).ToList();
        resolved["inputs"] = draft.Inputs.Count > 0;

        draft.Unrecognized = unrecognized;
        draft.Confidence = ComputeConfidence(raw.MentionedFields, resolved);
        return draft;
    }

    private static bool TryParseEnum<T>(string? value, out T result) where T : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse(value.Trim(), ignoreCase: true, out T parsed)
            && Enum.IsDefined(parsed))
        {
            result = parsed;
            return true;
        }
        result = default;
        return false;
    }

    private static Guid? MatchWorkspaceType(string text, List<(Guid Id, string Name)> types)
    {
        var needle = NormalizeForMatch(text);

        var exact = types.FirstOrDefault(t => NormalizeForMatch(t.Name) == needle);
        if (exact.Id != Guid.Empty) return exact.Id;

        var contains = types.FirstOrDefault(t =>
        {
            var name = NormalizeForMatch(t.Name);
            return name.Contains(needle, StringComparison.Ordinal) || needle.Contains(name, StringComparison.Ordinal);
        });
        return contains.Id != Guid.Empty ? contains.Id : null;
    }

    /// <summary>Bỏ dấu tiếng Việt + hạ chữ thường để so khớp mờ tên loại không gian.</summary>
    private static string NormalizeForMatch(string s)
    {
        var replaced = s.Trim().Replace('đ', 'd').Replace('Đ', 'D').ToLowerInvariant();
        var formD = replaced.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>= tỉ lệ field resolve được / field AI tự báo có nhắc đến, clamp 0..1. Không nhắc gì → 0.</summary>
    private static decimal ComputeConfidence(List<string>? mentionedFields, Dictionary<string, bool> resolved)
    {
        var mentioned = (mentionedFields ?? new List<string>())
            .Where(f => !string.IsNullOrWhiteSpace(f) && resolved.ContainsKey(f.Trim()))
            .Select(f => f.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (mentioned.Count == 0) return 0m;

        var resolvedCount = mentioned.Count(f => resolved.TryGetValue(f, out var ok) && ok);
        return Math.Clamp((decimal)resolvedCount / mentioned.Count, 0m, 1m);
    }
}
