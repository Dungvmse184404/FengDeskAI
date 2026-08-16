# 11 — Returns / Refunds / Exchanges

[← Mục lục](./README.md)

Nguồn hiện thực: `ReturnsController`, `RefundsController` và `DevRefundsController`.
Mọi transition không hợp lệ trả `409 Conflict`.

## State machine ReturnRequest

```text
Requested
  ├─ request-more-evidence → NeedMoreEvidence
  │    ├─ resubmit-evidence → Requested
  │    └─ quá deadline → Rejected
  ├─ cancel → Cancelled
  └─ accept → UnderReview
       ├─ PlantHealth → Reviewing
       └─ WrongItem / DamagedPackage / NotAsDescribed → ReturnInTransit
            └─ ship-back + confirm-received → ItemReceived → Reviewing

Reviewing
  ├─ reject → Rejected
  ├─ approve-refund → Refunding → refund Completed → Completed
  └─ approve-exchange → Exchanging
       ├─ replacement hết hàng → Refunding
       └─ replacement delivery Delivered → Completed
```

`UnderReview` và `ItemReceived` là transition trung gian được ghi trong `statusLogs`;
response của `accept`/`confirm-received` trả trạng thái sau khi route xong.

## Endpoint ReturnRequest

| Method | Path | Actor | Điều kiện / kết quả |
|---|---|---|---|
| POST | `/api/returns` | Customer | Tạo ticket `Requested`, trong 7 ngày từ `DeliveredAt`, bắt buộc evidence |
| GET | `/api/returns/mine` | Customer | Danh sách ticket của caller |
| GET | `/api/returns/{id}` | Customer / store member / platform Staff+ | Chi tiết ticket |
| POST | `/api/returns/{id}/cancel` | Customer | Chỉ từ `Requested` |
| POST | `/api/returns/{id}/resubmit-evidence` | Customer | `NeedMoreEvidence → Requested`, multipart field `files` |
| POST | `/api/returns/{id}/ship-back` | Customer | Ghi mã vận đơn khi `ReturnInTransit` |
| POST | `/api/returns/{id}/images` | Customer | Thêm evidence khi `Requested/NeedMoreEvidence` |
| DELETE | `/api/returns/{id}/images/{imageId}` | Customer | Xóa evidence khi `Requested/NeedMoreEvidence` |
| GET | `/api/returns/stores/{storeId}` | Owner / accepted store staff / platform Staff+ | Ticket của store |
| POST | `/api/returns/{id}/vendor-acknowledge` | Owner / accepted store staff / Admin | Phản hồi trong SLA, không quyết định ticket |
| POST | `/api/returns/{id}/vendor-dispute` | Owner / accepted store staff / Admin | Phản đối trong SLA, không chặn Staff |
| POST | `/api/returns/{id}/confirm-received` | Owner / accepted store staff / Admin | Cần có tracking; `ReturnInTransit → ItemReceived → Reviewing` |
| GET | `/api/returns/pending` | Staff / Manager / Admin | Queue `Requested/UnderReview/Reviewing` |
| GET | `/api/returns/all` | Staff / Manager / Admin | Tất cả ticket |
| POST | `/api/returns/{id}/accept` | Staff / Manager / Admin | Tiếp nhận và tự route theo `reason` |
| POST | `/api/returns/{id}/request-more-evidence` | Staff / Manager / Admin | `Requested → NeedMoreEvidence` |
| POST | `/api/returns/{id}/approve-refund` | Staff / Manager / Admin | `Reviewing → Refunding`, tạo refund `Pending` |
| POST | `/api/returns/{id}/approve-exchange` | Staff / Manager / Admin | `Reviewing → Exchanging`; hoàn tất khi delivery thay thế Delivered |
| POST | `/api/returns/{id}/reject` | Staff / Manager / Admin | `Reviewing/NeedMoreEvidence → Rejected` |

### Khai báo gửi trả

```http
POST /api/returns/{returnId}/ship-back
Content-Type: application/json

{
  "trackingCode": "GHN-RETURN-123456"
}
```

