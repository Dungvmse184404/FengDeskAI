using System.Text.Json;
using System.Text.Json.Serialization;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Workspace.DTOs;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Application.Features.Workspace.Services;

/// <summary>
/// Phân loại tag "hiện trạng phòng" do user tự đặt tên (không có sẵn trong element_input_map)
/// thành hành + weight, bằng AI — rồi CHUẨN HÓA DETERMINISTIC (clamp/normalize) trước khi lưu.
/// AI chỉ đề xuất; code luôn là chốt chặn cuối cùng quyết định weight thật sự nằm trong khoảng cho phép
/// (cùng triết lý với WorkspaceIntakeService: AI trích xuất, code validate).
/// </summary>
public sealed class WorkspaceElementInputClassifierService : IWorkspaceElementInputClassifierService
{
    private const int MaxLabelLength = 50;
    private const int MaxCodeLength = 30;
    private const int MaxElements = 2;

    /// <summary>Weight mỗi hành phải nằm trong [0.2, 1.0] — dưới 0.2 coi như nhiễu, không đáng một hành riêng.</summary>
    private const decimal MinWeight = 0.2m;
    private const decimal MaxWeight = 1.0m;

    private readonly IAiChatClient _client;
    private readonly IGenericRepository<ElementInputMap> _inputMap;
    private readonly IUnitOfWork _uow;
    private readonly WorkspaceIntakeOptions _options;
    private readonly ILogger<WorkspaceElementInputClassifierService> _logger;

