# 20 — Chat

[← Mục lục](./README.md)

Controller: `ChatController` · Route gốc: `/api/chat` · Mặc định `[Authorize]`.

Chat người ↔ người (customer ↔ garden owner/staff/manager) và người ↔ trợ lý AI, dùng chung mô hình `chatboxes` / `chat_messages`. AI có thể đọc ngữ cảnh sản phẩm để hỗ trợ.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/chat/chatbox/with/{otherUserId}` | Authenticated | Lấy/tạo phòng 1-1 |
| POST | `/api/chat/support` | Authenticated | Lấy/tạo phòng hỗ trợ |
| GET | `/api/chat/support/open` | StaffOrAbove | Hàng đợi phòng hỗ trợ đang mở (paged) |
| POST | `/api/chat/groups` | Authenticated | Tạo phòng nhóm |
| POST | `/api/chat/chatbox/{chatboxId}/participants` | Authenticated | Thêm thành viên |
| DELETE | `/api/chat/chatbox/{chatboxId}/participants/{userId}` | Owner | Xóa thành viên |
| GET | `/api/chat/chatboxes` | Authenticated | Danh sách chatbox của tôi (paged) |
| GET | `/api/chat/chatbox/{chatboxId}/messages` | Authenticated | Tin nhắn trong phòng (paged) |
| POST | `/api/chat/chatbox/{chatboxId}/messages` | Authenticated | Gửi tin nhắn |
| POST | `/api/chat/chatbox/{chatboxId}/images` | Authenticated | Upload ảnh chat (multipart) |
| PATCH | `/api/chat/chatbox/{chatboxId}/read-all` | Authenticated | Đánh dấu cả phòng đã đọc |
| DELETE | `/api/chat/chatbox/{chatboxId}` | Authenticated | Xóa/đóng phòng |
| GET | `/api/chat/chatbox/{chatboxId}/consent` | Authenticated | Quyền chia sẻ thông tin của tôi |
| PUT | `/api/chat/chatbox/{chatboxId}/consent` | Authenticated | Cập nhật quyền chia sẻ |
| POST | `/api/chat/ai/messages` | Authenticated | Gửi tin cho trợ lý AI |
| POST | `/api/chat/ai/chatbox` | Authenticated | Lấy/tạo phòng user ↔ AI |

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
