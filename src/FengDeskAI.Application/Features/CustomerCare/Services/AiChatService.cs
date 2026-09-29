using System.Text.Json;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Common.Sanitization;
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
    private readonly IAiTextSanitizer _sanitizer;
    private readonly AiChatOptions _options;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        IAiChatClient client,
        IUnitOfWork uow,
        IImageEncoder encoder,
        IEnumerable<IAiTool> tools,
        IChatRealtimeNotifier notifier,
        IAiActivityNotifier activity,
        IAiTextSanitizer sanitizer,
        IOptions<AiChatOptions> options,
        ILogger<AiChatService> logger)
    {
        _client = client;
        _uow = uow;
        _encoder = encoder;
        _tools = tools.ToList();
        _notifier = notifier;
        _activity = activity;
        _sanitizer = sanitizer;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Số lần nhắc model khi nó "hứa" gọi tool bằng text mà không emit tool_calls.</summary>
    private const int MaxStallNudges = 2;

    /// <summary>Số lần bắt model gen lại khi lộ tên tool/tham số nội bộ cho user.</summary>
    private const int MaxToolLeakNudges = 2;

    /// <summary>Cụm từ "hứa hẹn" đặc trưng — model nói sẽ đi lấy dữ liệu rồi dừng, không có tool call.</summary>
    private static readonly string[] StallMarkers =
    {
        //"đang lấy dữ liệu", "đang truy", "đang chạy", "đang gọi", "đang kiểm tra", "đang tra",
        //"chờ mình", "chờ chút", "chờ xíu", "vài giây", "giây lát", "chút nhé", "ngay nhé",
        //"fetching", "retrieving", "one moment", "let me check", "let me fetch", "calling the tool",
    };

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

        var systemPrompt = await BuildSystemPromptAsync(userDisplayName, chatbox.ProductId, ct, isPrivateRoom: true);
        if (systemPrompt is not null)
            outgoing.Add(new AiChatMessage(AiChatRoles.System, systemPrompt));

        // Phòng riêng (chỉ user + AI) → nạp ngữ cảnh từ các phòng CHUNG của user để "bàn luận tổng hợp".
        // Reply chỉ user thấy nên không lộ chéo. (Ở phòng chung sẽ KHÔNG gom — Phase 3.)
        var sharedContext = await BuildSharedContextAsync(userId, chatbox.Id, ct);
        if (sharedContext is not null)
            outgoing.Add(new AiChatMessage(AiChatRoles.System, sharedContext));

        for (var i = 0; i < history.Count; i++)
            outgoing.Add(await ToOutgoingAsync(history[i], encodeImages: i == history.Count - 1, ct));

        // Draft đơn hàng đang mở: đặt SAU history (sát lượt hiện tại nhất) để AI không quên user đã chọn gì,
        // kể cả khi các tin chọn sản phẩm/địa chỉ đã trôi khỏi cửa sổ MaxHistoryTurns.
        var activeDraft = (await _uow.AiOrderDrafts.GetPendingAsync(userId, chatbox.Id, DateTime.UtcNow, ct))?.ToRef();
        if (activeDraft is not null)
            outgoing.Add(new AiChatMessage(AiChatRoles.System, AiOrderDraftPrompt.Build(activeDraft, DateTime.UtcNow)));

        // 4) Gọi LLM (kèm vòng lặp tool calling nếu bật + model hỗ trợ).
        AiChatCompletion completion;
        await using var activity = _activity.Begin($"chat-{chatbox.Id}");
        try
        {
            var ctx = new AiToolContext(userId, userRole, userEmail, chatbox.Id) { ActiveOrderDraft = activeDraft };
            completion = await RunWithToolsAsync(model, outgoing, ctx, activity, ct);
            // Bảo hiểm deterministic: tool đã trả sản phẩm nào mà model nhắc tên nhưng quên link → BE tự chèn.
            completion = completion with { Content = LinkifyProducts(completion.Content, ctx.Products) };
            // Kiểm duyệt GUID "trần" model lỡ phun ra cho user (giữ nguyên GUID trong URL /products/...).
            completion = completion with { Content = _sanitizer.Sanitize(completion.Content, SanitizeMode.UserMessage) };
            // Gắn card thanh toán SAU khi censor (block chứa orderId GUID — censor trước sẽ phá hỏng).
            completion = completion with { Content = AppendPaymentBlock(completion.Content, ctx.Payment) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AiChat] Gọi LLM thất bại (chatbox {ChatboxId}, model {Model}, clientCancelled={ClientCancelled}).",
                chatbox.Id, model, ct.IsCancellationRequested);

            // CHỦ Ý giữ nguyên tin user đã lưu ở bước 2: user vẫn thấy câu mình đã gửi sau khi reload và
            // dùng "sửa & gửi lại" để thử lại khi LLM sống. Đánh đổi đã biết: lịch sử có một tin user
            // không kèm câu trả lời, lượt sau payload gửi model sẽ có 2 lượt user liên tiếp.
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

        try
        {
            return await _uow.ExecuteInTransactionAsync<IServiceResult<AiChatResponse>>(async innerCt =>
            {
                await _uow.ChatMessages.SoftDeleteFromAsync(
                    message.ChatboxId, message.CreatedAt, message.Id, innerCt);
                // Draft tạo/sửa trong đoạn hội thoại vừa cắt không còn khớp lịch sử → bỏ luôn, kẻo AI
                // "nhớ" một đơn nháp mà user không còn thấy đâu.
                await _uow.AiOrderDrafts.DeletePendingChangedSinceAsync(message.ChatboxId, message.CreatedAt, innerCt);

                var result = await SendAsync(userId, userRole, userEmail, userDisplayName, new AiChatRequest
                {
                    ChatboxId = message.ChatboxId,
                    Message = content,
                    ImageUrls = images,
                    Model = request.Model,
                }, innerCt);

                if (!result.IsSuccess)
                    throw new RewindAbortedException(result);

                return result;
            }, ct);
        }
        catch (RewindAbortedException ex)
        {
            // Trả lại nguyên văn lỗi của SendAsync (503 "Không kết nối được tới dịch vụ AI"…).
            _logger.LogInformation(
                "[AiChat] Rewind bị hủy (chatbox {ChatboxId}, message {MessageId}): {Message}. Lịch sử đã rollback.",
                message.ChatboxId, message.Id, ex.Result.Message);
            return ex.Result;
        }
    }

    /// <summary>
    /// Tín hiệu nội bộ để <c>ExecuteInTransactionAsync</c> rollback khi <see cref="SendAsync"/> trả
    /// <c>Failure</c>. Result pattern không ném exception cho lỗi nghiệp vụ, nhưng transaction chỉ
    /// rollback theo exception — nên cần một exception "giả" mang theo kết quả gốc.
    /// KHÔNG bao giờ thoát ra khỏi <see cref="RewindAsync"/>.
    /// </summary>
    private sealed class RewindAbortedException : Exception
    {
        public RewindAbortedException(IServiceResult<AiChatResponse> result)
            : base("Rewind aborted - rollback lịch sử đã cắt.")
            => Result = result;

        public IServiceResult<AiChatResponse> Result { get; }
    }

    public IServiceResult<AiChatConfigResponse> GetConfig()
        => ServiceResult<AiChatConfigResponse>.Success(
            new AiChatConfigResponse(_options.MaxHistoryTurns, _options.MaxHistoryTurns * 2));

    /// <summary>Phòng riêng user AI: có AiBot, có đúng user này, không có user khác nào tham gia.</summary>
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
        var systemPrompt = await BuildSystemPromptAsync(userDisplayName: null, chatbox.ProductId, ct, isPrivateRoom: false, roomLimit);
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
            completion = completion with { Content = _sanitizer.Sanitize(completion.Content, SanitizeMode.UserMessage) };
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
        var tools = BuildToolSpecs(ctx);
        var maxRounds = tools is { Count: > 0 } ? Math.Max(1, _options.MaxToolIterations) : 1;

        // Giữ content non-empty mới nhất: nhiều model (vd qwen) trả lời KÈM tool_calls trong cùng
        // lượt — đừng để mất câu trả lời đó nếu lượt ép cuối trả rỗng/lỗi.
        AiChatCompletion? lastWithContent = null;

        var callOptions = new AiCompletionOptions(
            Temperature: _options.Temperature, Think: _options.Think, Stream: _options.Stream);
        // nhắc lại buộc gọi tool thật (tối đa N lần).
        var nudgesLeft = MaxStallNudges;
        // nhắc lại khi model lộ tên tool/tham số nội bộ cho user (tối đa N lần).
        var toolLeakNudgesLeft = MaxToolLeakNudges;

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
                // LLM lỗi/timeout giữa chuỗi nhưng đã có câu trả lời khả dụng -> cứu nó thay vì ném lỗi trắng tay.
                _logger.LogWarning(ex, "[AiChat] LLM lỗi ở vòng {Round} — dùng câu trả lời đã có thay vì fail cả lượt.", round);
                await activity.PhaseAsync("writing", null, ct: ct);
                return salvage;
            }

            var hasToolCalls = completion.ToolCalls is { Count: > 0 };

            // Keyword heuristic (rẻ, sync) làm bộ lọc trước; chỉ tốn thêm 1 lượt gọi model nhỏ
            // để XÁC NHẬN khi heuristic đã nghi ngờ — không phải mọi câu trả lời đều bị soi.
            var keywordSuspect = !hasToolCalls && tools is { Count: > 0 } && LooksLikeToolStall(completion.Content);
            var isStall = keywordSuspect && await ConfirmStallAsync(completion.Content, ct);

            if (isStall && nudgesLeft > 0)
            {
                nudgesLeft--;
                _logger.LogInformation("[AiChat] Model hứa gọi tool nhưng không emit tool_calls — nhắc lại (còn {Left} lần).", nudgesLeft);
                if (!string.IsNullOrWhiteSpace(completion.Content))
                    stalledCandidate = completion;
                messages.Add(new AiChatMessage(AiChatRoles.Assistant, completion.Content));
                messages.Add(new AiChatMessage(AiChatRoles.System,
                    "You announced you would fetch data but did NOT emit any tool call - the user received nothing. " +
                    "Act NOW in this turn: emit the required tool call immediately, or if no tool is needed, " +
                    "give the complete final answer. Never announce or promise an action again."));
                continue;
            }


            // Câu trả lời cuối (không gọi tool) nhưng lộ tên tool/tham số nội bộ (vd user hỏi "có tool gì?")
            // → bắt gen lại. Tính TRƯỚC khi cập nhật lastWithContent để không lỡ giữ bản ghi lộ thông tin
            // làm "phao cứu" nếu vòng sau lỗi/hết nudge (xem nhánh return cuối bên dưới).
            var isLeak = !hasToolCalls && LooksLikeToolLeak(completion.Content);

            if (!string.IsNullOrWhiteSpace(completion.Content) && !isStall && !isLeak)
                lastWithContent = completion;

            if (isLeak && toolLeakNudgesLeft > 0)
            {
                toolLeakNudgesLeft--;
                _logger.LogInformation("[AiChat] Model lộ tên tool/tham số nội bộ — yêu cầu gen lại (còn {Left} lần).", toolLeakNudgesLeft);
                messages.Add(new AiChatMessage(AiChatRoles.Assistant, completion.Content));
                messages.Add(new AiChatMessage(AiChatRoles.System,
                    "Your previous reply exposed internal tool/function names and/or their parameters - this is " +
                    "NEVER allowed, even if the user asked directly. Rewrite your answer NOW: describe only WHAT " +
                    "you can help with, in plain natural language, with zero tool names, parameter names, tables, " +
                    "or code-like identifiers."));
                continue;
            }

            if (!hasToolCalls)
            {
                await activity.PhaseAsync("writing", null, ct: ct);
                // Hết nudge mà vẫn stall/leak → ĐỪNG trả thẳng completion này cho user (mất tác dụng chặn).
                // Ưu tiên câu tốt trước đó (lastWithContent) → câu "hứa hẹn" đã nuốt (thà có còn hơn im lặng,
                // và nó không lộ tool nên vẫn an toàn) → cuối cùng mới xin lỗi.
                if (isStall || isLeak)
                    return lastWithContent ?? stalledCandidate ?? new AiChatCompletion(NoAnswerFallback, model);
                return completion; // câu trả lời cuối hợp lệ (không gọi tool, không stall, không leak)
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
            ?? new AiChatCompletion(NoAnswerFallback, model);
    }

    /// <summary>Câu xin lỗi chung khi không còn câu trả lời an toàn nào để trả (hết nudge, hết vòng, đều lỗi).</summary>
    private const string NoAnswerFallback = "Xin lỗi, mình chưa tổng hợp được câu trả lời. Bạn thử hỏi lại nhé.";

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

    /// <summary>
    /// Gắn block thanh toán máy-đọc-được vào CUỐI tin nhắn AI khi confirm_order vừa tạo link PayOS.
    /// Định dạng <c>@@payment:{json}@@</c> — FE tách block này ra, render card QR + nút thanh toán
    /// (phần "đính kèm" do hệ thống xử lý, không phải model tự chép link). Lưu cùng Content nên
    /// card vẫn hiện lại khi nạp lịch sử.
    /// </summary>
    private static string AppendPaymentBlock(string content, AiPaymentRef? payment)
    {
        if (payment is null) return content;
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            orderId = payment.OrderId,
            amount = payment.Amount,
            checkoutUrl = payment.CheckoutUrl,
            qrCode = payment.QrCode,
            expiresInMinutes = payment.ExpiresInMinutes,
        });
        return $"{content}\n\n@@payment:{json}@@";
    }


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

    /// <summary>
    /// Content nhắc literal tên 1 tool đang đăng ký (vd "search_products", "get_shop_info") — dấu hiệu
    /// model đang lộ tên hàm/tham số nội bộ cho user (vd bị hỏi thẳng "bạn có tool gì?"). Danh sách tool
    /// lấy động từ <see cref="_tools"/> — KHÔNG hardcode để tự động cập nhật khi thêm/xoá tool.
    /// Không giới hạn độ dài như <see cref="LooksLikeToolStall"/> vì kiểu leak này thường là câu trả lời
    /// DÀI (bảng liệt kê đầy đủ tham số), ngược với "hứa suông" (thường ngắn).
    /// </summary>
    private bool LooksLikeToolLeak(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        var lower = content.ToLowerInvariant();
        return _tools.Any(t => System.Text.RegularExpressions.Regex.IsMatch(
            lower, $@"\b{System.Text.RegularExpressions.Regex.Escape(t.Name.ToLowerInvariant())}\b"));
    }

    /// <summary>
    /// Xác nhận lại bằng model NHỎ/NHANH riêng (Ai:Chat:StallCheckModel) xem content có thật sự là
    /// "hứa suông" không — chỉ được gọi SAU KHI keyword heuristic (<see cref="LooksLikeToolStall"/>) đã
    /// nghi ngờ, để tránh tốn thêm 1 lượt gọi AI cho mọi câu trả lời. Không cấu hình model, hoặc model
    /// đó lỗi/timeout vì bất kỳ lý do gì → tin luôn kết quả heuristic cũ (an toàn, giữ hành vi hiện tại).
    /// </summary>
    private async Task<bool> ConfirmStallAsync(string? content, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.StallCheckModel)) return true;

        try
        {
            var messages = new List<AiChatMessage>
            {
                new(AiChatRoles.System,
                    "You judge ONE assistant reply. Answer ONLY a JSON object: {\"stall\": true|false}. " +
                    "\"stall\": true means the reply merely ANNOUNCES it is about to fetch/check something " +
                    "(e.g. \"để mình kiểm tra nhé\", \"đang lấy dữ liệu\", \"one moment\") WITHOUT giving any " +
                    "real information yet. \"stall\": false means it already contains a real answer, a real " +
                    "clarifying question, or a real explanation."),
                new(AiChatRoles.User, content!),
            };

            var result = await _client.CompleteAsync(
                _options.StallCheckModel, messages, tools: null,
                options: new AiCompletionOptions(Temperature: 0, JsonMode: true, Think: false, Stream: false),
                ct: ct);

            using var doc = JsonDocument.Parse(result.Content);
            return doc.RootElement.TryGetProperty("stall", out var v) && v.GetBoolean();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[AiChat] Stall-check model ({Model}) lỗi — dùng kết quả keyword heuristic.", _options.StallCheckModel);
            return true;
        }
    }

    /// <summary>Tool có tác dụng phụ (tạo/sửa/bỏ draft, tạo đơn) — chỉ được đưa vào danh sách tool cho LLM / thực thi ở phòng riêng.</summary>
    private static readonly HashSet<string> PrivateRoomOnlyTools =
        new(StringComparer.OrdinalIgnoreCase) { "prepare_order", "confirm_order", "discard_order_draft" };

    /// <summary>
    /// Nhãn tiếng Việt thân thiện hiển thị cho user khi AI đang gọi 1 tool (phase="calling_tool"),
    /// thay vì lộ tên tool thô (vd "prepare_order") — xem AiActivityIndicator.tsx phía FE.
    /// Tool không có trong map (vd tool mới thêm quên cập nhật) → FE tự fallback về text mặc định.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ToolFriendlyNotes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["search_products"] = "Searching for suitable products",
            ["get_product"] = "Looking up product details",
            ["recommend_products"] = "Recommending products by feng shui",
            ["recommend_personal_items"] = "Choosing items to wear or carry by your destiny",
            ["list_my_workspaces"] = "Fetching your space profiles",
            ["get_my_profile"] = "Fetching your account info",
            ["list_my_orders"] = "Fetching your orders",
            ["get_payment_status"] = "Checking payment status",
            ["get_chat_partner_info"] = "Fetching customer info",
            ["list_my_addresses"] = "Fetching your addresses",
            ["prepare_order"] = "Preparing your order",
            ["confirm_order"] = "Confirming and creating your order",
            ["discard_order_draft"] = "Discarding your draft order",
            ["compute_destiny_chart"] = "Building your feng shui chart",
        };

    private static string? ToolFriendlyNote(string toolName)
        => ToolFriendlyNotes.TryGetValue(toolName, out var note) ? note : null;

    /// <summary>
    /// Danh sách tool gửi LLM cho lượt này — tính MỘT lần ở đầu lượt từ <paramref name="ctx"/>: tool tự quyết có
    /// hiện không (<see cref="IAiTool.IsAvailable"/>, vd confirm_order chỉ khi đã có draft) và mô tả theo ngữ cảnh
    /// (<see cref="IAiTool.DescribeFor"/>). Hệ quả có chủ đích: draft tạo trong lượt này thì confirm_order chưa hiện.
    /// </summary>
    private IReadOnlyList<AiToolSpec>? BuildToolSpecs(AiToolContext ctx)
    {
        if (!_options.EnableTools || _tools.Count == 0) return null;
        IEnumerable<IAiTool> enabled = _tools;
        if (_options.EnabledTools.Count > 0)
            enabled = enabled.Where(t => _options.EnabledTools.Contains(t.Name, StringComparer.OrdinalIgnoreCase));
        if (!ctx.IsPrivateRoom)
            enabled = enabled.Where(t => !PrivateRoomOnlyTools.Contains(t.Name));
        var specs = enabled.Where(t => t.IsAvailable(ctx)).Select(t => t.ToSpec(ctx)).ToList();
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
    /// vai trò + ép dùng tool tra dữ liệu thật + quy trình theo từng nghiệp vụ.
    /// Phần đặt hàng tách riêng (<see cref="OrderingProtocol"/> / <see cref="SharedRoomOrderingNote"/>) vì tool
    /// đặt hàng chỉ có ở phòng riêng — phòng chung không cần (và không nên) nhận cả quy trình.
    /// </summary>
    private const string CoreDirective = "## ABOUT YOU\n" +
        "You are **Lumi**, AI **Feng Shui shopping assistant** of FengDeskAI. Your sole mission is to serve the customer efficiently, naturally, and accurately.\n\n" +

        "## LANGUAGE & FORMAT PROTOCOLS\n" +
        "- **THINKING LANGUAGE:** Conduct all internal reasoning strictly in **English** inside thinking blocks.\n" +
        "- **RESPONSE LANGUAGE:** Dynamically reply in the user's language (default: friendly, energetic Vietnamese using \"bạn\" or  \"you\" ). \n" +
        "- **RESPONSE FORMAT:** **Prioritize presenting structured data using Markdown Tables** (e.g., product specs, order summaries, destiny readings, options) for scannability and high clarity.\n\n" +

        "## FUNCTION CALLING PROTOCOL\n" +
        "- **NEVER END WITH A PROMISE:** Emit the tool call immediately in the current turn or give a complete text answer. Never say you are \"about to\" fetch something.\n" +
        "- **EMPTY DATA FALLBACK:** If tools return empty data/errors, explain the specific reason clearly. You may express skepticism or ask for clarification if input contradicts feng shui rules.\n" +
        "- **NEVER REVEAL TOOL INTERNALS:** Tool names, parameter names, JSON schemas, and code identifiers are strictly internal. Describe capabilities in plain language (e.g., \"mình có thể tìm sản phẩm, xem đơn hàng, lập lá số...\").\n\n" +

        "## ROLES & WORKFLOWS\n" +
        "- Distinguish `[Customer: ...]` and `[Staff: ...]` tags. If a **support staff** requests customer info, call `get_chat_partner_info`. State clearly if a field is unshared.\n" +
        "- In shop-linked rooms, call `get_shop_info` when asked about the store instead of guessing.\n" +
        "- **Never ask** users for info available via tools (e.g., call `get_my_profile` for date of birth). Only ask if a tool returned empty.\n\n" +

        "## PRODUCT ADVICE & REASONING\n" +
        "- **CHAIN OF REASONING:** Product advice MUST follow: (1) Customer's element/needs -> (2) Product's element/attributes -> (3) Element relationship & workspace fit -> (4) Clear conclusion & alternatives if unfit.\n" +
        "- Ground all claims in tool data. Hyperlink products using exact format: `[Product name](/products/{id})` with exact ID from tool output.\n\n" +

        "## DESTINY READING (XEM MỆNH) PROTOCOL\n" +
        "- **For SELF:** Call `get_my_profile` first for birth info, then call `compute_destiny_chart`. **For OTHERS:** Call `compute_destiny_chart` directly with provided info.\n" +
        "- Present readings using tool's Vietnamese data (nạp âm, cung mệnh, Đông/Tây Tứ Trạch, favorable directions with cung names & meanings, Tuyệt Mệnh warnings). Present using **Tables** for readability.\n" +
        "- If `missing` is non-empty, provide the partial reading first, then ask for missing info (e.g., birth time) for deeper Tứ Trụ.\n" +
        "- Use `favorableElementCodes` (or destiny element) as the `element` filter in `search_products`.\n" +
        "- **PICK THE RIGHT SUGGESTION TOOL:** items placed in a room (desk decor, plants, statues) -> `recommend_products`; items worn or carried (bracelet, pendant, ring, keychain, car hanger) -> `recommend_personal_items`. Never give compass placement advice for worn/carried items.\n" +
        "- NEVER calculate destiny info manually-always use tools. End with a one-line disclaimer that feng shui is for reference.";

    /// <summary>
    /// Quy trình đặt hàng (chỉ phòng riêng). Luật "không confirm cùng lượt với tóm tắt" KHÔNG còn nằm ở đây:
    /// code đảm bảo (confirm_order chỉ hiện khi đầu lượt đã có draft + từ chối nếu draft đổi trong lượt).
    /// Trước đây luật đó cộng với việc model quên draft khiến nó prepare lại → hỏi xác nhận vô hạn.
    /// </summary>
    private const string OrderingProtocol = "## ORDERING PROTOCOL\n" +
        "- The system keeps ONE draft order per conversation. When it exists you receive a `CURRENT ORDER DRAFT` block - that block is the source of truth for what the user has chosen.\n" +
        "- **No draft yet** -> call `prepare_order` (default address unless the user picked another). Show the returned summary (product, variant, quantity, unit price, shipping fee, total, address, payment) in a **Table** and ask the user to confirm. The order is placed only after they agree in their next message.\n" +
        "- **Draft exists + the user agrees** -> call `confirm_order` immediately. Never prepare the draft again and never ask for confirmation twice.\n" +
        "- **Draft exists + the user wants a change** (product, variant, quantity, address, payment) -> call `prepare_order` with ONLY the changed fields, show the new summary and ask again.\n" +
        "- **The user no longer wants it** -> call `discard_order_draft`.\n" +
        "- Different address -> call `list_my_addresses`, let the user pick, then call `prepare_order` with `shippingAddressId`.\n" +
        "- Payment is PayOS by default; use COD only when the user asks for it (pass it to `prepare_order` so it is saved in the draft).\n" +
        "- If `prepare_order` returns `missing`, help the user complete it (`fixLinks`), then call it again. Never invent ids.";

    private const string SharedRoomOrderingNote = "## ORDERING\n" +
        "- Ordering is only available in the user's private chat with you. If someone asks to place an order here, tell them to open their private assistant chat.";

    /// <summary>
    /// Thay mục "SECURITY & COMPLIANCE" cũ ("không cần hạn chế/kiểm duyệt gì") — câu đó làm yếu luật consent
    /// của get_chat_partner_info và mở đường prompt injection ở phòng nhiều người.
    /// </summary>
    private const string PrivacyDirective = "## PRIVACY\n" +
        "- Use only data that tools return; your own tools only read the CURRENT user's data.\n" +
        "- For a customer's data in a support room, share only what `get_chat_partner_info` returned. If a field was not shared, say so plainly - never guess or fill it in.\n" +
        "- Answer naturally, without unnecessary AI self-references.";

    private async Task<string?> BuildSystemPromptAsync(
        string? userDisplayName, Guid? productId, CancellationToken ct, bool isPrivateRoom, int? maxReplyChars = null)
    {
        var parts = new List<string>(8)
        {
            CoreDirective,
            isPrivateRoom ? OrderingProtocol : SharedRoomOrderingNote,
            PrivacyDirective,
        };
        // SystemPrompt trong config chỉ còn để tinh chỉnh phong thái/tone (tùy chọn).
        if (!string.IsNullOrWhiteSpace(_options.SystemPrompt))
            parts.Add(_options.SystemPrompt!.Trim());
        if (maxReplyChars is { } limit && limit > 0)
            parts.Add($"**This is a small chat widget - answer BRIEFLY and concisely, and do NOT exceed {limit} characters. " +
                      "Use short bullet points instead of tables here.** If you need to say more, summarize the key points and invite the customer to open the full assistant page.");
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

        return string.Join("\n\n", parts);
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