    private static readonly JsonSerializerOptions RawJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public WorkspaceElementInputClassifierService(
        IAiChatClient client,
        IGenericRepository<ElementInputMap> inputMap,
        IUnitOfWork uow,
        IOptions<WorkspaceIntakeOptions> options,
        ILogger<WorkspaceElementInputClassifierService> logger)
    {
        _client = client;
        _inputMap = inputMap;
        _uow = uow;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IServiceResult<ClassifyElementInputResponse>> ClassifyAsync(
        ClassifyElementInputRequest request, CancellationToken ct = default)
    {
        var label = request.Label?.Trim() ?? string.Empty;
        if (label.Length is 0 or > MaxLabelLength)
            return ServiceResult<ClassifyElementInputResponse>.Failure(
                ApiStatusCodes.BadRequest, $"Tên tag phải từ 1 đến {MaxLabelLength} ký tự.");

        // Fast path: label chuẩn hóa trùng code đã có sẵn → dùng luôn map cũ, khỏi tốn 1 lượt gọi AI
        // và tránh trả về weight khác với cái đã lưu trong DB cho cùng 1 code.
        if (NormalizeCode(label) is { } candidateCode)
        {
            var already = await _inputMap.FindAsync(x => x.InputKind == request.Kind && x.InputCode == candidateCode, ct);
            if (already.Count > 0)
            {
                var existingResult = new ClassifyElementInputResponse(
                    candidateCode, already.Select(m => new ElementContributionDto(m.Element, m.Weight)).ToList());
                return ServiceResult<ClassifyElementInputResponse>.Success(existingResult);
            }
        }

        try
        {
            var messages = new List<AiChatMessage>
            {
                new(AiChatRoles.System, BuildPrompt()),
                new(AiChatRoles.User, label),
            };

            // Think=false + temperature 0: cần JSON ngắn gọn, ổn định — không cần model "suy nghĩ".
            var completion = await _client.CompleteAsync(
                _options.Model, messages, tools: null,
                options: new AiCompletionOptions(Temperature: 0, JsonMode: true, Think: false), ct: ct);

            var raw = ParseRaw(completion.Content);
            var normalized = Normalize(raw, label);
            if (normalized.Elements.Count == 0)
            {
                _logger.LogWarning("[ElementInputClassifier] AI không trả hành hợp lệ cho label \"{Label}\": {RawContent}", label, completion.Content);
                return ServiceResult<ClassifyElementInputResponse>.Failure(
                    ApiStatusCodes.UnprocessableEntity, "Không nhận diện được hành phù hợp — thử mô tả cụ thể hơn (vd chất liệu chính).");
            }

            var persisted = await PersistAsync(request.Kind, normalized, ct);

            _logger.LogInformation(
                "[ElementInputClassifier] Phân loại \"{Label}\" ({Kind}) → {Code}: {Elements}",
                label, request.Kind, persisted.Code,
                string.Join(", ", persisted.Elements.Select(e => $"{e.Element}={e.Weight}")));

            return ServiceResult<ClassifyElementInputResponse>.Success(persisted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[ElementInputClassifier] Phân loại thất bại cho label \"{Label}\".", label);
            return ServiceResult<ClassifyElementInputResponse>.Failure(
                ApiStatusCodes.ServiceUnavailable, "Không phân loại được, thử lại sau.");
        }
    }

    private static string BuildPrompt() =>
        "Bạn là bộ phân loại ngũ hành phong thủy cho một VẬT PHẨM/VẬT TRANG TRÍ mới do người dùng tự gõ tên " +
        "(không có sẵn trong danh sách hệ thống). Đọc TÊN vật phẩm (tiếng Việt hoặc tiếng Anh) và trả về " +
        "CHÍNH XÁC MỘT đối tượng JSON — KHÔNG markdown, KHÔNG giải thích, KHÔNG chữ nào ngoài JSON.\n\n" +

        "## SCHEMA\n" +
        "{\n" +
        "  \"code\": string,       // định danh tiếng Anh ngắn gọn kiểu PascalCase, không dấu, không khoảng trắng, tối đa 30 ký tự (vd \"Piano\", \"LacquerPainting\")\n" +
        "  \"elements\": [ { \"element\": \"Kim\"|\"Moc\"|\"Thuy\"|\"Hoa\"|\"Tho\", \"weight\": number } ]  // 1 đến 2 phần tử, weight ước lượng theo tỉ lệ, không cần tổng chính xác 1 (hệ thống sẽ tự chuẩn hóa)\n" +
        "}\n\n" +

        "## QUY TẮC\n" +
        "- Dựa trên chất liệu/hình dáng/công năng đặc trưng NHẤT của vật phẩm để suy luận hành — không suy diễn viển vông.\n" +
        "- Vật phẩm rõ ràng thuộc 1 hành duy nhất → 1 phần tử, weight=1.\n" +
        "- Vật phẩm pha trộn rõ 2 đặc tính (vd vừa kim loại vừa có nước) → 2 phần tử.\n" +
        "- KHÔNG vượt quá 2 phần tử. KHÔNG bịa hành ngoài 5 hành trên.\n\n" +

        "## VÍ DỤ\n" +
        "\"đàn piano\" (gỗ + dây kim loại) → {\"code\":\"Piano\",\"elements\":[{\"element\":\"Moc\",\"weight\":0.6},{\"element\":\"Kim\",\"weight\":0.4}]}\n" +
        "\"tranh sơn mài\" (tranh treo tường, sơn mài truyền thống) → {\"code\":\"LacquerPainting\",\"elements\":[{\"element\":\"Hoa\",\"weight\":0.5},{\"element\":\"Moc\",\"weight\":0.5}]}\n" +
        "\"quạt trần\" (cánh kim loại, tạo gió) → {\"code\":\"CeilingFan\",\"elements\":[{\"element\":\"Kim\",\"weight\":0.6},{\"element\":\"Thuy\",\"weight\":0.4}]}\n" +
        "\"chậu sen đá\" (cây mọng nước nhỏ) → {\"code\":\"Succulent\",\"elements\":[{\"element\":\"Moc\",\"weight\":1.0}]}\n\n" +

        "Chỉ trả JSON, không thêm bất kỳ ký tự nào khác.";

    private sealed class RawClassification
    {
        public string? Code { get; set; }
        public List<RawElement>? Elements { get; set; }
    }

    private sealed class RawElement
    {
        public string? Element { get; set; }
        public decimal? Weight { get; set; }
    }

    private static RawClassification ParseRaw(string content)
    {
        var json = StripCodeFence(content);
        return JsonSerializer.Deserialize<RawClassification>(json, RawJsonOptions)
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

    /// <summary>
    /// Chốt chặn deterministic: chuẩn hóa code (fallback từ label gốc nếu AI trả rác), gộp hành trùng,
    /// giữ tối đa <see cref="MaxElements"/> hành theo weight lớn nhất, CLAMP từng weight vào
    /// [<see cref="MinWeight"/>, <see cref="MaxWeight"/>] rồi normalize lại để tổng = 1.
    /// </summary>
    private static ClassifyElementInputResponse Normalize(RawClassification raw, string originalLabel)
    {
        var code = NormalizeCode(raw.Code) ?? NormalizeCode(originalLabel) ?? "CustomTag";

        var merged = new Dictionary<FengShuiElement, decimal>();
        foreach (var e in raw.Elements ?? new List<RawElement>())
        {
            if (!Enum.TryParse<FengShuiElement>(e.Element, ignoreCase: true, out var element)
                || !Enum.IsDefined(element))
                continue;

            var weight = e.Weight is > 0 ? e.Weight.Value : 0m;
            if (weight <= 0m) continue;

            merged[element] = merged.GetValueOrDefault(element) + weight;
        }

        var top = merged.OrderByDescending(kv => kv.Value).Take(MaxElements).ToList();
        if (top.Count == 0) return new ClassifyElementInputResponse(code, new List<ElementContributionDto>());

        var clamped = top.Select(kv => (kv.Key, Weight: Math.Clamp(kv.Value, MinWeight, MaxWeight))).ToList();
        var total = clamped.Sum(c => c.Weight);
        var normalized = clamped
            .Select(c => new ElementContributionDto(c.Key, Math.Round(c.Weight / total, 3)))
            .ToList();

        return new ClassifyElementInputResponse(code, normalized);
    }

    /// <summary>PascalCase, chỉ chữ/số, tối đa 30 ký tự. Trả null nếu không còn ký tự nào hợp lệ.</summary>
    private static string? NormalizeCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var parts = raw.Split(new[] { ' ', '-', '_', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        foreach (var part in parts)
        {
            var firstInPart = true;
            foreach (var ch in part)
            {
                if (!char.IsLetterOrDigit(ch) || ch >= 128) continue; // chỉ ASCII — code là định danh tiếng Anh
                sb.Append(firstInPart ? char.ToUpperInvariant(ch) : ch);
                firstInPart = false;
            }
        }

        if (sb.Length == 0) return null;
        var result = sb.ToString();
        return result.Length > MaxCodeLength ? result[..MaxCodeLength] : result;
    }

    /// <summary>
    /// Lưu map mới nếu (kind, code) chưa tồn tại. Luôn trả về giá trị THẬT SỰ đang nằm trong DB
    /// (map cũ nếu đã có — tránh trả 2 kết quả khác nhau cho cùng 1 code do đụng độ hiếm gặp).
    /// </summary>
    private async Task<ClassifyElementInputResponse> PersistAsync(
        ElementInputKind kind, ClassifyElementInputResponse result, CancellationToken ct)
    {
        var existing = await _inputMap.FindAsync(x => x.InputKind == kind && x.InputCode == result.Code, ct);
        if (existing.Count > 0)
            return new ClassifyElementInputResponse(
                result.Code, existing.Select(m => new ElementContributionDto(m.Element, m.Weight)).ToList());

        foreach (var e in result.Elements)
        {
            await _inputMap.AddAsync(new ElementInputMap
            {
                InputKind = kind,
                InputCode = result.Code,
                Element = e.Element,
                Weight = e.Weight,
            }, ct);
        }
        await _uow.SaveChangesAsync(ct);
        return result;
    }
}
