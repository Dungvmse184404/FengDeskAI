using System.Text.Json;
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
    private readonly IReadOnlyList<IAiTool> _tools;
    private readonly IChatRealtimeNotifier _notifier;
    private readonly AiChatOptions _options;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        IAiChatClient client,
        IUnitOfWork uow,
        IImageEncoder encoder,
        IEnumerable<IAiTool> tools,
        IChatRealtimeNotifier notifier,
        IOptions<AiChatOptions> options,
        ILogger<AiChatService> logger)
    {
        _client = client;
        _uow = uow;
        _encoder = encoder;
        _tools = tools.ToList();
        _notifier = notifier;
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

        // 1) Lấy/tạo phòng riêng user ↔ AI (chỉ user đó + AiBot).
        Chatbox chatbox;
        if (request.ChatboxId is { } cbId)
        {
            var existing = await _uow.Chatboxes.GetWithParticipantsAsync(cbId, ct);
            var isPrivateAiRoom = existing is not null
                && existing.Participants.Any(p => p.ParticipantType == ParticipantType.AiBot)
                && existing.Participants.Any(p => p.UserId == userId)
                && !existing.Participants.Any(p => p.UserId != null && p.UserId != userId);
            if (!isPrivateAiRoom)
                return ServiceResult<AiChatResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy hội thoại AI của bạn.");
            chatbox = existing!;
        }
        else
        {
            chatbox = await _uow.Chatboxes.GetOrCreateAssistantAsync(
                userId, ChatSenderHelper.TypeFrom(userRole), request.ProductId, ct);
            await _uow.SaveChangesAsync(ct); // đảm bảo có ChatboxId
        }

        // 2) Lưu tin của người dùng (kèm link ảnh).
        var userMessage = new ChatMessage
        {
            ChatboxId = chatbox.Id,
            SenderId = userId,
            SenderType = MessageSenderType.User,
            SenderName = ChatSenderHelper.NameFrom(userEmail),
            Content = message,
            Images = imageUrls.Select((url, i) => new ChatMessageImage { Url = url, SortOrder = i }).ToList(),
        };
        await _uow.ChatMessages.AddAsync(userMessage, ct);
        chatbox.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        // 3) Payload: system (+danh tính +sản phẩm) + N lượt gần nhất.
        var history = await _uow.ChatMessages.GetRecentAsync(chatbox.Id, _options.MaxHistoryTurns * 2, ct);
        var outgoing = new List<AiChatMessage>(history.Count + 1);

        var systemPrompt = await BuildSystemPromptAsync(userDisplayName, chatbox.ProductId, ct);
        if (systemPrompt is not null)
            outgoing.Add(new AiChatMessage(AiChatRoles.System, systemPrompt));

        // Phòng riêng (chỉ user + AI) → nạp ngữ cảnh từ các phòng CHUNG của user để "bàn luận tổng hợp".
        // Reply chỉ user thấy nên không lộ chéo. (Ở phòng chung sẽ KHÔNG gom — Phase 3.)
        var sharedContext = await BuildSharedContextAsync(userId, chatbox.Id, ct);
        if (sharedContext is not null)
            outgoing.Add(new AiChatMessage(AiChatRoles.System, sharedContext));

        for (var i = 0; i < history.Count; i++)
            outgoing.Add(await ToOutgoingAsync(history[i], encodeImages: i == history.Count - 1, ct));

        // 4) Gọi LLM (kèm vòng lặp tool calling nếu bật + model hỗ trợ).
        AiChatCompletion completion;
        try
        {
            var ctx = new AiToolContext(userId, userRole, userEmail, chatbox.Id);
            completion = await RunWithToolsAsync(model, outgoing, ctx, chatbox.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AiChat] Gọi LLM thất bại (chatbox {ChatboxId}, model {Model}).", chatbox.Id, model);
            return ServiceResult<AiChatResponse>.Failure(
                ApiStatusCodes.ServiceUnavailable, "Không kết nối được tới dịch vụ AI. Vui lòng thử lại sau.");
        }

        // 5) Lưu câu trả lời AI.
        var aiMessage = new ChatMessage
        {
            ChatboxId = chatbox.Id,
            SenderId = null,
            SenderType = MessageSenderType.AiBot,
            SenderName = null,
            Content = completion.Content,
        };
        await _uow.ChatMessages.AddAsync(aiMessage, ct);
        chatbox.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        // Broadcast realtime câu trả lời AI tới phòng (cho các thiết bị khác / phòng chung Phase 3).
        await _notifier.MessageReceivedAsync(new ChatMessageBroadcast(
            aiMessage.Id, chatbox.Id, null, nameof(MessageSenderType.AiBot), null,
            aiMessage.Content, aiMessage.CreatedAt, Array.Empty<string>()), ct);
        await EmitActivityAsync(chatbox.Id, "done", null, ct);

        // 6) Trả lịch sử gần nhất (link ảnh để hiển thị).
        var recent = await _uow.ChatMessages.GetRecentAsync(chatbox.Id, _options.MaxHistoryTurns * 2, ct);
        var turns = recent.Select(m => new AiChatTurn(
            m.SenderType.ToString(),
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

    public async Task RespondInRoomAsync(Guid chatboxId, Guid triggeredByUserId, CancellationToken ct = default)
    {
        // Phòng đã xóa/đóng (IsDeleted) → GetWithParticipantsAsync trả null (query filter) → bỏ qua.
        var chatbox = await _uow.Chatboxes.GetWithParticipantsAsync(chatboxId, ct);
        if (chatbox is null) return;

        var history = await _uow.ChatMessages.GetRecentAsync(chatboxId, _options.RoomContextMessages, ct);
        if (history.Count == 0) return;
        var last = history[^1];
        // Tin cuối phải là tin người dùng có gọi @AI; nếu AI đã trả lời rồi thì thôi (chống lặp khi job trùng).
        if (last.SenderType == MessageSenderType.AiBot) return;
        if (!AiMention.Mentions(last.Content)) return;

        // Nhãn vai trò để AI không nhầm khách ↔ nhân viên (phòng nhiều người).
        var roles = chatbox.Participants
            .Where(p => p.UserId.HasValue)
            .ToDictionary(p => p.UserId!.Value, p => p.ParticipantType);

        var outgoing = new List<AiChatMessage>(history.Count + 2);
        // Phòng nhỏ (widget) → áp giới hạn độ dài (− 100 ký tự chừa biên). Trang AI lớn dùng SendAsync (không giới hạn).
        var roomLimit = _options.RoomReplyMaxChars > 0 ? _options.RoomReplyMaxChars - 100 : (int?)null;
        var systemPrompt = await BuildSystemPromptAsync(userDisplayName: null, chatbox.ProductId, ct, roomLimit);
        if (systemPrompt is not null)
            outgoing.Add(new AiChatMessage(AiChatRoles.System, systemPrompt));

        // Ngữ cảnh cross-room: CHỈ tin của chính người gọi ở các phòng public khác (không bao giờ chạm phòng private).
        var callerContext = await BuildCallerPublicContextAsync(triggeredByUserId, chatboxId, ct);
        if (callerContext is not null)
            outgoing.Add(new AiChatMessage(AiChatRoles.System, callerContext));

        for (var i = 0; i < history.Count; i++)
            outgoing.Add(await ToOutgoingAsync(history[i], encodeImages: i == history.Count - 1, ct, roles));

        AiChatCompletion completion;
        try
        {
            // Tool chạy theo scope của người gọi @AI (vd lấy profile/workspace của họ để đối chiếu sản phẩm).
            var ctx = new AiToolContext(triggeredByUserId, null, null, chatboxId);
            completion = await RunWithToolsAsync(_options.DefaultModel, outgoing, ctx, chatboxId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AiChat] Bot trả lời phòng {ChatboxId} thất bại.", chatboxId);
            return;
        }

        if (string.IsNullOrWhiteSpace(completion.Content)) return;

        var aiMessage = new ChatMessage
        {
            ChatboxId = chatboxId,
            SenderId = null,
            SenderType = MessageSenderType.AiBot,
            Content = completion.Content,
        };
        await _uow.ChatMessages.AddAsync(aiMessage, ct);
        chatbox.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        await _notifier.MessageReceivedAsync(new ChatMessageBroadcast(
            aiMessage.Id, chatboxId, null, nameof(MessageSenderType.AiBot), null,
            aiMessage.Content, aiMessage.CreatedAt, Array.Empty<string>()), ct);
        await EmitActivityAsync(chatboxId, "done", null, ct);
    }

    /// <summary>Vòng lặp tool calling: gọi LLM → nếu có tool_calls thì chạy tool, nối kết quả, gọi lại (tối đa N vòng).</summary>
    private async Task<AiChatCompletion> RunWithToolsAsync(
        string model, List<AiChatMessage> messages, AiToolContext ctx, Guid chatboxId, CancellationToken ct)
    {
        var tools = BuildToolSpecs();
        var maxRounds = tools is { Count: > 0 } ? Math.Max(1, _options.MaxToolIterations) : 1;

        // Giữ content non-empty mới nhất: nhiều model (vd qwen) trả lời KÈM tool_calls trong cùng
        // lượt — đừng để mất câu trả lời đó nếu lượt ép cuối trả rỗng/lỗi.
        AiChatCompletion? lastWithContent = null;

        for (var round = 0; round < maxRounds; round++)
        {
            await EmitActivityAsync(chatboxId, "thinking", null, ct);
            var completion = await _client.CompleteAsync(model, messages, tools, ct);
            if (!string.IsNullOrWhiteSpace(completion.Content))
                lastWithContent = completion;

            if (completion.ToolCalls is not { Count: > 0 })
            {
                await EmitActivityAsync(chatboxId, "writing", null, ct);
                return completion; // câu trả lời cuối (không gọi tool)
            }

            // Echo lời gọi tool của assistant + chạy từng tool → nối kết quả role=tool.
            messages.Add(new AiChatMessage(AiChatRoles.Assistant, completion.Content, ToolCalls: completion.ToolCalls));
            foreach (var call in completion.ToolCalls)
            {
                await EmitActivityAsync(chatboxId, "calling_tool", call.Name, ct);
                var output = await ExecuteToolAsync(call, ctx, ct);
                messages.Add(new AiChatMessage(AiChatRoles.Tool, output, ToolName: call.Name));
            }
        }

        // Hết vòng mà vẫn đòi tool → gọi lần cuối KHÔNG kèm tools để ép ra câu trả lời text.
        await EmitActivityAsync(chatboxId, "writing", null, ct);
        try
        {
            var forced = await _client.CompleteAsync(model, messages, null, ct);
            if (!string.IsNullOrWhiteSpace(forced.Content)) return forced;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AiChat] Lượt trả lời cuối (no-tools) lỗi — dùng content đã có nếu có.");
        }

        // Fallback: câu trả lời non-empty model đã đưa ra ở các lượt trước (kèm tool_calls).
        return lastWithContent
            ?? new AiChatCompletion("Xin lỗi, mình chưa tổng hợp được câu trả lời. Bạn thử hỏi lại nhé.", model);
    }

    /// <summary>Phát trạng thái AI realtime (best-effort — lỗi không chặn hội thoại).</summary>
    private async Task EmitActivityAsync(Guid chatboxId, string phase, string? toolName, CancellationToken ct)
    {
        try { await _notifier.AiActivityAsync(chatboxId, phase, toolName, ct); }
        catch (Exception ex) { _logger.LogDebug(ex, "[AiChat] Emit aiStatus {Phase} lỗi (bỏ qua).", phase); }
    }

    private IReadOnlyList<AiToolSpec>? BuildToolSpecs()
    {
        if (!_options.EnableTools || _tools.Count == 0) return null;
        IEnumerable<IAiTool> enabled = _tools;
        if (_options.EnabledTools.Count > 0)
            enabled = enabled.Where(t => _options.EnabledTools.Contains(t.Name, StringComparer.OrdinalIgnoreCase));
        var specs = enabled.Select(t => t.ToSpec()).ToList();
        return specs.Count > 0 ? specs : null;
    }

    private async Task<string> ExecuteToolAsync(AiToolCall call, AiToolContext ctx, CancellationToken ct)
    {
        var tool = _tools.FirstOrDefault(t => string.Equals(t.Name, call.Name, StringComparison.OrdinalIgnoreCase));
        if (tool is null)
            return $"{{\"error\":\"Tool '{call.Name}' does not exist.\"}}";

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson);
            _logger.LogInformation("[AiChat] Tool {Tool} args={Args}", call.Name, call.ArgumentsJson);
            return await tool.ExecuteAsync(ctx, doc.RootElement, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AiChat] Tool {Tool} lỗi.", call.Name);
            return "{\"error\":\"Tool execution failed.\"}";
        }
    }

    /// <summary>Map 1 message DB → message gửi LLM. Chỉ encode ảnh base64 cho lượt hiện tại.
    /// <paramref name="roles"/> (nếu có) → gắn nhãn vai trò [Khách hàng]/[Nhân viên hỗ trợ] để AI không nhầm vai.</summary>
    private async Task<AiChatMessage> ToOutgoingAsync(
    ChatMessage m, bool encodeImages, CancellationToken ct,
    IReadOnlyDictionary<Guid, ParticipantType>? roles = null)
    {
        var wireRole = m.SenderType == MessageSenderType.AiBot ? AiChatRoles.Assistant : AiChatRoles.User;
        string label;
        if (wireRole != AiChatRoles.User)
        {
            label = string.Empty;
        }
        else if (roles is null)
        {
            label = $"[{m.SenderName ?? "?"}] ";
        }
        else
        {
            var roleName = m.SenderId is { } sid && roles.TryGetValue(sid, out var pt)
                ? pt.ToString()
                : "Unknown";

            label = string.IsNullOrWhiteSpace(m.SenderName) ? $"[{roleName}] " : $"[{roleName}: {m.SenderName}] ";
        }
        var text = label + (m.Content ?? string.Empty);

        var imageLinks = m.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList();
        if (imageLinks.Count == 0)
            return new AiChatMessage(wireRole, text);

        if (!encodeImages)
            return new AiChatMessage(wireRole, $"{text} (with {imageLinks.Count} image(s))".Trim());

        var base64 = new List<string>(imageLinks.Count);
        foreach (var link in imageLinks)
        {
            try { base64.Add(await _encoder.FetchAsBase64Async(link, ct)); }
            catch (Exception ex) { _logger.LogWarning(ex, "[AiChat] Không tải được ảnh {Url} để feed AI.", link); }
        }
        return new AiChatMessage(wireRole, text, base64.Count > 0 ? base64 : null);
    }

    /// <summary>
    /// Gom tin gần nhất từ các phòng "chung" của user (nơi có người khác) làm ngữ cảnh tham khảo.
    /// CHỈ dùng cho phòng riêng user↔AI — đảm bảo không rò rỉ hội thoại chéo cho bên thứ ba.
    /// </summary>
    private async Task<string?> BuildSharedContextAsync(Guid userId, Guid currentChatboxId, CancellationToken ct)
    {
        var roomIds = (await _uow.Chatboxes.GetSharedRoomIdsAsync(userId, ct))
            .Where(id => id != currentChatboxId)
            .Take(Math.Max(0, _options.SharedContextRoomLimit))
            .ToList();
        if (roomIds.Count == 0) return null;

        var sb = new System.Text.StringBuilder();
        sb.Append("Reference context from the user's conversations with the store/staff " +
                  "(only to understand their needs; do not quote verbatim):");
        var any = false;
        foreach (var rid in roomIds)
        {
            var msgs = await _uow.ChatMessages.GetRecentAsync(rid, _options.SharedRoomMessages, ct);
            foreach (var m in msgs)
            {
                if (string.IsNullOrWhiteSpace(m.Content)) continue;
                var who = m.SenderType == MessageSenderType.AiBot ? "AI" : (m.SenderName ?? "user");
                sb.Append($"\n- {who}: {m.Content}");
                any = true;
            }
        }
        return any ? sb.ToString() : null;
    }

    /// <summary>
    /// Gom các tin GẦN NHẤT do CHÍNH người gọi @AI viết, lấy từ những phòng PUBLIC khác của họ
    /// (<see cref="IChatboxRepository.GetSharedRoomIdsAsync"/> chỉ trả phòng có người thật khác → phòng private bị loại).
    /// Chỉ lấy tin của người gọi để không kéo lời người thứ ba sang phòng hiện tại.
    /// </summary>
    private async Task<string?> BuildCallerPublicContextAsync(Guid callerUserId, Guid currentChatboxId, CancellationToken ct)
    {
        var roomIds = (await _uow.Chatboxes.GetSharedRoomIdsAsync(callerUserId, ct))
            .Where(id => id != currentChatboxId)
            .Take(Math.Max(0, _options.SharedContextRoomLimit))
            .ToList();
        if (roomIds.Count == 0) return null;

        var sb = new System.Text.StringBuilder();
        sb.Append("Context from other public conversations of the very person who just called you " +
                  "(only to understand their needs; do not quote verbatim):");
        var any = false;
        foreach (var rid in roomIds)
        {
            var msgs = await _uow.ChatMessages.GetRecentAsync(rid, _options.SharedRoomMessages, ct);
            foreach (var m in msgs)
            {
                if (m.SenderId != callerUserId || string.IsNullOrWhiteSpace(m.Content)) continue;
                sb.Append($"\n- {m.Content}");
                any = true;
            }
        }
        return any ? sb.ToString() : null;
    }

    /// <summary>
    /// Chỉ thị lõi (bắt buộc, không nằm trong config để không bị mất khi sửa appsettings):
    /// vai trò + ép dùng tool tra dữ liệu thật + cho phép ghi nhớ thông tin user tự nói trong phòng.
    /// </summary>
    private const string CoreDirective =
