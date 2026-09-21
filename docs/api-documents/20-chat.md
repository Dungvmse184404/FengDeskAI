# 20 — Chat

[← Mục lục](./README.md)

Controller: `ChatController` · Route gốc: `/api/chat` · Mặc định `[Authorize]`.

Chat người ↔ người (customer ↔ garden owner/staff/manager) và người ↔ trợ lý AI, dùng chung mô hình `chatboxes` / `chat_messages`. AI có thể đọc ngữ cảnh sản phẩm để hỗ trợ.

---

## 📋 Bảng endpoint — **21 endpoint**

### Người ↔ người & hỗ trợ sàn

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/chat/chatbox/with/{otherUserId}` | Authenticated | Lấy/tạo phòng 1-1 |
| POST | `/api/chat/support` | Authenticated | Lấy/tạo phòng hỗ trợ sàn (`?forceNew=true` → luôn tạo mới) |
| GET | `/api/chat/support/open` | **StaffOrAbove** | Hàng đợi phòng hỗ trợ sàn đang mở (paged) |
| POST | `/api/chat/groups` | Authenticated | Tạo phòng nhóm |
| POST | `/api/chat/chatbox/{chatboxId}/participants` | Authenticated | Thêm thành viên |
| DELETE | `/api/chat/chatbox/{chatboxId}/participants/{userId}` | Owner | Xóa thành viên |
| GET | `/api/chat/chatboxes` | Authenticated | Danh sách chatbox của tôi (paged) |
| GET | `/api/chat/chatbox/{chatboxId}/messages` | Authenticated | Tin nhắn trong phòng (paged) |
| POST | `/api/chat/chatbox/{chatboxId}/messages` | Authenticated | Gửi tin nhắn |
| POST | `/api/chat/chatbox/{chatboxId}/images` | Authenticated | Upload ảnh chat (multipart) |
| PATCH | `/api/chat/chatbox/{chatboxId}/read-all` | Authenticated | Đánh dấu cả phòng đã đọc |
| DELETE | `/api/chat/chatbox/{chatboxId}` | Authenticated | Rỗng → xóa hẳn; còn tin → **đóng phòng** (khoá, hiện mờ) |
| GET | `/api/chat/chatbox/{chatboxId}/consent` | Authenticated | Quyền chia sẻ thông tin của tôi |
| PUT | `/api/chat/chatbox/{chatboxId}/consent` | Authenticated | Cập nhật quyền chia sẻ |

### Hỗ trợ theo SHOP (vendor: garden owner/staff)

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/chat/support/stores/{storeId}` | Authenticated | Khách nhắn tin cho một shop cụ thể |
| GET | `/api/chat/support/stores/{storeId}/open` | Owner/staff store (hoặc Admin) | Hàng đợi phòng đang mở của store |
| GET | `/api/chat/support/stores/{storeId}/mine` | Owner/staff store (hoặc Admin) | Phòng của store mình đã nhận |

