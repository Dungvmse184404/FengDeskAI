# AI Chat Rewind — Thiết kế trước khi code

> Cho phép user trong **phòng riêng user↔AI** quay lại một tin nhắn cũ của mình:
> bấm nút rewind (ẩn mặc định, hover mới hiện) → **sửa nội dung tin đó và gửi lại** (edit & resend
> — kiểu Gemini). Tin được chọn + mọi tin sau nó bị loại khỏi hội thoại, thay bằng tin đã sửa.
> UX chỉ có 1 hành động edit vì đa phần rewind nghĩa là user muốn sửa lời của mình;
> API vẫn hỗ trợ regenerate (giữ nguyên nội dung) để dành cho sau.

## 1. Nguyên lý

AI không có "trí nhớ" riêng — mỗi lượt nó chỉ đọc `GetRecentAsync` từ `chat_messages`.
Vì vậy rewind = **soft-delete phần đuôi lịch sử + gửi lại tin** → AI tự "quên" phần đã cắt.
Không cần đổi gì trong pipeline LLM (nudge-retry, linkify, tools hoạt động nguyên vẹn).

```
Trước:  U1 → A1 → U2 → A2 → U3 → A3
Rewind tại U2 (sửa thành U2'):
        U1 → A1 → U2' → A2''          (U2, A2, U3, A3 bị IsDeleted = true)
```

## 2. Phạm vi & ràng buộc

- **Chỉ phòng riêng user↔AI** (check giống `SendAsync`: có AiBot, có user, không có user khác).
  Phòng chung KHÔNG cho rewind — sửa lịch sử là gian lận với người tham gia khác.
- Chỉ rewind được **tin của chính user** (`SenderType == User`, `SenderId == userId`).
- Soft-delete (`IsDeleted = true`) — tin cũ vẫn nằm trong DB để audit, query filter tự loại khỏi mọi truy vấn.
- Ảnh đính kèm tin cũ: đi theo message (không xóa file trên storage — chấp nhận rác, dọn sau).

## 3. API

### `POST api/chat/ai/messages/{messageId}/rewind`

Request body (`AiRewindRequest`):
```json
{
  "newMessage": "string | null",   // null/absent = giữ nguyên nội dung cũ (regenerate)
  "imageUrls": ["..."] | null,     // null = giữ ảnh cũ; [] = bỏ ảnh; có phần tử = thay ảnh
  "model": "string | null"         // như AiChatRequest.Model
}
```

Response: **tái dùng `AiChatResponse`** (ChatboxId, Model, Reply, History) — FE replace toàn bộ history.

Lỗi:
| Case | Trả về |
|---|---|
| Message không tồn tại / không thuộc user / đã IsDeleted | 404 "Không tìm thấy tin nhắn." |
| Message không phải SenderType.User | 400 "Chỉ rewind được tin nhắn của bạn." |
| Chatbox không phải phòng riêng AI | 404 (giống SendAsync, không lộ tồn tại phòng) |

## 4. Luồng xử lý BE

`AiChatService.RewindAsync(userId, userRole, userEmail, userDisplayName, messageId, request, ct)`:

1. Load message (kèm Images) + chatbox (kèm Participants). Validate như mục 2.
2. Xác định nội dung gửi lại:
   - `content = request.NewMessage ?? message.Content`
   - `images  = request.ImageUrls ?? message.Images.Select(i => i.Url)`
3. **Soft-delete từ message trở đi**: repo method mới
   `IChatMessageRepository.SoftDeleteFromAsync(chatboxId, fromCreatedAt, fromId, ct)`
   — set `IsDeleted = true` cho mọi message có `(CreatedAt, Id) >= (from.CreatedAt, from.Id)`
   trong chatbox (dùng tuple so sánh để tie-break tin cùng timestamp).
4. `SaveChangesAsync` — điểm cắt phải commit TRƯỚC khi gọi LLM (nếu LLM lỗi thì lịch sử đã cắt
   nhưng tin mới chưa gửi → user thấy hội thoại dừng ở điểm rewind, bấm gửi lại được; chấp nhận).
5. Gọi lại chính **`SendAsync`** với `AiChatRequest { ChatboxId, Message = content, ImageUrls = images, Model = request.Model }`
   → toàn bộ flow lưu tin / gọi LLM / tools / broadcast / trả History tái dùng 100%.

Controller (`ChatController`, khu "Người ↔ AI"):
```csharp
[HttpPost("ai/messages/{messageId:guid}/rewind")]
public async Task<IActionResult> RewindAi(Guid messageId, [FromBody] AiRewindRequest request, CancellationToken ct)
    => ToActionResult(await _aiService.RewindAsync(
        CurrentUserId, CurrentUser.Role, CurrentUser.Email, CurrentUser.Name, messageId, request, ct));
```

## 5. FE (FengDeskAI_FE — feature chatbox)

