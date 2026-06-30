# 11 — Returns

[← Mục lục](./README.md)

Controller: `ReturnsController` · Route gốc: `/api/returns` · Mặc định `[Authorize]`.

Trả hàng / hoàn tiền / đổi trả (RMA).
- **Customer:** tạo yêu cầu, xem của mình, hủy, gửi hàng trả, upload/xóa ảnh.
- **Vendor (owner/staff store):** xem & xử lý yêu cầu của delivery thuộc store mình (duyệt/từ chối/nhận hàng/xử lý).
- **Admin:** xem toàn bộ, can thiệp, xác nhận hoàn tiền.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/returns` | Customer | Tạo yêu cầu trả |
| GET | `/api/returns/mine` | Customer | Yêu cầu của tôi (paged) |
| POST | `/api/returns/{id}/cancel` | Customer | Hủy yêu cầu |
| POST | `/api/returns/{id}/ship-back` | Customer | Khai báo gửi hàng trả |
| POST | `/api/returns/{id}/images` | Customer (chủ) | Upload ảnh bằng chứng (multipart) |
| DELETE | `/api/returns/{id}/images/{imageId}` | Customer (chủ) | Xóa ảnh (khi còn chờ duyệt) |
| GET | `/api/returns/all` | AdminOnly | Tất cả yêu cầu (paged) |
| GET | `/api/returns/stores/{storeId}` | Owner/Staff/Admin | Yêu cầu của 1 store (paged) |
| GET | `/api/returns/{id}` | Customer/Vendor/Admin | Chi tiết yêu cầu |
| POST | `/api/returns/{id}/approve` | Vendor/Admin | Duyệt |
| POST | `/api/returns/{id}/reject` | Vendor/Admin | Từ chối |
| POST | `/api/returns/{id}/receive` | Vendor/Admin | Xác nhận đã nhận hàng trả |
| POST | `/api/returns/{id}/resolve` | Vendor/Admin | Xử lý (hoàn kho / hoàn tiền / đổi) |
| POST | `/api/returns/{id}/complete-refund` | AdminOnly | Xác nhận hoàn tiền xong |

---

## POST `/api/returns`

**Request body** (`CreateReturnRequest`)
```json
{
  "deliveryId": "guid",
  "type": "Refund",
  "reason": "Defective",
  "reasonDetail": "Chậu bị nứt",
  "items": [{ "orderItemId": "guid", "quantity": 1, "exchangeProductItemId": null }],
  "imageUrls": ["https://..."],
  "bankAccountName": "NGUYEN VAN A",
  "bankAccountNumber": "0123456789",
  "bankName": "Vietcombank"
}
```
| Field | Ghi chú |
|-------|---------|
| `type` | `Refund` hoặc `Exchange` |
| `items[].exchangeProductItemId` | Bắt buộc khi `type = Exchange` |
| `bank*` | Bắt buộc khi đơn COD + hoàn tiền (chuyển khoản) |

---

## GET `/api/returns/mine` · `/all` · `/stores/{storeId}`

Paged. `data` = `PagedResult<ReturnListItemResponse>`:
```json
{ "id": "guid", "orderId": "guid", "deliveryId": "guid", "type": "Refund",
  "status": "Requested", "reason": "Defective", "refundAmount": 120000,
  "itemCount": 1, "createdAt": "..." }
```

## GET `/api/returns/{id}`
`data` = `ReturnDetailResponse` (gồm `items`, `imageUrls`, `statusLogs`, `refund`):
```json
{
  "id": "guid", "orderId": "guid", "deliveryId": "guid", "customerId": "guid",
  "type": "Refund", "status": "ItemReceived", "reason": "Defective", "reasonDetail": "...",
  "refundAmount": 120000, "refundMethod": "Original",
  "bankAccountName": null, "bankAccountNumber": null, "bankName": null,
  "returnTrackingCode": "...", "approvedAt": "...", "rejectedReason": null,
  "receivedAt": "...", "replacementDeliveryId": null, "createdAt": "...",
  "items": [{ "id": "guid", "orderItemId": "guid", "productName": "...",
              "quantity": 1, "unitPrice": 120000, "lineTotal": 120000, "exchangeProductItemId": null }],
  "imageUrls": ["https://..."],
  "statusLogs": [{ "fromStatus": "Requested", "toStatus": "Approved", "note": null, "changedAt": "..." }],
  "refund": { "id": "guid", "amount": 120000, "method": "Original", "status": "Pending",
              "providerRefundId": null, "processedAt": null, "completedAt": null }
}
```

---

## Hành động (customer)

- **POST `/{id}/cancel`** — hủy yêu cầu.
- **POST `/{id}/ship-back`** — body `ShipBackRequest`: `{ "trackingCode": "..." }`.
- **POST `/{id}/images`** — `multipart/form-data`, field `files` (chọn nhiều tệp). Chỉ chủ yêu cầu.
- **DELETE `/{id}/images/{imageId}`** — xóa ảnh, chỉ khi còn chờ duyệt.

## Hành động (vendor/admin)

- **POST `/{id}/approve`** — body `ApproveReturnRequest`: `{ "note": "..." }`.
- **POST `/{id}/reject`** — body `RejectReturnRequest`: `{ "reason": "..." }` (bắt buộc).
- **POST `/{id}/receive`** — xác nhận đã nhận hàng trả (không body).
- **POST `/{id}/resolve`** — body `ResolveReturnRequest`: `{ "restock": true, "note": "..." }`.
- **POST `/{id}/complete-refund`** (AdminOnly) — xác nhận hoàn tiền hoàn tất.

Trạng thái: xem [Appendix → ReturnRequestStatus / ReturnType / ReturnReason](./99-appendix-models.md).

---

[← Payments](./10-payments.md) · [Tiếp: Shipping →](./12-shipping.md)