### Người ↔ AI

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/chat/ai/messages` | Authenticated | Gửi tin cho trợ lý AI |
| POST | `/api/chat/ai/messages/{messageId}/rewind` | Authenticated | **Sửa & gửi lại** một tin cũ của mình — cắt lịch sử từ đó trở đi rồi gọi lại LLM |
| POST | `/api/chat/ai/chatbox` | Authenticated | Lấy/tạo phòng user ↔ AI (`?productId=`) — cần trước khi upload ảnh ở lượt đầu |
| GET | `/api/chat/ai/config` | Authenticated | Cửa sổ nhớ (số tin) để FE vẽ mốc "AI context limit" |

> Đánh dấu đã đọc có **cả hai đường**: REST `PATCH …/read-all` và hub method `MarkChatboxRead` — dùng cái nào cũng được.

## SignalR — hub `/hubs/chat`

Token truyền qua query `access_token` (cấu hình JwtBearer cho đường `/hubs`).

| Hub method (client → server) | Dùng để |
|---|---|
| `JoinChatbox(chatboxId)` / `LeaveChatbox(chatboxId)` | Nhận `messageReceived` của phòng |
| `MarkChatboxRead(chatboxId)` | Đánh dấu đã đọc (tương đương `PATCH …/read-all`) |
| `JoinAiOperation(operationId)` / `LeaveAiOperation(operationId)` | Nhận `aiStatus` |

### Event `aiStatus` — trạng thái AI realtime

Group: **`ai-op-{operationId}`**. Client **phải gọi `JoinAiOperation` trước**, nếu không sẽ không nhận được gì.

`operationId` theo quy ước:

| Luồng | `operationId` |
|---|---|
| Chat AI | `chat-{chatboxId}` |
| Workspace intake | GUID do server sinh, trả về từ `POST /api/workspace/parse-description` |

**Payload** (`AiActivityEvent`):
```json
{ "operationId": "chat-3f2a…", "phase": "thinking", "toolName": null, "note": "Đang xem lại hồ sơ không gian của bạn…" }
```

| `phase` | Ý nghĩa |
|---|---|
| `thinking` | Model đang suy luận; `note` = đuôi chuỗi suy luận đang chảy (ephemeral) |
| `calling_tool` | Đang gọi tool; `note` = **nhãn tiếng Việt thân thiện** do BE map (`AiChatService.ToolFriendlyNotes`) |
| `writing` | Đang soạn câu trả lời cuối |
| `narration` | Lời dẫn trung gian — **dồn theo lượt**, không đè indicator, không lưu DB |
| `done` | Kết thúc lượt (scope `Begin()` tự phát khi dispose, **kể cả khi có exception**) |
| `error` | Lượt lỗi |

⚠️ **Ba điểm FE phải biết:**

1. **`toolName` LUÔN `null` trên dây** — bị null hoá ở `SanitizingAiActivityNotifier` (tên hàm nội bộ, không để lộ qua DevTools). Đừng thiết kế UI dựa vào nó; dùng `note`.
2. **`note` đã qua bộ lọc `LiveStream`** → có thể chứa `[id]` / `[email]` / `[link]` thay cho giá trị thật. Đây là chủ ý, không phải lỗi.
3. Luôn có `done` hoặc `error` đóng lượt → indicator không bao giờ treo vì thiếu event.

---

### Rewind — hành vi khi LLM lỗi

Cắt lịch sử + gọi LLM nằm trong **cùng một transaction**. LLM chết ⇒ **rollback**, lịch sử nguyên vẹn, client nhận đúng `503` của `SendAsync`.
Chỉ rewind được tin của **chính mình**, loại `User`, trong **phòng riêng user↔AI**. Sai điều kiện → `404 "Không tìm thấy tin nhắn."` (cố tình không trả 403 để không lộ tồn tại tin của người khác).

> 💡 Tin nhắn FE tự vẽ khi đang chờ (optimistic) mang id tạm dạng `u-…`, **không phải GUID** — gọi rewind với id đó sẽ luôn `404`. FE phải chặn trước khi gửi request.

---

## Người ↔ người

**POST `/api/chat/chatbox/with/{otherUserId}`** — lấy/tạo phòng 1-1 với user khác. `data` = `ChatboxResponse`.

**POST `/api/chat/support`** — lấy/tạo phòng hỗ trợ (mình là Owner). Query `forceNew` (bool) = true → luôn tạo phòng mới.

**GET `/api/chat/support/open`** (StaffOrAbove, paged) — hàng đợi phòng hỗ trợ chưa có nhân sự nhận.

**POST `/api/chat/groups`** — tạo phòng nhóm. Body `CreateGroupRequest`:
```json
{ "title": "Nhóm hỗ trợ", "memberUserIds": ["guid", "guid"] }
```

**POST `/api/chat/chatbox/{chatboxId}/participants`** — thêm thành viên (Owner, hoặc staff tự join → truyền UserId của chính mình). Body `AddParticipantRequest`: `{ "userId": "guid" }`.

**DELETE `/api/chat/chatbox/{chatboxId}/participants/{userId}`** — xóa thành viên (chỉ Owner).

---

## Phòng & tin nhắn

**GET `/api/chat/chatboxes`** (paged) — `data` = `ChatboxListResponse` (có `unreadCount`, `lastMessage`):
```json
{
  "items": [{
    "id": "guid", "isGroup": false, "isSupport": false, "isClosed": false,
    "title": null, "createdByUserId": "guid", "productId": null,
    "createdAt": "...", "updatedAt": "...",
    "participants": [{ "userId": "guid", "participantType": "Customer",
                       "role": "Owner", "isMuted": false, "isHidden": false }],
    "unreadCount": 2,
    "lastMessage": { "id": "guid", "chatboxId": "guid", "senderId": "guid",
                     "senderType": "User", "senderName": "...", "content": "Xin chào",
                     "createdAt": "...", "images": [] }
  }],
  "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1
}
```

**GET `/api/chat/chatbox/{chatboxId}/messages`** (paged, mới nhất trước) — `data` = `PagedResult<ChatMessageResponse>`.

**POST `/api/chat/chatbox/{chatboxId}/messages`** — gửi tin (text và/hoặc ảnh). Body `SendMessageRequest`:
```json
{ "content": "Xin chào", "imageUrls": ["https://..."] }
```

**POST `/api/chat/chatbox/{chatboxId}/images`** — `multipart/form-data`, field `file`. Trả link để gắn vào `imageUrls` khi gửi tin.

**PATCH `/api/chat/chatbox/{chatboxId}/read-all`** — đánh dấu cả phòng đã đọc.

**DELETE `/api/chat/chatbox/{chatboxId}`** — phòng rỗng → xóa hẳn; còn tin nhắn → đóng phòng (khóa, hiện mờ).

---

## Quyền chia sẻ (consent)

**GET `/api/chat/chatbox/{chatboxId}/consent`** — `data` = `ChatConsentResponse`.
**PUT `/api/chat/chatbox/{chatboxId}/consent`** — body `SetChatConsentRequest`:
```json
{ "shareProfile": true, "shareWorkspaces": false, "shareOrders": true }
```

---

## Người ↔ AI

**POST `/api/chat/ai/messages`** — gửi tin cho trợ lý AI. Body `AiChatRequest`:
```json
{ "chatboxId": null, "message": "Gợi ý cây hợp mệnh Hỏa", "model": null,
  "productId": null, "imageUrls": [] }
```
> Bỏ trống `chatboxId` ở lượt đầu (kèm `productId` nếu hỏi về sản phẩm) → server tạo hội thoại AI và trả lại `chatboxId` để dùng cho lượt sau.

**Response `data`** = `AiChatResponse`:
```json
{
  "chatboxId": "guid", "model": "...", "reply": "...",
  "history": [{ "role": "user", "content": "...", "images": [] },
              { "role": "assistant", "content": "...", "images": [] }]
}
```

**POST `/api/chat/ai/chatbox`** — lấy/tạo phòng user ↔ AI và trả `chatboxId` (gọi trước khi upload ảnh ở lượt đầu chưa gửi tin). Query `productId` (guid?, tùy chọn).

---

[← Reviews](./19-reviews.md) · [Tiếp: Notifications →](./21-notifications.md)
