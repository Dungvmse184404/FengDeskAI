# 24 — Dev Tools

[← Mục lục](./README.md)

Controller: `DevToolsController` · Route gốc: `/api/dev/tools` · **`[Authorize]`** · ⚠️ **Chỉ môi trường Development** (Production trả `404`).

Test tay các AI tool — gọi thẳng `IAiTool.ExecuteAsync` đúng path mà AI dùng, với ngữ cảnh scope theo user đang đăng nhập. Khác path AI: endpoint này **không nuốt exception** → phơi bày nguyên nhân thật khi tool báo lỗi (vd `search_products`, `recommend_products`).

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/dev/tools` | Authenticated (Dev) | Liệt kê tool + schema tham số |
| POST | `/api/dev/tools/{name}` | Authenticated (Dev) | Chạy 1 tool theo tên |

---

## GET `/api/dev/tools`
Liệt kê tool khả dụng + schema tham số:
```json
[{
  "name": "search_products",
  "description": "...",
  "parameters": { "query": { "type": "string", "description": "...", "required": true, "enum": null } }
}]
```

## POST `/api/dev/tools/{name}`
Chạy tool. Body = JSON đối số (giống thứ LLM sinh ra), vd:
```json
{ "query": "Hỏa" }
```
Query: `chatboxId` (guid?, chỉ cần cho tool đọc theo phòng như `get_chat_partner_info`).

**Response:** chính chuỗi JSON output mà AI nhận (`Content`, `application/json`).
**Lỗi:** tool không tồn tại → `404` (kèm danh sách `available`); tool ném exception → `500` kèm `message`, `type`, `inner`, `stackTrace`.

---

[← Ping](./23-ping.md) · [Phụ lục: Enums & Models →](./99-appendix-models.md)
