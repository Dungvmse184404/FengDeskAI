# Phụ lục — Enums & Data Models

[← Mục lục](./README.md)

Tổng hợp enum, envelope và mã trạng thái dùng chung. **Mọi enum serialize thành chuỗi** (vd `"Pending"`, `"Kim"`).

---

## Envelope `ServiceResult`

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "message": null,
  "errors": null,
  "data": { }
}
```
| Field | Kiểu | Ghi chú |
|-------|------|---------|
| `isSuccess` | bool | Thành công hay không |
| `statusCode` | int | = HTTP status |
| `message` | string? | Thông điệp (lỗi hoặc thông báo) |
| `errors` | string[]? | Danh sách lỗi chi tiết |
| `data` | T? | Dữ liệu (null khi không có / khi lỗi) |

## `PagedResult<T>`
```json
{ "items": [], "page": 1, "pageSize": 20, "totalCount": 0, "totalPages": 0 }
```

---

## Mã trạng thái (`ApiStatusCodes`)

| Code | Hằng | | Code | Hằng |
|------|------|-|------|------|
| 200 | Ok | | 404 | NotFound |
| 201 | Created | | 409 | Conflict |
| 202 | Accepted | | 422 | UnprocessableEntity |
| 204 | NoContent | | 500 | InternalServerError |
| 400 | BadRequest | | 503 | ServiceUnavailable |
| 401 | Unauthorized | | | |
| 403 | Forbidden | | | |

---

## Identity

### `UserRole` (`[Flags]`)
| Tên | Bit | | Tên | Bit |
|-----|-----|-|-----|-----|
| `None` | 0 | | `Admin` | 8 |
| `Customer` | 1 | | `GardenOwner` | 16 |
| `Manager` | 2 | | | |
| `Staff` | 4 | | | |

> 1 account có thể mang nhiều role; `role` (string) là chuỗi gộp, `roles` (array) là danh sách tách rời.

### `Gender`
`Unspecified` (0) · `Male` (1) · `Female` (2) · `Other` (3)

---

## Workspace

| Enum | Giá trị |
|------|---------|
| `LocationType` | `Home`, `Office`, `Cafe`, `Studio`, `Other` |
| `LightingType` | `Natural`, `Artificial`, `Mixed`, `Dim` |
| `DeskType` | `Sitting`, `Standing`, `StandingSitting`, `LShape`, `Corner`, `Other` |
| `CompassDirection` | `North`, `Northeast`, `East`, `Southeast`, `South`, `Southwest`, `West`, `Northwest` |
| `WorkPurpose` | `Office`, `Study`, `Creative`, `Reading`, `Gaming`, `Mixed`, `Other` |
| `FengShuiElement` | `Kim` (Metal), `Moc` (Wood), `Thuy` (Water), `Hoa` (Fire), `Tho` (Earth) |
| `WorkspaceScope` | `Private`, `Shared`, `Public` — mức riêng tư → lọc mệnh "hard"/"soft" (engine v3) |
| `ElementInputKind` | `Color`, `Material`, `Shape` — loại tín hiệu suy ra vector ngũ hành (engine v3) |

---

## Catalog

| Enum | Giá trị |
|------|---------|
| `SizeClass` | `Small`, `Medium`, `Large` |
| `Model3DStatus` | `Pending` (0), `Processing` (1), `Succeeded` (2), `Failed` (3) |

---

## Recommendation

| Enum | Giá trị |
|------|---------|
| `RecommendationStatus` | `Scored`, `Completed`, `Failed` |
| `KuaGroup` | `East` (Đông tứ mệnh), `West` (Tây tứ mệnh) |
| `FengShuiRelation` | `TuongHoa`, `TuongSinh`, `TietKhi`, `TuongKhac`, `BiKhac` |

---

## Sales

### `OrderStatus`
`Pending` (vừa tạo, chờ thanh toán) · `Paid` · `Processing` (đang chuẩn bị/giao) · `Completed` · `Cancelled` · `Expired` (quá hạn thanh toán)

### `DeliveryStatus`
`Pending` (chờ store xác nhận) · `Confirmed` · `Preparing` · `Shipped` · `Delivered` · `DeliveryFailed` · `Cancelled` · `Returned`

### `ReturnType`
`Refund` (trả & hoàn tiền) · `Exchange` (đổi sản phẩm/biến thể)

### `ReturnReason`
`Defective` · `WrongItem` · `NotAsDescribed` · `DamagedInTransit` · `ChangedMind` · `Other`

### `ReturnRequestStatus`
`Requested` → `Approved` → `ReturnInTransit` → `ItemReceived` → (`Refunding` | `Exchanging`) → `Completed`. Ngoài luồng: `Rejected`, `Cancelled`.

---

## Payment

| Enum | Giá trị |
|------|---------|
| `PaymentMethod` | `PayOS` (online), `COD` (khi nhận hàng) |
| `PaymentStatus` | `Pending`, `Paid`, `Cancelled`, `Failed`, `Expired` |
| `RefundMethod` | `Original` (về nguồn PayOS), `BankTransfer` (COD), `Manual` |
| `RefundStatus` | `Pending`, `Processing`, `Completed`, `Failed`, `Cancelled` |

---

## Shipping

| Enum | Giá trị |
|------|---------|
| `DeliverySource` | `Manual` (0), `Webhook` (1), `System` (2) |

---

## Chat

| Enum | Giá trị |
|------|---------|
| `ParticipantType` | `Customer` (0), `Staff` (1), `Manager` (2), `Admin` (3), `AiBot` (9) |
| `ParticipantRole` | `Owner` (0), `Member` (1) |
| `MessageSenderType` | `User` (0), `AiBot` (1), `System` (2) |

---

## Notification

### `NotificationType`
`OrderPlaced`, `OrderPaid`, `OrderCancelled`, `OrderCompleted`, `DeliveryConfirmed`, `DeliveryPreparing`, `DeliveryShipped`, `DeliveryDelivered`, `DeliveryReturned`, `DeliveryCancelled`, `ReturnRequested`, `ReturnApproved`, `ReturnRejected`, `ReturnReceived`, `ReturnCancelled`, `RefundCompleted`, `ExchangeShipped`, `SystemAlert`

### `ReferenceType`
`None` (0) · `Order` (1) · `Delivery` (2) · `Payment` (3) · `CustomerDesign` (4) · `Promotion` (5) · `System` (6) · `Return` (7) · `Refund` (8)

---

[← Dev Tools](./24-dev-tools.md) · [Mục lục →](./README.md)
