using System.Globalization;
using System.Text;
using System.Text.Json;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Media;
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
    private const int MaxImages = 3;
    private static readonly TimeSpan VocabularyCacheTtl = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions RawJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IAiChatClient _client;
    private readonly IUnitOfWork _uow;
    private readonly IMemoryCache _cache;
    private readonly IFileStorage _storage;
    private readonly IImageEncoder _encoder;
    private readonly WorkspaceIntakeOptions _options;
    private readonly ILogger<WorkspaceIntakeService> _logger;

    public WorkspaceIntakeService(
        IAiChatClient client,
        IUnitOfWork uow,
        IMemoryCache cache,
        IFileStorage storage,
        IImageEncoder encoder,
        IOptions<WorkspaceIntakeOptions> options,
        ILogger<WorkspaceIntakeService> logger)
    {
        _client = client;
        _uow = uow;
        _cache = cache;
        _storage = storage;
        _encoder = encoder;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IServiceResult<string>> UploadImageAsync(
        Guid userId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        if (!ImageUpload.IsAllowed(contentType))
            return ServiceResult<string>.Failure(ApiStatusCodes.UnprocessableEntity, "Chỉ chấp nhận ảnh JPG, PNG, BMP hoặc GIF.");

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ImageUpload.ExtensionFor(contentType);
        var objectPath = $"Workspace_intake/{userId}/{Guid.NewGuid():N}{ext}";

        var stored = await _storage.UploadAsync(objectPath, content, contentType, ct);
        return ServiceResult<string>.Success(stored.Url, "Tải ảnh thành công.");
    }

    public async Task<IServiceResult<WorkspaceProfileDraftResponse>> ParseAsync(
        Guid userId, ParseWorkspaceDescriptionRequest request, CancellationToken ct = default)
    {
        var description = request.Description?.Trim() ?? string.Empty;
        var imageUrls = (request.ImageUrls ?? new List<string>())
            .Where(u => !string.IsNullOrWhiteSpace(u)).Take(MaxImages).ToList();

        if (imageUrls.Count == 0 && description.Length is < MinDescriptionLength or > MaxDescriptionLength)
            return ServiceResult<WorkspaceProfileDraftResponse>.Failure(
                ApiStatusCodes.BadRequest, $"Mô tả phải từ {MinDescriptionLength} đến {MaxDescriptionLength} ký tự (hoặc đính kèm ít nhất 1 ảnh).");
        if (description.Length > MaxDescriptionLength)
            return ServiceResult<WorkspaceProfileDraftResponse>.Failure(
                ApiStatusCodes.BadRequest, $"Mô tả tối đa {MaxDescriptionLength} ký tự.");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        AiChatCompletion? completion = null;
        try
        {
            var vocab = await GetVocabularyAsync(userId, ct);

            List<string>? imagesBase64 = null;
            if (imageUrls.Count > 0)
            {
                imagesBase64 = new List<string>(imageUrls.Count);
                foreach (var url in imageUrls)
                    imagesBase64.Add(await _encoder.FetchAsBase64Async(url, ct));
            }

            var userContent = description.Length > 0 ? description : "(Không có mô tả chữ — chỉ có ảnh, hãy phân tích ảnh.)";
            var messages = new List<AiChatMessage>
            {
                new(AiChatRoles.System, BuildSystemPrompt(vocab, hasImages: imagesBase64 is { Count: > 0 })),
                new(AiChatRoles.User, userContent, imagesBase64),
            };

            _logger.LogInformation(
                "[WorkspaceIntake] Bắt đầu parse cho user {UserId} (mô tả {Length} ký tự, {ImageCount} ảnh, model {Model}, think={Think}).",
                userId, description.Length, imageUrls.Count, _options.Model, _options.Think);

            // Model + temperature riêng cho intake (Ai:Intake) — không dùng chung với chatbox.
            completion = await _client.CompleteAsync(
                _options.Model, messages, tools: null,
                options: new AiCompletionOptions(_options.Temperature, _options.JsonMode, _options.Think), ct: ct);

            var raw = ParseRaw(completion.Content);
            var draft = Normalize(raw, vocab);

            _logger.LogInformation(
                "[WorkspaceIntake] Parse thành công cho user {UserId} sau {ElapsedMs}ms — confidence={Confidence}, unrecognized={UnrecognizedCount}.",
                userId, sw.ElapsedMilliseconds, draft.Confidence, draft.Unrecognized.Count);

            return ServiceResult<WorkspaceProfileDraftResponse>.Success(draft);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Log NGUYÊN VĂN content model trả về (cắt bớt) — lý do phổ biến nhất khiến parse thất bại
            // là model trả JSON kèm rác/markdown hoặc field sai schema; không có dòng này thì không cách
            // nào biết model đã nói gì để sửa prompt.
            var rawContent = completion?.Content;
            var preview = string.IsNullOrEmpty(rawContent)
                ? "(rỗng)"
                : rawContent.Length > 500 ? rawContent[..500] + "…(cắt bớt)" : rawContent;

            _logger.LogWarning(ex,
                "[WorkspaceIntake] Parse thất bại cho user {UserId} sau {ElapsedMs}ms. AI trả về: {RawContent}",
                userId, sw.ElapsedMilliseconds, preview);

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

    private static string BuildSystemPrompt(Vocabulary vocab, bool hasImages = false)
    {
        var workspaceTypeNames = string.Join(", ", vocab.WorkspaceTypes.Select(t => t.Name));
        var styleCodes = string.Join(", ", vocab.StyleCodes);
        var byKind = vocab.ElementInputs
            .GroupBy(m => m.InputKind)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(m => m.InputCode).Distinct()));

        var colors = byKind.GetValueOrDefault(ElementInputKind.Color, "(không có)");
        var materials = byKind.GetValueOrDefault(ElementInputKind.Material, "(không có)");
        var shapes = byKind.GetValueOrDefault(ElementInputKind.Shape, "(không có)");
        var decorItems = byKind.GetValueOrDefault(ElementInputKind.DecorItem, "(không có)");

        var imageRule = hasImages
            ? "- Có kèm theo (các) ẢNH chụp không gian. Dùng ảnh làm bằng chứng NGANG HÀNG với mô tả chữ: " +
              "chỉ điền field khi NHÌN THẤY RÕ trong ảnh (vd thấy rõ ánh sáng tự nhiên từ cửa sổ → lighting=Natural; " +
              "thấy rõ bàn gỗ → inputs Material=Wood; thấy bể cá/cây xanh/gương... → inputs DecorItem tương ứng). " +
              "KHÔNG suy diễn những gì không thấy rõ trong ảnh (vd không đoán hướng phòng chỉ vì thấy cửa sổ có nắng).\n"
            : "";

        return
            "Bạn là bộ trích xuất dữ liệu (data extractor) cho form \"không gian làm việc\" của FengDeskAI. " +
            "Nhiệm vụ DUY NHẤT: đọc mô tả của khách — tiếng Việt HOẶC tiếng Anh, tự nhận diện ngôn ngữ — (và ảnh không gian nếu có) " +
            "rồi trả về CHÍNH XÁC MỘT đối tượng JSON theo schema bên dưới — " +
            "KHÔNG kèm markdown, KHÔNG giải thích, KHÔNG chữ nào ngoài JSON. Toàn bộ giá trị field/code vẫn PHẢI theo đúng danh sách " +
            "cho phép bên dưới (đều là mã tiếng Anh cố định) dù mô tả gốc là tiếng Anh hay tiếng Việt.\n\n" +

            "## QUY TẮC TỐI QUAN TRỌNG\n" +
            "- KHÔNG được đoán hay suy diễn. Field nào không được nhắc rõ ràng → để null.\n" +
            "- Chỉ điền field khi văn bản nói TƯỜNG MINH. Vd \"cạnh cửa sổ\" KHÔNG có nghĩa là biết hướng; " +
            "chỉ điền deskOrientation/roomFacingDirection khi user nói rõ như \"hướng đông\", \"bàn quay mặt về hướng tây\", \"nhìn về phía nam\".\n" +
            imageRule +
            "- CHỈ dùng đúng giá trị trong danh sách cho phép bên dưới — không tự bịa từ khác, không dịch nghĩa.\n" +
            "- inputs (màu/vật liệu/hình khối/vật trang trí): đây là NGOẠI LỆ — hãy liệt kê CÀNG NHIỀU tín hiệu bạn nhận thấy CÀNG TỐT, " +
            "KHÔNG giới hạn 1 cái mỗi loại (vd nếu user nhắc \"bàn gỗ, ghế da, có bể cá và cây xanh\" → liệt kê đủ cả Material=Wood, Material=Leather, " +
            "DecorItem=FishTank, DecorItem=Plant). Rủi ro thấp vì user luôn sửa/xoá lại được ở bước review — thà liệt kê dư rồi để user bỏ bớt, " +
            "còn hơn bỏ sót.\n" +
            "- mentionedFields: liệt kê CHÍNH XÁC những field-key (theo tên trong schema) mà bạn tin user CÓ nhắc đến trong văn bản, " +
            "kể cả khi bạn không map được ra giá trị hợp lệ (dùng để tính độ tự tin) — đừng liệt kê field user không hề nhắc.\n" +
            "- hasDesk: true nếu user MÔ TẢ rõ có bàn làm việc (nhắc \"bàn\", loại bàn, hướng bàn, hoặc workspaceType kiểu bàn làm việc/văn phòng); " +
            "false nếu user mô tả rõ đây là loại không gian KHÔNG có bàn làm việc (vd bếp, phòng khách, phòng ngủ, phòng ăn, ban công, phòng tập) " +
            "và không hề nhắc đến bàn nào; null nếu không đủ căn cứ để kết luận theo cả 2 hướng trên.\n" +
            "- NGOẠI LỆ DUY NHẤT cho quy tắc \"không đoán\": nếu workspaceType đã xác định chắc chắn và bản thân loại không gian đó " +
            "gắn với ĐÚNG MỘT công năng hiển nhiên (Kitchen→Cooking, Bedroom→Sleep, Dining Room→Dining, Kids Room→Childcare, Home Gym→Exercise), " +
            "được phép điền workPurpose tương ứng dù user không nói rõ từ \"mục đích\" — vì đây là suy ra từ định nghĩa loại phòng, không phải đoán mò. " +
            "Các workspaceType khác (Home Office, Personal Desk...) KHÔNG áp dụng ngoại lệ này — vẫn phải để workPurpose null nếu không nói rõ.\n\n" +

            "## SCHEMA (trả đúng các key sau, đúng kiểu, thiếu thì null)\n" +
            "{\n" +
            "  \"name\": string|null,                 // tên gợi nhớ ngắn cho không gian, vd \"Bàn làm việc tại nhà\"\n" +
            "  \"locationType\": string|null,          // MỘT trong: " + string.Join(", ", Enum.GetNames<LocationType>()) + "\n" +
            "  \"workspaceType\": string|null,         // TÊN loại không gian, MỘT trong: " + workspaceTypeNames + "\n" +
            "  \"styleCode\": string|null,             // MỘT trong: " + styleCodes + "\n" +
            "  \"lighting\": string|null,              // MỘT trong: " + string.Join(", ", Enum.GetNames<LightingType>()) + "\n" +
            "  \"hasDesk\": boolean|null,             // xem quy tắc hasDesk phía trên\n" +
            "  \"deskType\": string|null,              // MỘT trong: " + string.Join(", ", Enum.GetNames<DeskType>()) + " — null nếu không có bàn\n" +
            "  \"deskOrientation\": string|null,       // hướng bàn quay mặt về, MỘT trong: " + string.Join(", ", Enum.GetNames<CompassDirection>()) + "\n" +
            "  \"roomFacingDirection\": string|null,   // hướng cửa/phòng, cùng danh sách hướng trên\n" +
            "  \"workPurpose\": string|null,           // MỘT trong: " + string.Join(", ", Enum.GetNames<WorkPurpose>()) + "\n" +
            "  \"deskArea\": number|null,              // diện tích MẶT BÀN, đơn vị cm² (vd bàn 1.2m x 0.6m = 7200), chỉ điền khi user nói rõ kích thước\n" +
            "  \"inputs\": [ { \"kind\": \"Color\"|\"Material\"|\"Shape\"|\"DecorItem\", \"code\": string } ],  // hiện trạng phòng: màu chủ đạo/chất liệu nội thất/hình khối/vật trang trí user nhắc\n" +
            "        // Color (màu chủ đạo) MỘT trong: " + colors + "\n" +
            "        // Material (chất liệu nội thất, vd bàn ghế gỗ) MỘT trong: " + materials + "\n" +
            "        // Shape MỘT trong: " + shapes + "\n" +
            "        // DecorItem (vật trang trí cụ thể, vd bể cá/cây xanh/gương) MỘT trong: " + decorItems + "\n" +
            "  \"mentionedFields\": string[]           // các field-key ở trên mà user CÓ nhắc đến (bất kể map được hay không)\n" +
            "}\n\n" +

            "## VÍ DỤ 1\n" +
            "User: \"Bàn làm việc ở nhà cạnh cửa sổ hướng đông, nhiều nắng sáng, bàn gỗ màu nâu, tôi hay ngồi học bài\"\n" +
            "{\"name\":\"Bàn làm việc tại nhà\",\"locationType\":\"Home\",\"workspaceType\":null,\"styleCode\":null," +
            "\"lighting\":\"Natural\",\"hasDesk\":true,\"deskType\":null,\"deskOrientation\":null,\"roomFacingDirection\":\"East\"," +
            "\"workPurpose\":\"Study\",\"deskArea\":null," +
            "\"inputs\":[{\"kind\":\"Material\",\"code\":\"Wood\"},{\"kind\":\"Color\",\"code\":\"Brown\"}]," +
            "\"mentionedFields\":[\"name\",\"locationType\",\"lighting\",\"hasDesk\",\"roomFacingDirection\",\"workPurpose\",\"inputs\"]}\n\n" +

            "## VÍ DỤ 2 (mơ hồ — hầu hết null)\n" +
            "User: \"Phòng tôi khá đẹp\"\n" +
            "{\"name\":null,\"locationType\":null,\"workspaceType\":null,\"styleCode\":null,\"lighting\":null,\"hasDesk\":null," +
            "\"deskType\":null,\"deskOrientation\":null,\"roomFacingDirection\":null,\"workPurpose\":null,\"deskArea\":null," +
            "\"inputs\":[],\"mentionedFields\":[]}\n\n" +

            "## VÍ DỤ 3 (không gian rõ ràng không có bàn làm việc + LIỆT KÊ ĐỦ nhiều vật trang trí/chất liệu)\n" +
            "User: \"Đây sẽ là nhà bếp khá rộng rãi, thuận hướng nắng, nội thất đa phần là gỗ, có bể cá lớn, " +
            "treo thêm vài bức tranh và đặt vài chậu cây nhỏ cho tươi mát.\"\n" +
            "{\"name\":null,\"locationType\":\"Home\",\"workspaceType\":\"Kitchen\",\"styleCode\":null," +
            "\"lighting\":\"Natural\",\"hasDesk\":false,\"deskType\":null,\"deskOrientation\":null,\"roomFacingDirection\":null," +
            "\"workPurpose\":\"Cooking\",\"deskArea\":null," +
            "\"inputs\":[{\"kind\":\"Material\",\"code\":\"Wood\"},{\"kind\":\"DecorItem\",\"code\":\"FishTank\"}," +
            "{\"kind\":\"DecorItem\",\"code\":\"Painting\"},{\"kind\":\"DecorItem\",\"code\":\"Plant\"}]," +
            "\"mentionedFields\":[\"locationType\",\"workspaceType\",\"lighting\",\"hasDesk\",\"workPurpose\",\"inputs\"]}\n\n" +

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
        public bool? HasDesk { get; set; }
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

        draft.HasDesk = raw.HasDesk;
        resolved["hasDesk"] = draft.HasDesk is not null;

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
