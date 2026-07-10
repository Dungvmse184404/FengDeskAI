# ARD — Refactor: AI Activity Module (trạng thái AI realtime dùng chung)

> **Status:** Proposal.
> **Mục tiêu:** tách cơ chế `aiStatus` đang nằm trong chatbox thành module tái sử dụng cho mọi khâu có AI (chat, workspace intake, recommendation explain…). Chat hoạt động y nguyên trong quá trình chuyển.
> **Không làm:** map nhãn tiếng Việt (`TOOL_LABELS`) — event chỉ chứa `phase` + `toolName` thô, FE render trực tiếp.

## Hiện trạng

```
AiChatService.EmitActivityAsync(chatboxId, phase, toolName)
  └► IChatRealtimeNotifier.AiActivityAsync(...)          // Application abstraction
       └► SignalR group "chat-{chatboxId}", event "aiStatus"
            └► FE features/chatbox: ChatHubClient → AiActivityIndicator
```

Coupling cần gỡ: khóa định tuyến là `chatboxId`; điểm emit hard-code trong `AiChatService`; FE type + component nằm trong `features/chatbox`.

## 1. BE — Application

### 1.1 `Interfaces/External/IAiActivityNotifier.cs` (mới)

```csharp
public sealed record AiActivityEvent(
    string OperationId,      // "chat-{chatboxId}" | GUID client sinh (intake…) 
    string Phase,            // thinking | calling_tool | writing | done | error
    string? ToolName = null);

public interface IAiActivityNotifier
{
    /// <summary>Best-effort — không throw, không chặn luồng chính.</summary>
    Task PublishAsync(AiActivityEvent e, CancellationToken ct = default);

    /// <summary>Scope tự phát "done" khi dispose (kể cả khi exception) — tránh indicator treo.</summary>
    AiActivityScope Begin(string operationId);
}

public sealed class AiActivityScope : IAsyncDisposable
{
    // giữ operationId + notifier; PhaseAsync(phase, toolName?); DisposeAsync → publish "done"
    // nếu phase cuối là "error" thì dispose không ghi đè.
}
```

Phase là `string` (không enum) — thêm phase mới không cần migrate contract.

### 1.2 Sửa `AiChatService`

- Inject `IAiActivityNotifier` thay cho gọi `IChatRealtimeNotifier.AiActivityAsync`.
- `EmitActivityAsync` → `_activity.PublishAsync(new($"chat-{chatboxId}", phase, toolName), ct)`; hoặc bọc cả vòng tool-loop trong `Begin()`.
- **Xóa** `AiActivityAsync` khỏi `IChatRealtimeNotifier` (chỉ còn `MessageReceivedAsync`) — notifier chat chỉ lo tin nhắn.

### 1.3 `WorkspaceIntakeService` (từ ARD ai-intake) dùng luôn

```csharp
await using var act = _activity.Begin(request.OperationId);   // header X-Ai-Operation-Id
await act.PhaseAsync("thinking");     // trước khi gọi Ollama
await act.PhaseAsync("writing");      // Ollama trả stream/đang chờ parse
// dispose → "done"; lỗi → act.PhaseAsync("error") trước khi return Failure
```

## 2. BE — WebAPI

### 2.1 `Hubs/AiActivityNotifier.cs` (mới, impl)

```csharp
public sealed class AiActivityNotifier : IAiActivityNotifier
{
    // IHubContext<ChatHub> — tái dùng hub sẵn có làm transport
    public Task PublishAsync(AiActivityEvent e, CancellationToken ct = default)
        => Safe(_hub.Clients.Group($"ai-op-{e.OperationId}")
               .SendAsync("aiStatus", e, ct));   // giữ nguyên event name "aiStatus"
}
```

Try/catch nuốt lỗi + `LogDebug` (chuyển logic best-effort từ `EmitActivityAsync` cũ vào đây — service không phải tự bọc nữa).

### 2.2 `ChatHub` — thêm 2 method

```csharp
public Task JoinAiOperation(string operationId)
    => Groups.AddToGroupAsync(Context.ConnectionId, $"ai-op-{operationId}");
public Task LeaveAiOperation(string operationId) => ...;
```

- Quyền: `[Authorize]` sẵn có trên hub + operationId là GUID ngẫu nhiên client sinh (capability token). Đủ cho phạm vi hiện tại.
- **Tương thích chat:** để không phải sửa FE chat cùng lúc, `JoinChatbox` hiện có join thêm group `ai-op-chat-{chatboxId}`. FE chat cũ nhận event như cũ, không đổi dòng nào.

### 2.3 DI

`services.AddSingleton<IAiActivityNotifier, AiActivityNotifier>();`

## 3. FE — `features/shared/ai-activity/` (mới)

```
types.ts          # AiActivity { operationId, phase, toolName? } — bỏ chatboxId
useAiActivity.ts  # hook
AiActivityIndicator.tsx   # move từ chatbox, bỏ TOOL_LABELS
```

### 3.1 `useAiActivity(operationId | null)`

- `operationId` null → không kết nối (idle).
- Join group `ai-op-{operationId}` qua connection SignalR dùng chung (tách `ChatHubClient` phần connection ra `lib/signalr.ts` nếu tiện, hoặc nhận connection qua param — quyết khi code).
- Lắng nghe `aiStatus`, lọc theo `operationId`, trả `{ activity, isActive }`; `phase === "done" | "error"` → `isActive = false`.
- Cleanup: leave group khi unmount/đổi id.

### 3.2 `AiActivityIndicator` (đơn giản hóa — không label map)

```
thinking      → spinner  + "AI đang xử lý…"
calling_tool  → wrench   + toolName thô (vd "search_products")
writing       → pen      + "Đang tạo kết quả…"
error         → cảnh báo + "Có lỗi xảy ra"
```

### 3.3 Nơi dùng

- **Chatbox:** đổi import sang shared component; `operationId = "chat-" + chatboxId`. Xóa bản cũ trong `features/chatbox`.
- **Workspace intake:** `WorkspaceDescribeStep` sinh `operationId = crypto.randomUUID()` → `useAiActivity(id)` → gửi header `X-Ai-Operation-Id` khi gọi parse → indicator hiện dưới nút phân tích.

## 4. Thứ tự PR

1 PR duy nhất, commit theo lớp: (1) interface + impl + DI, (2) chuyển AiChatService + gỡ method khỏi IChatRealtimeNotifier, (3) FE shared module + chatbox đổi import, (4) wire vào intake (nếu intake đã merge; chưa thì để lại cho PR intake).

## 5. Test

1. Chat: gửi tin cho AI → FE vẫn thấy thinking → calling_tool(toolName) → writing → done (regression, không đổi hành vi).
2. Intake: gọi parse với operationId → nhận thinking/writing/done đúng group; client khác không join → không nhận gì.
3. Ollama throw giữa chừng → scope dispose vẫn phát done/error — indicator không treo.
4. Notifier throw (hub chết) → service vẫn trả kết quả bình thường (best-effort).
5. 2 operation song song 2 tab → event không lẫn nhau (đúng group).
