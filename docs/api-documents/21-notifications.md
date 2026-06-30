# 21 — Notifications

[← Mục lục](./README.md)

Controller: `NotificationsController` · Route gốc: `/api/notifications` · **Toàn bộ `[Authorize]`** — thông báo của user đang đăng nhập.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/notifications` | Authenticated | Danh sách thông báo (paged) |
| GET | `/api/notifications/unread-count` | Authenticated | Số chưa đọc (badge) |
| PATCH | `/api/notifications/{id}/read` | Authenticated | Đánh dấu 1 cái đã đọc |
| PATCH | `/api/notifications/read-all` | Authenticated | Đánh dấu tất cả đã đọc |

---

## GET `/api/notifications`
Query: `page`, `pageSize` (`PageRequest`) + `unreadOnly` (bool?, `true` = chỉ chưa đọc).
`data` = `PagedResult<NotificationResponse>`:
```json
{
  "items": [{
    "id": "guid", "type": "OrderPaid", "title": "Đơn đã thanh toán",
    "message": "...", "isRead": false, "readAt": null,
    "referenceId": "guid", "referenceType": "Order", "createdAt": "..."
  }],
  "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1
}
```
> `type`: xem [Appendix → NotificationType](./99-appendix-models.md); `referenceType`: `Order/Delivery/Payment/...`.

## GET `/api/notifications/unread-count`
`data` = số nguyên (số thông báo chưa đọc).

## PATCH `/api/notifications/{id}/read`
Đánh dấu một thông báo đã đọc.

## PATCH `/api/notifications/read-all`
Đánh dấu tất cả đã đọc.

---

[← Chat](./20-chat.md) · [Tiếp: Uploads →](./22-uploads.md)