Sau đó vendor gọi `POST /api/returns/{returnId}/confirm-received`. Backend từ chối xác
nhận nếu customer chưa khai báo tracking code.

### Duyệt hoàn tiền

```http
POST /api/returns/{returnId}/approve-refund
Content-Type: application/json

{
  "restock": true,
  "note": "Đã kiểm tra hàng"
}
```

Response có `status = Refunding` và `refund.status = Pending`. Worker gửi refund sang
gateway, chuyển `Pending → Processing`; webhook thành công chuyển refund và ticket sang
`Completed`. `Processing` không có webhook trong 30 phút chuyển `Failed` để retry.

### Duyệt đổi hàng

```http
POST /api/returns/{returnId}/approve-exchange
Content-Type: application/json

{
  "restock": true,
  "note": "Đồng ý đổi sản phẩm"
}
```

Response giữ `status = Exchanging` và trả cả `replacementDeliveryId` lẫn
`replacementDelivery` (`status`, provider, tracking, trackingUrl, ETA). Carrier webhook
hoặc cập nhật delivery thủ công sang `Delivered` sẽ tự chuyển ticket sang `Completed`.
Nếu tạo shipment ban đầu lỗi, delivery vẫn được giữ ở `Pending` để vendor xác nhận và gọi
`POST /api/orders/deliveries/{replacementDeliveryId}/shipment` thử lại.

Customer được xem delivery thay thế bằng:

```http
GET /api/orders/deliveries/{replacementDeliveryId}/detail
GET /api/shipping/deliveries/{replacementDeliveryId}/progress
```

## Refund sub-saga

```text
Pending
  ├─ Manager cancel fraud → Cancelled; ReturnRequest → Rejected
  └─ worker dispatch → Processing
       ├─ webhook success → Completed; ReturnRequest → Completed nếu đang Refunding
       └─ webhook error/timeout → Failed
            ├─ retry (tối đa 3) → Processing
            └─ hết retry → ManagerReview
                 ├─ retry → Processing
                 └─ manager-confirm → Completed
```

| Method | Path | Actor | Mô tả |
|---|---|---|---|
| POST | `/api/refunds/payos/webhook` | Anonymous + chữ ký PayOS | Callback idempotent |
| GET | `/api/refunds` | Manager / Admin | Danh sách `Failed/ManagerReview` |
| GET | `/api/refunds/{id}` | Manager / Admin | Chi tiết refund |
| POST | `/api/refunds/{id}/retry` | Manager / Admin | Retry `Failed/ManagerReview` |
| POST | `/api/refunds/{id}/manager-confirm` | Manager / Admin | Hoàn thủ công từ `ManagerReview`, bắt buộc reason/evidence |
| POST | `/api/refunds/{id}/manager-cancel` | Manager / Admin | Chỉ từ `Pending`, đồng thời reject ticket do fraud |

Development-only, ngoài Development trả `404`:

```http
POST /api/dev/refunds/{refundId}/success
POST /api/dev/refunds/{refundId}/failed
```

Hai endpoint dev yêu cầu role Admin và đi qua cùng nghiệp vụ hoàn tất/thất bại với webhook.

## Contract FE theo trạng thái

| Status | Customer action | Vendor action | Staff action |
|---|---|---|---|
| `Requested` | Cancel | — | Accept / request evidence |
| `NeedMoreEvidence` | Resubmit evidence | — | Theo dõi deadline / reject |
| `ReturnInTransit` | Ship back nếu chưa có tracking | Confirm received khi đã có tracking | Chỉ theo dõi |
| `Reviewing` | Theo dõi | Acknowledge/dispute nếu còn SLA | Approve refund/exchange hoặc reject |
| `Refunding` | Theo dõi nested `refund.status` | — | Theo dõi refund saga |
| `Exchanging` | Theo dõi `replacementDelivery` | Xử lý shipment nếu Pending | Theo dõi delivery thay thế |
| `Completed/Cancelled/Rejected` | Chỉ đọc | Chỉ đọc | Chỉ đọc |

[← Payments](./10-payments.md) · [Tiếp: Shipping →](./12-shipping.md)
