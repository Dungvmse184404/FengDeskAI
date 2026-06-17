using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Chat;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Chat;
using FengDeskAI.Domain.Enums.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Application.Features.CustomerCare.Services;

public sealed class AiChatService : IAiChatService
{
    private readonly IAiChatClient _client;
    private readonly IUnitOfWork _uow;
    private readonly IImageEncoder _encoder;
    private readonly AiChatOptions _options;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        IAiChatClient client,
        IUnitOfWork uow,
        IImageEncoder encoder,
        IOptions<AiChatOptions> options,
        ILogger<AiChatService> logger)
    {
        _client = client;
        _uow = uow;
        _encoder = encoder;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IServiceResult<AiChatResponse>> SendAsync(
        Guid userId, string? userRole, string? userEmail, string? userDisplayName,
        AiChatRequest request, CancellationToken ct = default)
    {
        var message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim();
        var imageUrls = request.ImageUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? new List<string>();
        if (message is null && imageUrls.Count == 0)
            return ServiceResult<AiChatResponse>.Failure(ApiStatusCodes.BadRequest, "Tin nhắn phải có nội dung hoặc ảnh.");

        if (!TryResolveModel(request.Model, out var model, out var modelError))
            return ServiceResult<AiChatResponse>.Failure(ApiStatusCodes.BadRequest, modelError!);

        // 1) Lấy/tạo hội thoại AI.
        Chatbox chatbox;
        if (request.ChatboxId is { } cbId)
        {
            var existing = await _uow.Chatboxes.GetByIdAsync(cbId, ct);
            if (existing is null || existing.Type != ChatboxType.Assistant || existing.SenderUserId != userId)
                return ServiceResult<AiChatResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy hội thoại AI của bạn.");
            chatbox = existing;
        }
        else
        {
            chatbox = await _uow.Chatboxes.GetOrCreateAssistantAsync(userId, request.ProductId, ct);
            await _uow.SaveChangesAsync(ct); // đảm bảo có ChatboxId trước khi thêm message
        }

        // 2) Lưu tin nhắn người dùng (kèm link ảnh — KHÔNG lưu nhị phân).
        var userMessage = new ChatMessage
        {
            ChatboxId = chatbox.Id,
            SenderUserId = userId,
            SenderRole = ChatSenderHelper.RoleFrom(userRole),
            SenderName = ChatSenderHelper.NameFrom(userEmail),
            Content = message,
            IsFromAi = false,
            Images = imageUrls.Select((url, i) => new ChatMessageImage { Url = url, SortOrder = i }).ToList(),
        };
        await _uow.ChatMessages.AddAsync(userMessage, ct);
        chatbox.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        // 3) Dựng payload: system (+danh tính +sản phẩm) + N lượt gần nhất (đã gồm tin vừa lưu).
        var history = await _uow.ChatMessages.GetRecentAsync(chatbox.Id, _options.MaxHistoryTurns * 2, ct);
        var outgoing = new List<AiChatMessage>(history.Count + 1);

        var systemPrompt = await BuildSystemPromptAsync(userDisplayName, chatbox.ProductId, ct);
        if (systemPrompt is not null)
            outgoing.Add(new AiChatMessage(AiChatRoles.System, systemPrompt));

        for (var i = 0; i < history.Count; i++)
        {
            var m = history[i];
            var isLast = i == history.Count - 1;
            outgoing.Add(await ToOutgoingAsync(m, isLast, ct));
        }

        // 4) Gọi LLM.
        AiChatCompletion completion;
        try
        {
            completion = await _client.CompleteAsync(model, outgoing, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AiChat] Gọi LLM thất bại (chatbox {ChatboxId}, model {Model}).", chatbox.Id, model);
            return ServiceResult<AiChatResponse>.Failure(
                ApiStatusCodes.ServiceUnavailable, "Không kết nối được tới dịch vụ AI. Vui lòng thử lại sau.");
        }

        // 5) Lưu câu trả lời của AI.
        var aiMessage = new ChatMessage
        {
            ChatboxId = chatbox.Id,
            SenderUserId = null,
            SenderRole = ChatRole.Assistant,
            SenderName = null,
            Content = completion.Content,
            IsFromAi = true,
        };
        await _uow.ChatMessages.AddAsync(aiMessage, ct);
        chatbox.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        // 6) Trả về lịch sử gần nhất (link ảnh để client hiển thị, không phải base64).
        var recent = await _uow.ChatMessages.GetRecentAsync(chatbox.Id, _options.MaxHistoryTurns * 2, ct);
        var turns = recent.Select(m => new AiChatTurn(
            m.SenderRole.ToString(),
            m.Content,
            m.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList())).ToList();

        return ServiceResult<AiChatResponse>.Success(new AiChatResponse
        {
            ChatboxId = chatbox.Id,
            Model = completion.Model,
            Reply = completion.Content,
            History = turns,
        });
    }

    /// <summary>Map 1 message DB → message gửi LLM. Chỉ encode ảnh base64 cho lượt hiện tại (tránh nặng).</summary>
    private async Task<AiChatMessage> ToOutgoingAsync(ChatMessage m, bool encodeImages, CancellationToken ct)
    {
        var wireRole = m.IsFromAi || m.SenderRole == ChatRole.Assistant ? AiChatRoles.Assistant : AiChatRoles.User;

        // Nhãn "[Role/name]" giúp AI phân biệt các bên (Ollama không có field name riêng).
        var label = wireRole == AiChatRoles.User ? $"[{m.SenderRole}/{m.SenderName ?? "?"}] " : string.Empty;
        var text = label + (m.Content ?? string.Empty);

        var imageLinks = m.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList();
        if (imageLinks.Count == 0)
            return new AiChatMessage(wireRole, text);

        if (!encodeImages)
            return new AiChatMessage(wireRole, $"{text} (kèm {imageLinks.Count} ảnh)".Trim());

        var base64 = new List<string>(imageLinks.Count);
        foreach (var link in imageLinks)
        {
            try { base64.Add(await _encoder.FetchAsBase64Async(link, ct)); }
            catch (Exception ex) { _logger.LogWarning(ex, "[AiChat] Không tải được ảnh {Url} để feed AI.", link); }
        }
        return new AiChatMessage(wireRole, text, base64.Count > 0 ? base64 : null);
    }

    private async Task<string?> BuildSystemPromptAsync(string? userDisplayName, Guid? productId, CancellationToken ct)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(_options.SystemPrompt))
            parts.Add(_options.SystemPrompt!.Trim());
        if (!string.IsNullOrWhiteSpace(userDisplayName))
            parts.Add($"Người dùng bạn đang trò chuyện tên là {userDisplayName!.Trim()}.");

        if (productId is { } pid)
        {
            var product = await _uow.Products.GetDetailAsync(pid, ct);
            if (product is not null)
            {
                var minPrice = product.Items?.Count > 0 ? product.Items.Min(it => it.Price) : (decimal?)null;
                var priceText = minPrice is { } p ? $" Giá từ {p:#,0}đ." : string.Empty;
                parts.Add($"Người dùng đang hỏi về sản phẩm \"{product.Name}\". " +
                          $"Mô tả: {product.Description ?? "(chưa có)"}.{priceText} " +
                          "Hãy tư vấn dựa trên thông tin sản phẩm này.");
            }
        }

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private bool TryResolveModel(string? requested, out string model, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(requested))
        {
            model = _options.DefaultModel;
            return true;
        }

        model = requested.Trim();
        if (_options.AllowedModels.Count > 0
            && !_options.AllowedModels.Contains(model, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Model '{model}' không được hỗ trợ. Cho phép: {string.Join(", ", _options.AllowedModels)}.";
            return false;
        }

        return true;
    }
}
