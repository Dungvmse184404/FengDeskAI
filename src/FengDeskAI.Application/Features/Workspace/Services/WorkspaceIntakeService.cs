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
    // Kết quả job giữ đủ lâu để client F5/kết nối lại vẫn lấy được, nhưng không phình bộ nhớ.
    private static readonly TimeSpan JobResultTtl = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions RawJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IAiChatClient _client;
    private readonly IUnitOfWork _uow;
    private readonly IMemoryCache _cache;
    private readonly IFileStorage _storage;
    private readonly IImageEncoder _encoder;
    private readonly IWorkspaceIntakeQueue _queue;
    private readonly IWorkspaceIntakeNotifier _notifier;
    private readonly IAiActivityNotifier _activity;
    private readonly WorkspaceIntakeOptions _options;
    private readonly ILogger<WorkspaceIntakeService> _logger;

    public WorkspaceIntakeService(
        IAiChatClient client,
        IUnitOfWork uow,
        IMemoryCache cache,
        IFileStorage storage,
        IImageEncoder encoder,
        IWorkspaceIntakeQueue queue,
        IWorkspaceIntakeNotifier notifier,
        IAiActivityNotifier activity,
        IOptions<WorkspaceIntakeOptions> options,
        ILogger<WorkspaceIntakeService> logger)
    {
        _client = client;
        _uow = uow;
        _cache = cache;
        _storage = storage;
        _encoder = encoder;
        _queue = queue;
        _notifier = notifier;
        _activity = activity;
        _options = options.Value;
        _logger = logger;
    }

    private static string JobKey(string operationId) => $"workspace-intake-job:{operationId}";

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

    /// <summary>Validate + chuẩn hóa input dùng chung cho cả sync (ParseAsync) lẫn async (StartParseAsync). Trả lỗi hoặc null.</summary>
    private static string? ValidateRequest(
        ParseWorkspaceDescriptionRequest request, out string description, out List<string> imageUrls)
    {
        description = request.Description?.Trim() ?? string.Empty;
        imageUrls = (request.ImageUrls ?? new List<string>())
            .Where(u => !string.IsNullOrWhiteSpace(u)).Take(MaxImages).ToList();

        if (imageUrls.Count == 0 && description.Length is < MinDescriptionLength or > MaxDescriptionLength)
            return $"Mô tả phải từ {MinDescriptionLength} đến {MaxDescriptionLength} ký tự (hoặc đính kèm ít nhất 1 ảnh).";
        if (description.Length > MaxDescriptionLength)
            return $"Mô tả tối đa {MaxDescriptionLength} ký tự.";
        return null;
    }

    /// <summary>
    /// Async: validate nhanh rồi đẩy job vào hàng đợi nền, trả operationId ngay. Đặt trạng thái "pending"
    /// vào cache trước để nếu FE poll trước khi worker chạy vẫn thấy job tồn tại.
    /// </summary>
    public Task<IServiceResult<WorkspaceIntakeStartResponse>> StartParseAsync(
        Guid userId, ParseWorkspaceDescriptionRequest request, CancellationToken ct = default)
    {
        var error = ValidateRequest(request, out var description, out var imageUrls);
        if (error is not null)
            return Task.FromResult<IServiceResult<WorkspaceIntakeStartResponse>>(
                ServiceResult<WorkspaceIntakeStartResponse>.Failure(ApiStatusCodes.BadRequest, error));

        var operationId = Guid.NewGuid().ToString("N");
        _cache.Set(JobKey(operationId), WorkspaceIntakeJobStatusResponse.Pending(), JobResultTtl);
        _queue.Enqueue(new WorkspaceIntakeJob(operationId, userId, description, imageUrls, request.Think));

        _logger.LogInformation(
            "[WorkspaceIntake] Nhận job async {OperationId} cho user {UserId} ({Length} ký tự, {ImageCount} ảnh).",
            operationId, userId, description.Length, imageUrls.Count);

        return Task.FromResult<IServiceResult<WorkspaceIntakeStartResponse>>(
            ServiceResult<WorkspaceIntakeStartResponse>.Success(
                new WorkspaceIntakeStartResponse(operationId), "Đã bắt đầu phân tích."));
    }

    /// <summary>Worker nền gọi: phát tiến trình realtime, chạy parse, cache kết quả + push draft/lỗi. Best-effort.</summary>
    public async Task RunJobAsync(WorkspaceIntakeJob job, CancellationToken ct = default)
    {
        // Scope tự phát "done" khi dispose (trừ khi ta đặt phase cuối = "error").
        await using var activity = _activity.Begin(job.OperationId);
        await activity.PhaseAsync("thinking", ct: ct);

        var request = new ParseWorkspaceDescriptionRequest
        {
            Description = job.Description!,
            ImageUrls = job.ImageUrls,
            Think = job.Think,
        };
        // Stream thinking (nếu model bật think) → "chữ chạy" trên progress bar intake.
        var result = await ParseAsync(job.UserId, request, activity.ThinkingProgress(), ct);

        if (result.IsSuccess && result.Data is not null)
        {
            _cache.Set(JobKey(job.OperationId), WorkspaceIntakeJobStatusResponse.Done(result.Data), JobResultTtl);
            await _notifier.PublishResultAsync(job.OperationId, result.Data, ct);
        }
        else
        {
            var message = result.Message ?? "Trợ lý đang bận, bạn có thể điền form thủ công.";
            await activity.PhaseAsync("error", ct: ct); // chặn "done" tự phát → indicator hiện lỗi
            _cache.Set(JobKey(job.OperationId), WorkspaceIntakeJobStatusResponse.Failed(message), JobResultTtl);
            await _notifier.PublishFailedAsync(job.OperationId, message, ct);
        }
    }

    /// <summary>Trạng thái job từ cache (fallback khi FE lỡ mất event realtime). NotFound nếu không tồn tại/hết hạn.</summary>
    public IServiceResult<WorkspaceIntakeJobStatusResponse> GetJobStatus(string operationId)
    {
        if (_cache.TryGetValue(JobKey(operationId), out WorkspaceIntakeJobStatusResponse? status) && status is not null)
            return ServiceResult<WorkspaceIntakeJobStatusResponse>.Success(status);
        return ServiceResult<WorkspaceIntakeJobStatusResponse>.Failure(
            ApiStatusCodes.NotFound, "Không tìm thấy phiên phân tích (có thể đã hết hạn).");
    }

    public async Task<IServiceResult<WorkspaceProfileDraftResponse>> ParseAsync(
        Guid userId, ParseWorkspaceDescriptionRequest request,
        IProgress<AiStreamChunk>? onDelta = null, CancellationToken ct = default)
    {
        var error = ValidateRequest(request, out var description, out var imageUrls);
        if (error is not null)
            return ServiceResult<WorkspaceProfileDraftResponse>.Failure(ApiStatusCodes.BadRequest, error);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        AiChatCompletion? completion = null;
        try
        {
            var vocab = await GetVocabularyAsync(userId, ct);

            // Tải ảnh SONG SONG: 3 ảnh nối tiếp = 3 round-trip Supabase cộng dồn, trong khi chúng
            // hoàn toàn độc lập nhau. Giữ nguyên thứ tự ảnh theo thứ tự user gửi lên.
            List<string>? imagesBase64 = null;
            if (imageUrls.Count > 0)
            {
                imagesBase64 = (await Task.WhenAll(
                    imageUrls.Select(url => _encoder.FetchAsBase64Async(url, ct)))).ToList();
            }

            var userContent = description.Length > 0 ? description : "(Không có mô tả chữ - chỉ có ảnh, hãy phân tích ảnh.)";
            var messages = new List<AiChatMessage>
            {
                new(AiChatRoles.System, BuildSystemPrompt(vocab, hasImages: imagesBase64 is { Count: > 0 })),
                new(AiChatRoles.User, userContent, imagesBase64),
            };

            // Có ảnh → PHẢI dùng vision model, không thì model text bỏ qua ảnh (không nhận màu/cây cảnh...).
            var hasImages = imagesBase64 is { Count: > 0 };
            var model = hasImages && !string.IsNullOrWhiteSpace(_options.VisionModel)
                ? _options.VisionModel!
                : _options.Model;
            // Think: cho phép override theo từng request (công tắc phía user) — null = theo cấu hình Ai:Intake.
            var think = request.Think ?? _options.Think;

            _logger.LogInformation(
                "[WorkspaceIntake] Bắt đầu parse cho user {UserId} (mô tả {Length} ký tự, {ImageCount} ảnh, model {Model}, think={Think}).",
                userId, description.Length, imageUrls.Count, model, think);

            // Trần token: bật think thì khối suy luận cũng tính vào đây → phải nới rộng hơn.
            var maxOutputTokens = think == true
                ? _options.MaxOutputTokensWhenThinking
                : _options.MaxOutputTokens;

            // Request chỉ có chữ không cần ctx rộng như request có ảnh.
            var numCtx = hasImages
                ? (_options.NumCtxVision > 0 ? _options.NumCtxVision : (int?)null)
                : (_options.NumCtxText > 0 ? _options.NumCtxText : (int?)null);

            // Model + temperature riêng cho intake (Ai:Intake) — không dùng chung với chatbox.
            completion = await _client.CompleteAsync(
                model, messages, tools: null,
                options: new AiCompletionOptions(
                    _options.Temperature, _options.JsonMode, think, _options.Stream,
                    MaxOutputTokens: maxOutputTokens, NumCtx: numCtx),
                onDelta: onDelta, ct: ct);

            RawDraft raw;
            try
            {
                raw = ParseRaw(completion.Content);
            }
            catch (JsonException) when (think == true)
            {
                // Bật "suy nghĩ kỹ" mà model tiêu hết trần token vào khối suy luận → không kịp viết
                // JSON. Chạy lại NGAY một lượt không-think: nhanh, ổn định, và user vẫn có draft
                // thay vì nhận thông báo lỗi rồi phải điền tay toàn bộ.
                _logger.LogWarning(
                    "[WorkspaceIntake] Lượt think không ra JSON (nhiều khả năng chạm trần {Max} token) — chạy lại không-think.",
                    maxOutputTokens);

                completion = await _client.CompleteAsync(
                    model, messages, tools: null,
                    options: new AiCompletionOptions(
                        _options.Temperature, _options.JsonMode, Think: false, _options.Stream,
                        MaxOutputTokens: _options.MaxOutputTokens, NumCtx: numCtx),
                    onDelta: onDelta, ct: ct);

                raw = ParseRaw(completion.Content);
            }

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
        // Chỉ đưa vào prompt tag công khai + tag của chính user — AI không được gợi ý tag riêng
        // của người khác (cùng quy tắc với picker: ElementInputMap.IsVisibleTo).
        var inputMap = (await _uow.ScoringConfig.GetElementInputMapAsync(ct))
            .Where(m => m.IsVisibleTo(userId))
            .ToList();

        var vocab = new Vocabulary(
            types.Select(t => (t.Id, t.Name)).ToList(),
            styles.Where(s => s.IsActive).Select(s => s.Code).ToList(),
            inputMap);

        _cache.Set(cacheKey, vocab, VocabularyCacheTtl);
        return vocab;
    }

    // ── Prompt ───────────────────────────────────────────────────────────────

    /// <summary>
    /// System prompt cho intake. Giữ NGẮN có chủ đích: mỗi token prompt đều phải prefill lại ở mỗi
    /// request, và khi user bật "suy nghĩ kỹ" thì prompt dài còn kéo theo khối suy luận dài hơn.
    /// Quy tắc được nén thành gạch đầu dòng 1 câu; ví dụ rút còn 2 ca quan trọng nhất (đủ tín hiệu / mơ hồ).
    /// </summary>
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
            ? "- Ảnh là bằng chứng ngang hàng với chữ: chỉ điền khi NHÌN THẤY RÕ (thấy nắng qua cửa sổ → Natural; " +
              "thấy bàn gỗ → Material=Wood; thấy bể cá/cây/gương → DecorItem). Không đoán hướng từ ảnh.\n"
            : "";

        return
            "Trích xuất dữ liệu form \"không gian làm việc\" (FengDeskAI). Đọc mô tả (Việt hoặc Anh) " +
            (hasImages ? "và ảnh " : "") +
            "→ trả DUY NHẤT một object JSON đúng schema. Không markdown, không giải thích, không chữ nào ngoài JSON. " +
            "Mọi giá trị phải lấy nguyên văn từ danh sách cho phép (mã tiếng Anh), kể cả khi mô tả là tiếng Việt.\n" +
            "Suy luận NGẮN GỌN - đây là tác vụ trích xuất, không phải giải đố.\n\n" +

            "## QUY TẮC\n" +
            "- Không đoán. Field không được nhắc TƯỜNG MINH → null. \"cạnh cửa sổ\" KHÔNG cho biết hướng; " +
            "chỉ điền hướng khi user nói rõ (\"hướng đông\", \"bàn quay về tây\").\n" +
            imageRule +
            "- inputs là NGOẠI LỆ của luật trên: liệt kê CÀNG NHIỀU tín hiệu nhận ra càng tốt, không giới hạn " +
            "1 cái mỗi loại (\"bàn gỗ, ghế da, bể cá, cây xanh\" → đủ 4 mục). Thà dư còn hơn sót - user sửa lại được.\n" +
            "- mentionedFields: những field-key user CÓ nhắc, kể cả khi không map ra giá trị hợp lệ.\n" +
            "- hasDesk: true nếu có nhắc bàn làm việc (loại bàn / hướng bàn / workspaceType kiểu bàn-văn phòng); " +
            "false nếu rõ ràng là loại phòng không có bàn (bếp, phòng khách, phòng ngủ, phòng ăn, ban công, phòng tập) " +
            "và không nhắc bàn nào; null nếu không đủ căn cứ.\n" +
            "- workPurpose: ngoại lệ DUY NHẤT được suy ra - khi workspaceType chắc chắn và chỉ có một công năng hiển nhiên " +
            "(Kitchen→Cooking, Bedroom→Sleep, Dining Room→Dining, Kids Room→Childcare, Home Gym→Exercise). " +
            "Home Office / Personal Desk... KHÔNG áp dụng, vẫn để null nếu không nói rõ.\n\n" +

            "## SCHEMA (đủ key, thiếu thì null)\n" +
            "{\n" +
            "  \"name\": string|null,               // tên ngắn gợi nhớ, vd \"Bàn làm việc tại nhà\"\n" +
            "  \"locationType\": string|null,        // " + string.Join("|", Enum.GetNames<LocationType>()) + "\n" +
            "  \"workspaceType\": string|null,       // " + workspaceTypeNames + "\n" +
            "  \"styleCode\": string|null,           // " + styleCodes + "\n" +
            "  \"lighting\": string|null,            // " + string.Join("|", Enum.GetNames<LightingType>()) + "\n" +
            "  \"hasDesk\": boolean|null,\n" +
            "  \"deskType\": string|null,            // " + string.Join("|", Enum.GetNames<DeskType>()) + " — null nếu không có bàn\n" +
            "  \"deskOrientation\": string|null,     // hướng bàn quay về: " + string.Join("|", Enum.GetNames<CompassDirection>()) + "\n" +
            "  \"roomFacingDirection\": string|null, // hướng cửa/phòng, cùng danh sách hướng\n" +
            "  \"workPurpose\": string|null,         // " + string.Join("|", Enum.GetNames<WorkPurpose>()) + "\n" +
            "  \"deskArea\": number|null,            // mặt bàn, cm² (1.2m x 0.6m = 7200) — chỉ khi nói rõ kích thước\n" +
            "  \"inputs\": [{\"kind\":\"Color\"|\"Material\"|\"Shape\"|\"DecorItem\",\"code\":string}],\n" +
            "        // Color: " + colors + "\n" +
            "        // Material: " + materials + "\n" +
            "        // Shape: " + shapes + "\n" +
            "        // DecorItem: " + decorItems + "\n" +
            "  \"mentionedFields\": string[]\n" +
            "}\n\n" +

            "## VÍ DỤ\n" +
            "\"Nhà bếp rộng, thuận nắng, nội thất gỗ, có bể cá lớn, treo tranh và vài chậu cây\" →\n" +
            "{\"name\":null,\"locationType\":\"Home\",\"workspaceType\":\"Kitchen\",\"styleCode\":null,\"lighting\":\"Natural\"," +
            "\"hasDesk\":false,\"deskType\":null,\"deskOrientation\":null,\"roomFacingDirection\":null,\"workPurpose\":\"Cooking\"," +
            "\"deskArea\":null,\"inputs\":[{\"kind\":\"Material\",\"code\":\"Wood\"},{\"kind\":\"DecorItem\",\"code\":\"FishTank\"}," +
            "{\"kind\":\"DecorItem\",\"code\":\"Painting\"},{\"kind\":\"DecorItem\",\"code\":\"Plant\"}]," +
            "\"mentionedFields\":[\"locationType\",\"workspaceType\",\"lighting\",\"hasDesk\",\"workPurpose\",\"inputs\"]}\n\n" +
            "\"Phòng tôi khá đẹp\" → mọi field null, \"inputs\":[], \"mentionedFields\":[].\n\n" +

            "Chỉ trả JSON.";
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
        var json = ExtractJsonObject(StripCodeFence(content))
            ?? throw new JsonException("Không tìm thấy object JSON nào trong phản hồi của AI.");

        return JsonSerializer.Deserialize<RawDraft>(json, RawJsonOptions)
            ?? throw new JsonException("AI trả về JSON rỗng.");
    }

    /// <summary>
    /// Bóc object JSON đầu tiên nằm trong text. KHÔNG giả định cả chuỗi là JSON — model hay kèm chữ
    /// quanh nó, và khi bật think mà content rỗng thì transport đưa thẳng khối suy luận sang đây
    /// (JSON thật thường nằm ở cuối khối đó).
    /// Đếm ngoặc có nhận biết chuỗi + escape, nếu không thì một dấu { nằm trong chuỗi là lệch hết.
    /// </summary>
    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        while (start >= 0)
        {
            int depth = 0;
            bool inString = false, escaped = false;

            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];

                if (escaped) { escaped = false; continue; }
                if (inString && c == '\\') { escaped = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;

                if (c == '{') depth++;
                else if (c == '}' && --depth == 0) return text[start..(i + 1)];
            }

            // Object mở ra mà không đóng (bị cắt giữa chừng) → thử object kế tiếp nếu còn.
            start = text.IndexOf('{', start + 1);
        }
        return null;
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