Cấu trúc hiện có: `hooks/useAiChat.ts` giữ `messages` bằng `useState` + expose
`{ messages, sending, activity, send, uploadImage, loadHistory, clearConversation }`;
`api/chat.api.ts` có `sendToAi`; `ChatMessageBubble({ message, isOwn })` render bubble.

### UX (quyết định chốt)
- Nút rewind **ẩn mặc định**, chỉ hiện khi **hover** vào bubble tin của user
  (Tailwind: wrapper `group` + nút `opacity-0 group-hover:opacity-100 transition-opacity`).
- Bấm nút → bubble chuyển sang **edit mode inline**: textarea prefill nội dung cũ + nút Gửi / Hủy.
  Không có nút regenerate riêng — edit là hành động duy nhất.
- Trong lúc edit, tin được chọn coi như "đang bị thay thế" (disable, mờ đi); các tin phía sau
  cũng nên mờ nhẹ để user hiểu chúng sẽ biến mất khi gửi.
- Gửi → mọi tin từ điểm đó bị thay bằng lịch sử mới từ server. Hủy → trả về trạng thái thường.
- Ẩn nút rewind khi `sending === true` (AI đang trả lời).

### Thay đổi code
| File | Việc |
|---|---|
| `api/chat.api.ts` | thêm `rewindAi: (messageId, payload) => post("/chat/ai/messages/{id}/rewind")` |
| `hooks/useAiChat.ts` | thêm `rewind(messageId, newText)`: set `sending`, gọi API, thành công → `setMessages(mapHistory(res.history))` (replace, không append) |
| `components/ChatMessageBubble.tsx` | props thêm `onRewind?: (id, text) => void` + state edit mode nội bộ; chỉ render nút khi `isOwn && onRewind` |
| `components/AiAssistantDrawer.tsx` | truyền `onRewind={rewind}` xuống bubble; đã có auto-scroll theo `messages` |

Lưu ý: `messages` cần mang theo `id` thật của message từ server (kiểm tra `mapMessages`/`mapHistory`
trong `useAiChat` — nếu `AiChatTurn` chưa trả id thì BE phải thêm `MessageId` vào `AiChatTurn`).
Optimistic UI không cần — chờ response rồi replace là đủ (đã có loading indicator).

## 6. File cần tạo / sửa

**BE:**
| File | Việc |
|---|---|
| `DTOs/AiChatDtos.cs` | thêm `AiRewindRequest`; **thêm `Guid Id` vào `AiChatTurn`** (hiện chỉ có Role/Content/Images — FE cần id để gọi rewind). Sửa 2 chỗ build turns trong `AiChatService.SendAsync` + endpoint load history của trang AI |
| `Services/IAiChatService.cs` | thêm `RewindAsync` |
| `Services/AiChatService.cs` | implement `RewindAsync` (validate + cắt + gọi `SendAsync`) |
| `Interfaces/Repositories/IChatMessageRepository.cs` | thêm `SoftDeleteFromAsync` |
| `Persistence/Repositories/ChatMessageRepository.cs` | implement (ExecuteUpdate hoặc load-set-save) |
| `WebAPI/Controllers/ChatController.cs` | endpoint mới |

**FE:** `ChatMessageBubble.tsx` (nút edit/regenerate), `AiAssistantDrawer.tsx` (handler gọi API + replace history), API client.

**Không cần migration** — `IsDeleted` đã có sẵn trên `BaseEntity`.

## 7. Edge cases

- Rewind tin ĐẦU tiên của phòng → cả phòng trống + gửi lại từ đầu: hợp lệ, không cần chặn.
- Double-click rewind 2 lần: lần 2 message đã `IsDeleted` → 404, vô hại. FE disable nút khi loading.
- Rewind trong khi AI đang trả lời lượt khác: FE chặn bằng loading state; BE nếu race thì
  tin AI đang lưu dở sẽ nằm SAU điểm cắt về thời gian → không phá lịch sử đã cắt, chấp nhận.
- `MaxHistoryTurns` vẫn áp bình thường — rewind xa hơn cửa sổ nhớ vẫn đúng vì cắt ở tầng DB.

## 8. Test checklist

- [ ] Sửa tin giữa hội thoại → tin sau biến mất, AI trả lời theo nội dung mới
- [ ] Regenerate (không sửa) → AI trả lời lại, có thể khác lần trước
- [ ] Rewind tin có ảnh, giữ ảnh cũ / thay ảnh / bỏ ảnh
- [ ] Rewind tin của AI → 400
- [ ] Rewind message của phòng chung → 404
- [ ] Rewind messageId của user khác → 404
- [ ] Gọi rewind 2 lần cùng messageId → lần 2 404, DB chỉ có 1 nhánh hội thoại sống
- [ ] Tin bị cắt vẫn còn trong DB với IsDeleted = true