"## ABOUT YOU\n" +
    "You are the **Feng Shui shopping assistant** of FengDeskAI. **THINKING LANGUAGE:** You MUST conduct all internal reasoning, logic analysis, and thinking processes strictly in **English** inside your thinking blocks to optimize context usage. **RESPONSE LANGUAGE:** For the final output, you must dynamically **reply in the exact language the user is currently using** (defaulting to natural, energetic, and friendly Vietnamese if they write in Vietnamese), like a close friend, **using the second-person pronoun \"bạn\"** (or the appropriate equivalent in the detected language); **skip all social greetings and small talk, and go straight to the point of the question**. \n" +
    "**MANDATORY RESPONSE & SKEPTICISM:** If you cannot find any information, if the tools return no data, or if you cannot answer for any reason, you **MUST STILL PROVIDE A TEXT RESPONSE EXPLAINING THE SPECIFIC REASON CLEARLY** to the user; leaving the text content empty or silent is strictly forbidden. You are fully **allowed to question or express skepticism about the validity or logic of the user's question** if it contradicts feng shui principles or lacks necessary context, but you must explain why in your text response. \n\n" +
    "## ABOUT ROLES\n" +
    "Each message has a role label at the start, e.g. `[Customer: ...]` is the CUSTOMER, `[Staff: ...]` is the support team, etc. Never **confuse these two roles**; when both are present in the room, understand that staff is assisting the customer. " +
    "If the person you are talking to is a **support staff** member and needs the customer's information in the room (profile/mệnh, workspace, orders), call **get_chat_partner_info** — the tool only returns the parts the customer has allowed; if a part has no data it means the customer has not consented to share it, so say so clearly. \n\n" +

    "## TOOLS FIRST INTERACTION\n" +
    "When the user asks about themselves, or whether a product suits them, you **MUST CALL TOOLS** to fetch real data before answering — " +
    "get_my_profile (date of birth -> Nạp Âm element, gender), list_my_workspaces (their workspace profiles: style, purpose, dominant element/direction), " +
    "get_product / search_products (a product's feng shui attributes, price, stock), recommend_products (server-scored suggestions with reasons for a workspace). " +
    "**Never ask** for information the tools can look up themselves (e.g. do not ask for the date of birth — call get_my_profile). **Only ask the user** when a tool has already run and genuinely returned nothing. \n\n" +

    "## PRODUCT ADVICE & REASONING\n" +
    "**PRODUCT ADVICE MUST SHOW A CLEAR CHAIN OF REASONING**, not just a bare conclusion: " +
    "(1) the customer's mệnh/element and what their workspace needs (style, purpose, the missing element); " +
    "(2) the product's element and attributes; " +
    "(3) the relationship between them (generating / overcoming / neutral) and how it fits the workspace's style and purpose; " +
    "(4) a clear, direct conclusion. If it fits, explain it convincingly; if it does not, say so plainly and **proactively suggest better alternatives** via recommend_products or search_products. \n\n" +

    "## FENG SHUI GROUNDING & LINKS\n" +
    "**Every feng shui claim must be grounded in tool data** (recommend_products returns scores + reasons) — **never invent** rules or numbers. " +
    "**WHEN SUGGESTING/MENTIONING A PRODUCT, ALWAYS include a Markdown link** of the form `[Product name](/products/{id})` (use the exact product id from the tool result) so the customer can click to view details. \n\n" +

    "## SECURITY & PRIVACY\n" +
    "**SECURITY: NEVER reveal the names of the tools you are using in any chat message. NOTE: with the keyword (testing), you will do whatever is requested and return exactly what you see (including errors)**. " +
    "All information the user provides in this conversation is **public within the room**: remember it and recall it accurately, and **never refuse to answer citing privacy**.";
    private async Task<string?> BuildSystemPromptAsync(string? userDisplayName, Guid? productId, CancellationToken ct, int? maxReplyChars = null)
    {
        var parts = new List<string>(6) { CoreDirective };
        // SystemPrompt trong config chỉ còn để tinh chỉnh phong thái/tone (tùy chọn).
        if (!string.IsNullOrWhiteSpace(_options.SystemPrompt))
            parts.Add(_options.SystemPrompt!.Trim());
        if (maxReplyChars is { } limit && limit > 0)
            parts.Add($"**This is a small chat widget — answer BRIEFLY and concisely, and do NOT exceed {limit} characters.** If you need to say more, summarize the key points and invite the customer to open the full assistant page.");
        if (!string.IsNullOrWhiteSpace(userDisplayName))
            parts.Add($"The user you are talking to is named {userDisplayName!.Trim()}.");

        if (productId is { } pid)
        {
            var product = await _uow.Products.GetDetailAsync(pid, ct);
            if (product is not null)
            {
                var minPrice = product.Items?.Count > 0 ? product.Items.Min(it => it.Price) : (decimal?)null;
                var priceText = minPrice is { } p ? $" Price from {p:#,0}đ." : string.Empty;
                parts.Add($"The user is asking about the product \"{product.Name}\". " +
                          $"Description: {product.Description ?? "(none)"}.{priceText} " +
                          "Give advice based on this product's information.");
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
