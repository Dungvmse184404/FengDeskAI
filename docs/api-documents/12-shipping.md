# 12 — Shipping

[← Mục lục](./README.md)

Controller: `ShippingController` · Route gốc: `/api/shipping` · Mặc định `[Authorize]`; webhook là `[AllowAnonymous]` (tự bảo vệ bằng secret).

Tích hợp vận chuyển (GHN, AhaMove). Các webhook chuẩn hóa payload riêng của từng nhà vận chuyển về `ShippingWebhookRequest` rồi chạy chung pipeline.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/shipping/webhook` | Public + header secret | Webhook tổng quát |
| POST | `/api/shipping/ahamove/webhook` | Public + header secret | Webhook AhaMove |
| POST | `/api/shipping/ghn/webhook` | Public + query `key` | Webhook GHN |
| GET | `/api/shipping/deliveries/{deliveryId}/progress` | Owner/Staff/Admin | Lịch sử tiến trình giao |
| POST | `/api/shipping/deliveries/{deliveryId}/redeliver` | Owner/Staff/Admin | Yêu cầu giao lại |

---

## POST `/api/shipping/webhook`

🔓 Public, yêu cầu header `X-Webhook-Secret` (so với `ShippingWebhook:Secret`). Sai → `401`.
**Request body** (`ShippingWebhookRequest`)
```json
{
  "provider": "GHN", "eventType": "switch_status", "deliveryId": "guid",
  "providerOrderId": "...", "newStatus": "Delivered",
  "trackingCode": "...", "trackingUrl": "...", "rawPayload": "..."
}
```

## POST `/api/shipping/ahamove/webhook`

🔓 Public + header `X-Webhook-Secret`. Nhận payload riêng của AhaMove (`AhamoveCallback`), tự map về `ShippingWebhookRequest`. Khớp delivery theo `tracking_number` (= delivery.Id), fallback `(Provider + ProviderOrderId)`.

## POST `/api/shipping/ghn/webhook`

🔓 Public — GHN không gửi được header secret nên bảo vệ bằng **query `key`** (so với `ShippingWebhook:Secret`). Nhận `GhnCallback`. Chỉ `Type` = `create`/`switch_status` mới đẩy state machine; loại khác trả `200` để GHN không retry.

Query: `key` (string, bắt buộc).

## GET `/api/shipping/deliveries/{deliveryId}/progress`

🔒 Owner/staff store hoặc admin. `data` = mảng `DeliveryProgressLogResponse`:
```json
[{ "id": "guid", "deliveryId": "guid", "sourceType": "Webhook",
   "fromStatus": "Shipped", "toStatus": "Delivered", "note": "...", "loggedAt": "..." }]
```

## POST `/api/shipping/deliveries/{deliveryId}/redeliver`

🔒 Owner/staff store hoặc admin. Yêu cầu nhà vận chuyển giao lại một đơn giao thất bại.

---

[← Returns](./11-returns.md) · [Tiếp: Addresses →](./13-addresses.md)
