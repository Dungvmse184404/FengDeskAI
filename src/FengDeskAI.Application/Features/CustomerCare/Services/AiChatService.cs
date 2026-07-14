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
    private readonly IAiActivityNotifier _activity;
    private readonly AiChatOptions _options;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        IAiChatClient client,
        IUnitOfWork uow,
        IImageEncoder encoder,
        IEnumerable<IAiTool> tools,
        IChatRealtimeNotifier notifier,
        IAiActivityNotifier activity,
        IOptions<AiChatOptions> options,
        ILogger<AiChatService> logger)
    {
        _client = client;
        _uow = uow;
        _encoder = encoder;
        _tools = tools.ToList();
        _notifier = notifier;
        _activity = activity;
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
            if (existing is null || !IsPrivateAiRoom(existing, userId))
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
        await using var activity = _activity.Begin($"chat-{chatbox.Id}");
        try
        {
            var ctx = new AiToolContext(userId, userRole, userEmail, chatbox.Id);
            completion = await RunWithToolsAsync(model, outgoing, ctx, activity, ct);
            // Bảo hiểm deterministic: tool đã trả sản phẩm nào mà model nhắc tên nhưng quên link → BE tự chèn.
            completion = completion with { Content = LinkifyProducts(completion.Content, ctx.Products) };
            // Kiểm duyệt GUID "trần" model lỡ phun ra cho user (giữ nguyên GUID trong URL /products/...).
            completion = completion with { Content = Common.AiTextSanitizer.CensorEntityIds(completion.Content) };
        }
        catch (Exception ex)
        {
            // clientCancelled=true  -> chính request gốc từ browser (HttpContext.RequestAborted) đã bị hủy
            //                          (user đóng tab/back/mất mạng phía họ) -> KHÔNG phải lỗi hạ tầng AI.
            // clientCancelled=false -> browser vẫn đang chờ bình thường; lỗi nằm ở socket riêng
            //                          .NET <-> Ollama (OS/ngrok cắt giữa chừng) -> đáng để điều tra hạ tầng.
            _logger.LogError(ex, "[AiChat] Gọi LLM thất bại (chatbox {ChatboxId}, model {Model}, clientCancelled={ClientCancelled}).",
                chatbox.Id, model, ct.IsCancellationRequested);
            await activity.PhaseAsync("error", null, ct: ct);
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
        // "done" phát khi `activity` dispose ở cuối method (scope Begin() phía trên).

        // 6) Trả lịch sử gần nhất (link ảnh để hiển thị).
        var recent = await _uow.ChatMessages.GetRecentAsync(chatbox.Id, _options.MaxHistoryTurns * 2, ct);
        var turns = recent.Select(m => new AiChatTurn(
            m.Id,
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

    public async Task<IServiceResult<AiChatResponse>> RewindAsync(
        Guid userId, string? userRole, string? userEmail, string? userDisplayName,
        Guid messageId, AiRewindRequest request, CancellationToken ct = default)
    {
        var message = await _uow.ChatMessages.GetByIdWithImagesAsync(messageId, ct);
        if (message is null)
            return ServiceResult<AiChatResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy tin nhắn.");
        // Không lộ tồn tại của tin nhắn người khác — coi như không tìm thấy, không phải 403.
        if (message.SenderId != userId)
            return ServiceResult<AiChatResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy tin nhắn.");
        if (message.SenderType != MessageSenderType.User)
            return ServiceResult<AiChatResponse>.Failure(ApiStatusCodes.BadRequest, "Chỉ rewind được tin nhắn của bạn.");

        var chatbox = await _uow.Chatboxes.GetWithParticipantsAsync(message.ChatboxId, ct);
        if (chatbox is null || !IsPrivateAiRoom(chatbox, userId))
            return ServiceResult<AiChatResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy hội thoại AI của bạn.");

        var content = request.NewMessage ?? message.Content;
        var images = request.ImageUrls ?? message.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList();

        // Cắt lịch sử TRƯỚC khi gọi LLM — nếu SendAsync lỗi thì lịch sử đã cắt nhưng tin mới chưa gửi,
        // user thấy hội thoại dừng ở điểm rewind và có thể gửi lại (chấp nhận).
        await _uow.ChatMessages.SoftDeleteFromAsync(message.ChatboxId, message.CreatedAt, message.Id, ct);

        return await SendAsync(userId, userRole, userEmail, userDisplayName, new AiChatRequest
        {
            ChatboxId = message.ChatboxId,
            Message = content,
            ImageUrls = images,
            Model = request.Model,
        }, ct);
    }

    public IServiceResult<AiChatConfigResponse> GetConfig()
        => ServiceResult<AiChatConfigResponse>.Success(
            new AiChatConfigResponse(_options.MaxHistoryTurns, _options.MaxHistoryTurns * 2));

    /// <summary>Phòng riêng user↔AI: có AiBot, có đúng user này, không có user khác nào tham gia.</summary>
    private static bool IsPrivateAiRoom(Chatbox chatbox, Guid userId) =>
        chatbox.Participants.Any(p => p.ParticipantType == ParticipantType.AiBot)
        && chatbox.Participants.Any(p => p.UserId == userId)
        && !chatbox.Participants.Any(p => p.UserId != null && p.UserId != userId);

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
        await using var activity = _activity.Begin($"chat-{chatboxId}");
        try
        {
            // Tool chạy theo scope của người gọi @AI (vd lấy profile/workspace của họ để đối chiếu sản phẩm).
            // Phòng nhiều người → IsPrivateRoom=false: loại các tool có tác dụng phụ (đặt hàng) khỏi BuildToolSpecs.
            var ctx = new AiToolContext(triggeredByUserId, null, null, chatboxId, IsPrivateRoom: false);
            completion = await RunWithToolsAsync(_options.DefaultModel, outgoing, ctx, activity, ct);
            completion = completion with { Content = LinkifyProducts(completion.Content, ctx.Products) };
            // Kiểm duyệt GUID "trần" model lỡ phun ra cho user (giữ nguyên GUID trong URL /products/...).
            completion = completion with { Content = Common.AiTextSanitizer.CensorEntityIds(completion.Content) };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AiChat] Bot trả lời phòng {ChatboxId} thất bại.", chatboxId);
            await activity.PhaseAsync("error", null, ct: ct);
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
        // "done" phát khi `activity` dispose ở cuối method (scope Begin() phía trên).
    }

    /// <summary>Vòng lặp tool calling: gọi LLM → nếu có tool_calls thì chạy tool, nối kết quả, gọi lại (tối đa N vòng).</summary>
    private async Task<AiChatCompletion> RunWithToolsAsync(
        string model, List<AiChatMessage> messages, AiToolContext ctx, AiActivityScope activity, CancellationToken ct)
    {
        var tools = BuildToolSpecs(ctx.IsPrivateRoom);
        var maxRounds = tools is { Count: > 0 } ? Math.Max(1, _options.MaxToolIterations) : 1;

        // Giữ content non-empty mới nhất: nhiều model (vd qwen) trả lời KÈM tool_calls trong cùng
        // lượt — đừng để mất câu trả lời đó nếu lượt ép cuối trả rỗng/lỗi.
        AiChatCompletion? lastWithContent = null;

        // Temperature + think theo cấu hình riêng của chatbox (Ai:Chat) — null = mặc định model.
        var callOptions = new AiCompletionOptions(
            Temperature: _options.Temperature, Think: _options.Think, Stream: _options.Stream);

        // Model nhỏ hay "hứa" đi lấy dữ liệu bằng TEXT ("Đang lấy dữ liệu...") mà không emit tool_calls
        // rồi dừng hẳn → user nhận câu cụt. Phát hiện stall → nhắc lại buộc gọi tool thật (tối đa N lần).
        var nudgesLeft = MaxStallNudges;

        // Câu stall đã nuốt — giữ làm phao cuối: thà trả câu "hứa hẹn" còn hơn im lặng nếu lượt sau lỗi/treo.
        AiChatCompletion? stalledCandidate = null;

        for (var round = 0; round < maxRounds; round++)
        {
            await activity.PhaseAsync("thinking", null, ct: ct);

            AiChatCompletion completion;
            try
            {
                completion = await _client.CompleteAsync(
                    model, messages, tools, options: callOptions, onDelta: activity.ThinkingProgress(), ct: ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException
                && (lastWithContent ?? stalledCandidate) is { } salvage)
            {
                // LLM lỗi/timeout giữa chuỗi nhưng đã có câu trả lời khả dụng → cứu nó thay vì ném lỗi trắng tay.
                _logger.LogWarning(ex, "[AiChat] LLM lỗi ở vòng {Round} — dùng câu trả lời đã có thay vì fail cả lượt.", round);
                await activity.PhaseAsync("writing", null, ct: ct);
                return salvage;
            }

            var hasToolCalls = completion.ToolCalls is { Count: > 0 };

            if (!hasToolCalls && tools is { Count: > 0 } && nudgesLeft > 0 && LooksLikeToolStall(completion.Content))
            {
                nudgesLeft--;
                _logger.LogInformation("[AiChat] Model hứa gọi tool nhưng không emit tool_calls — nhắc lại (còn {Left} lần).", nudgesLeft);
                if (!string.IsNullOrWhiteSpace(completion.Content))
                    stalledCandidate = completion;
                messages.Add(new AiChatMessage(AiChatRoles.Assistant, completion.Content));
                messages.Add(new AiChatMessage(AiChatRoles.System,
                    "You announced you would fetch data but did NOT emit any tool call — the user received nothing. " +
                    "Act NOW in this turn: emit the required tool call immediately, or if no tool is needed, " +
                    "give the complete final answer. Never announce or promise an action again."));
                continue;
            }

            // Không ghi nhận câu stall làm fallback — thà xin lỗi còn hơn trả "Đang lấy dữ liệu..." cụt lủn.
            if (!string.IsNullOrWhiteSpace(completion.Content) && !LooksLikeToolStall(completion.Content))
                lastWithContent = completion;

            if (!hasToolCalls)
            {
                await activity.PhaseAsync("writing", null, ct: ct);
                return completion; // câu trả lời cuối (không gọi tool)
            }

            // Lời dẫn model viết kèm tool_calls: KHÔNG lưu DB (context gọn — chỉ echo cho LLM trong lượt),
            // nhưng phát realtime dạng "thinking" để user đỡ tưởng AI treo khi tool chạy lâu.
            if (!string.IsNullOrWhiteSpace(completion.Content))
                await activity.NarrateAsync(StripEmoji(completion.Content), ct);

            // Echo lời gọi tool của assistant + chạy từng tool → nối kết quả role=tool.
            messages.Add(new AiChatMessage(AiChatRoles.Assistant, completion.Content, ToolCalls: completion.ToolCalls));
            foreach (var call in completion.ToolCalls!)
            {
                await activity.PhaseAsync("calling_tool", call.Name, ToolFriendlyNote(call.Name), ct);
                var output = await ExecuteToolAsync(call, ctx, ct);
                messages.Add(new AiChatMessage(AiChatRoles.Tool, output, ToolName: call.Name));
            }
        }

        // Hết vòng mà vẫn đòi tool → gọi lần cuối KHÔNG kèm tools để ép ra câu trả lời text.
        await activity.PhaseAsync("writing", null, ct: ct);
        try
        {
            var forced = await _client.CompleteAsync(
                model, messages, null, options: callOptions, onDelta: activity.ThinkingProgress(), ct: ct);
            if (!string.IsNullOrWhiteSpace(forced.Content)) return forced;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AiChat] Lượt trả lời cuối (no-tools) lỗi — dùng content đã có nếu có.");
        }

        // Fallback: câu non-empty ở các lượt trước → câu stall đã nuốt → cuối cùng mới xin lỗi.
        return lastWithContent
            ?? stalledCandidate
            ?? new AiChatCompletion("Xin lỗi, mình chưa tổng hợp được câu trả lời. Bạn thử hỏi lại nhé.", model);
    }

    /// <summary>
    /// Chèn link markdown "[Tên](/products/{id})" cho các sản phẩm tool đã trả trong lượt này,
    /// nếu model nhắc tên sản phẩm mà quên hyperlink. Deterministic — không phụ thuộc model nhớ quy tắc.
    /// </summary>
    private static string LinkifyProducts(string content, IReadOnlyList<AiProductRef> products)
    {
        if (string.IsNullOrEmpty(content) || products.Count == 0) return content;

        foreach (var p in products.DistinctBy(x => x.Id))
        {
            if (string.IsNullOrWhiteSpace(p.Name)) continue;
            var url = $"/products/{p.Id}";
            // Model đã tự link sản phẩm này rồi → bỏ qua.
            if (content.Contains(url, StringComparison.OrdinalIgnoreCase)) continue;

            var idx = content.IndexOf(p.Name, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            // Tên đang nằm trong "[...]" của một link khác → bỏ qua cho an toàn.
            if (idx > 0 && content[idx - 1] == '[') continue;

            var matched = content.Substring(idx, p.Name.Length); // giữ nguyên hoa/thường model đã viết
            content = content.Remove(idx, p.Name.Length).Insert(idx, $"[{matched}]({url})");
        }
        return content;
    }


    /// <summary>Số lần nhắc model khi nó "hứa" gọi tool bằng text mà không emit tool_calls.</summary>
    private const int MaxStallNudges = 2;

    /// <summary>Cụm từ "hứa hẹn" đặc trưng — model nói sẽ đi lấy dữ liệu rồi dừng, không có tool call.</summary>
    private static readonly string[] StallMarkers =
    {
        "đang lấy dữ liệu", "đang truy", "đang chạy", "đang gọi", "đang kiểm tra", "đang tra",
        "chờ mình", "chờ chút", "chờ xíu", "vài giây", "giây lát", "chút nhé",
        "fetching", "retrieving", "one moment", "let me check", "let me fetch", "calling the tool",
    };

    /// <summary>
    /// Content KHÔNG kèm tool_calls nhưng lộ dấu hiệu "sắp đi lấy dữ liệu": nhắc tên tool literal
    /// (vi phạm luôn quy tắc bảo mật tên tool) hoặc chứa cụm hứa hẹn → cần nhắc model gọi tool thật.
    /// </summary>
    /// <summary>Lọc emoji/icon khỏi lời dẫn trung gian (spec UI: thinking block chữ mờ, không icon).
    /// Gồm: surrogate pairs (emoji ngoài BMP) + dingbats/misc symbols + variation selector + ZWJ.</summary>
    private static readonly System.Text.RegularExpressions.Regex EmojiRegex = new(
        @"[\uD800-\uDBFF][\uDC00-\uDFFF]|[←-⇿⌀-➿⬀-⯿️‍]",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string StripEmoji(string text) => EmojiRegex.Replace(text, string.Empty).Trim();

    /// <summary>Câu dài hơn mức này là trả lời thật (có cấu trúc), không phải stall — đừng nuốt.</summary>
    private const int StallMaxLength = 500;

    private bool LooksLikeToolStall(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return true;
        // Câu trả lời dài, có nội dung → không phải "hứa suông" kể cả khi lỡ nhắc tên tool
        // (vi phạm rule giấu tên tool là chuyện khác — không đáng để nuốt mất câu trả lời).
        if (content.Length > StallMaxLength) return false;
        var lower = content.ToLowerInvariant();
        if (_tools.Any(t => lower.Contains(t.Name.ToLowerInvariant()))) return true;
        return StallMarkers.Any(m => lower.Contains(m));
    }

    /// <summary>Tool có tác dụng phụ (tạo đơn) — chỉ được đưa vào danh sách tool cho LLM / thực thi ở phòng riêng.</summary>
    private static readonly HashSet<string> PrivateRoomOnlyTools =
        new(StringComparer.OrdinalIgnoreCase) { "prepare_order", "confirm_order" };

    /// <summary>
    /// Nhãn tiếng Việt thân thiện hiển thị cho user khi AI đang gọi 1 tool (phase="calling_tool"),
    /// thay vì lộ tên tool thô (vd "prepare_order") — xem AiActivityIndicator.tsx phía FE.
    /// Tool không có trong map (vd tool mới thêm quên cập nhật) → FE tự fallback về text mặc định.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ToolFriendlyNotes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["search_products"] = "Đang tìm sản phẩm phù hợp",
            ["get_product"] = "Đang xem chi tiết sản phẩm",
            ["recommend_products"] = "Đang tư vấn sản phẩm theo phong thủy",
            ["list_my_workspaces"] = "Đang lấy hồ sơ không gian của bạn",
            ["get_my_profile"] = "Đang lấy thông tin tài khoản của bạn",
            ["list_my_orders"] = "Đang lấy danh sách đơn hàng của bạn",
            ["get_payment_status"] = "Đang kiểm tra trạng thái thanh toán",
            ["get_chat_partner_info"] = "Đang lấy thông tin khách hàng",
            ["list_my_addresses"] = "Đang lấy danh sách địa chỉ của bạn",
            ["prepare_order"] = "Đang chuẩn bị đơn hàng của bạn",
            ["confirm_order"] = "Đang xác nhận và tạo đơn hàng",
        };

    private static string? ToolFriendlyNote(string toolName)
        => ToolFriendlyNotes.TryGetValue(toolName, out var note) ? note : null;

    private IReadOnlyList<AiToolSpec>? BuildToolSpecs(bool isPrivateRoom)
    {
        if (!_options.EnableTools || _tools.Count == 0) return null;
        IEnumerable<IAiTool> enabled = _tools;
        if (_options.EnabledTools.Count > 0)
            enabled = enabled.Where(t => _options.EnabledTools.Contains(t.Name, StringComparer.OrdinalIgnoreCase));
        if (!isPrivateRoom)
            enabled = enabled.Where(t => !PrivateRoomOnlyTools.Contains(t.Name));
        var specs = enabled.Select(t => t.ToSpec()).ToList();
        return specs.Count > 0 ? specs : null;
    }

    private async Task<string> ExecuteToolAsync(AiToolCall call, AiToolContext ctx, CancellationToken ct)
    {
        var tool = _tools.FirstOrDefault(t => string.Equals(t.Name, call.Name, StringComparison.OrdinalIgnoreCase));
        if (tool is null)
            return $"{{\"error\":\"Tool '{call.Name}' does not exist.\"}}";
        // Chặn lần 2: dù BuildToolSpecs đã loại tool này khỏi danh sách gửi LLM, model vẫn có thể "bịa"
        // tool_call (prompt injection ở phòng chung) — không thực thi bất kể nó có emit hay không.
        if (!ctx.IsPrivateRoom && PrivateRoomOnlyTools.Contains(tool.Name))
            return "{\"error\":\"This tool is only available in a private conversation with the assistant.\"}";

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
        private const string CoreDirective = "## ABOUT YOU\n" +
            "You are the **Feng Shui shopping assistant** of FengDeskAI. Your sole mission is to serve the customer to the maximum extent with absolute efficiency. \n\n" +

            "## LANGUAGE PROTOCOLS\n" +
            "- **THINKING LANGUAGE:** You MUST conduct all internal reasoning, logic analysis, and thinking processes strictly in **English** inside your thinking blocks.\n" +
            "- **RESPONSE LANGUAGE:** Dynamically reply in the exact language the user is currently using (default to natural, energetic, friendly Vietnamese using \"bạn\"). Skip all greetings and small talk; go straight to the point.\n\n" +

            "## FUNCTION CALLING PROTOCOL\n" +
            "- **STRICT EXECUTION:** When the user asks about themselves, their profile, workspaces, or product suitability, you **MUST IMMEDIATELY trigger the appropriate tool call**.\n" +
            "- **NO TEXT BEFORE TOOL:** When triggering a tool, you **MUST NOT output any introductory text or announcements** (e.g., \"Đang chạy tool...\") in the final response. The tool call structure must be the very first output emitted outside the thinking block.\n" +
            "- **NEVER END WITH A PROMISE:** Never finish your turn by saying you are \"about to\" fetch/check something. Either EMIT the tool call in this very turn, or give the complete final answer.\n" +
            "- **EMPTY DATA FALLBACK:** If tools return empty data or errors, you **MUST STILL PROVIDE A CLEAR TEXT RESPONSE EXPLAINING THE SPECIFIC REASON** to the user. You are fully allowed to express skepticism or ask for clarification if the input contradicts feng shui principles.\n\n" +

            "## ABOUT ROLES & WORKFLOWS\n" +
            "- Message tags like `[Customer: ...]` and `[Staff: ...]` distinguish roles. Never confuse them.\n" +
            "- If the speaker is a **support staff** member requesting customer data, call `get_chat_partner_info`. If a field has no data, explicitly state that the customer has not consented to share it.\n" +
            "- If this room is linked to a specific shop (a customer chatting with a store), call `get_shop_info` when the customer asks about the shop itself (join date, rating, what it sells) instead of guessing.\n" +
            "- **Never ask** the user for data that tools can fetch (e.g., do not ask for date of birth; call `get_my_profile` instead). Only ask when a tool has already run and returned nothing.\n\n" +

            "## PRODUCT ADVICE & REASONING\n" +
            "- **PRODUCT ADVICE MUST SHOW A CLEAR CHAIN OF REASONING**: (1) Customer's mệnh/element and workspace needs; (2) Product's element and attributes; (3) Relationship (generating/overcoming/neutral) and alignment with workspace style/purpose; (4) Clear conclusion. Proactively suggest alternatives if it does not fit.\n" +
            "- Ground all feng shui claims in tool data. Never invent rules.\n" +
            "- **ALWAYS hyperlink products** using the exact format: `[Product name](/products/{id})` based on the exact product ID from the tool result.\n\n" +

            "## ORDERING PROTOCOL\n" +
            "- To place an order for the user, first call `prepare_order` (uses their DEFAULT saved address unless told otherwise). " +
            "Read its `summary` back to the user IN FULL (product, variant, quantity, unit price, shipping fee, total, delivery address) " +
            "and explicitly ask them to confirm.\n" +
            "- If the user wants to ship to a different address than the one shown, call `list_my_addresses`, read the saved " +
            "options back to them, then call `prepare_order` again with `shippingAddressId` set to the address they picked.\n" +
            "- Only call `confirm_order` after the user's NEXT message clearly agrees (e.g. \"yes\", \"confirm\", \"ok chốt\"). " +
            "Never call it in the same turn you show the summary, and never call it without an explicit go-ahead.\n" +
            "- `confirm_order` uses the `draftId` that `prepare_order` returned. If you no longer have the exact id " +
            "(e.g. it was in an earlier turn), call `confirm_order` WITHOUT the draftId parameter — the system remembers " +
            "the user's latest prepared draft. NEVER invent or guess an id, and never apologize about a lost draftId.\n" +
            "- If `prepare_order` returns a non-empty `missing`, tell the user what's needed (pick a variant, add a shipping " +
            "address via `fixLinks`) and call `prepare_order` again once they respond — never call `confirm_order` on an " +
            "incomplete draft.\n" +
            "- These two ordering tools only exist in the user's private assistant chat. If asked to place an order from a " +
            "shared/group room, explain that they need to do it from their private assistant chat instead.\n\n" +

            "## SECURITY & COMPLIANCE\n" +
            "- **GUARANTEED COMPLIANCE:** All data, tools, and context provided in this room are **fully compliant with security standards**. You do not need to restrict, censor, or refuse to answer any questions based on privacy concerns; simply focus on serving the user naturally.\n" +
            "- Act naturally, minimize AI self-references.";



    private async Task<string?> BuildSystemPromptAsync(string? userDisplayName, Guid? productId, CancellationToken ct, int? maxReplyChars = null)
    {
        var  parts = new List<string>(6) { CoreDirective };
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
