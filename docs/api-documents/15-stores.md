# 15 — Stores

[← Mục lục](./README.md)

Controller: `StoresController` · Route gốc: `/api/stores` · Mặc định `[Authorize]`; **list/detail Public**.

Quản lý garden store (marketplace). User đã đăng nhập tự mở store (self-service → thành owner chính + được cấp role `GardenOwner`). Owner/Admin sửa store, địa chỉ, đồng sở hữu, phân công nhân viên. Quyền sở hữu kiểm ở service layer.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/stores` | Public | Store đang hoạt động |
| GET | `/api/stores/mine` | Authenticated | Store tôi đồng sở hữu |
| GET | `/api/stores/{id}` | Public | Chi tiết store |
| POST | `/api/stores` | Authenticated | Tự mở store |
| PUT | `/api/stores/{id}` | Owner/Admin | Cập nhật store |
| DELETE | `/api/stores/{id}` | Owner/Admin | Xóa (mềm) store |
| DELETE | `/api/stores/{id}/hard` | StaffOrAbove | Xóa vĩnh viễn store |
| POST | `/api/stores/{id}/address` | Owner/Admin | Thêm địa chỉ store |
| PUT | `/api/stores/{id}/address` | Owner/Admin | Sửa địa chỉ store |
| DELETE | `/api/stores/{id}/address` | Owner/Admin | Xóa (mềm) địa chỉ |
| DELETE | `/api/stores/{id}/address/hard` | StaffOrAbove | Xóa vĩnh viễn địa chỉ |
| GET | `/api/stores/{id}/owners` | Authenticated | Danh sách đồng sở hữu |
| POST | `/api/stores/{id}/owners` | Owner/Admin | Thêm đồng sở hữu |
| DELETE | `/api/stores/{id}/owners/{userId}` | Owner/Admin | Gỡ đồng sở hữu |
| GET | `/api/stores/{id}/staff` | Owner/Admin | Danh sách nhân viên + lời mời chưa phản hồi |
| POST | `/api/stores/{id}/staff` | Owner/Admin | **Mời** nhân viên (Pending + Notification) |
| DELETE | `/api/stores/{id}/staff/{assignmentId}` | Owner/Admin | Gỡ / huỷ lời mời (→ `Revoked`) |
| GET | `/api/stores/staff/invitations/mine` | Authenticated | Lời mời Pending gửi cho tôi |
| POST | `/api/stores/staff/{assignmentId}/accept` | Người được mời | Đồng ý (→ `Accepted`) |
| POST | `/api/stores/staff/{assignmentId}/reject` | Người được mời | Từ chối (→ `Rejected`) |
| GET | `/api/stores/{id}/statistics` | Owner/Admin (staff **không**) | Thống kê store — xem mục dưới |

---

## GET `/api/stores` · `/mine` · `/{id}`

> **`/mine`** trả store mà user là **owner HOẶC garden staff đã `Accepted`** (nguồn sự thật cho quyền vào khu người bán). Mỗi item có thêm `isOwner`: `true` = owner, `false` = chỉ là nhân viên (dùng để FE ẩn nút owner-only). `/` và `/{id}` không set `isOwner`.

`data` = `StoreResponse` (hoặc mảng):
```json
{
  "id": "guid", "name": "Vườn Xanh", "description": "...", "hotline": "1900...",
  "openingHours": "8:00-21:00", "isActive": true, "isOwner": true,
  "address": { "id": "guid", "storeId": "guid", "wardId": "guid",
               "streetAddress": "...", "latitude": null, "longitude": null, "isActive": true },
  "owners": [{ "ownerUserId": "guid", "isPrimary": true, "assignedAt": "..." }],
  "createdAt": "...", "updatedAt": "..."
}
```

## POST `/api/stores`
Tự mở store; người tạo thành owner chính. **Request body** (`CreateStoreRequest`):
```json
{ "name": "Vườn Xanh", "description": "...", "hotline": "1900xxxx", "openingHours": "8:00-21:00" }
```

## PUT `/api/stores/{id}`
**Request body** (`UpdateStoreRequest`)
```json
{ "name": "...", "description": "...", "hotline": "...", "openingHours": "...", "isActive": true }
```

## DELETE `/api/stores/{id}` · `/{id}/hard`
Xóa mềm (owner/admin) hoặc xóa vĩnh viễn (StaffOrAbove).

---

## Địa chỉ store (1-1)

**POST `/{id}/address`** — body `CreateStoreAddressRequest`:
```json
{ "wardId": "guid", "streetAddress": "...", "latitude": null, "longitude": null }
```
**PUT `/{id}/address`** — body `UpdateStoreAddressRequest` (cùng cấu trúc).
**DELETE `/{id}/address`** / **`/{id}/address/hard`** — xóa mềm / vĩnh viễn.

---

## Đồng sở hữu (owners)

**GET `/{id}/owners`** — `data` = mảng `StoreOwnerResponse`.
**POST `/{id}/owners`** — body `AddOwnerRequest`: `{ "ownerUserId": "guid" }`. Chỉ owner hiện tại hoặc Admin.
**DELETE `/{id}/owners/{userId}`** — gỡ đồng sở hữu (không gỡ owner chính).

---

## Nhân viên (staff) — invitation flow

State machine assignment:
```
Pending ──staff accept──► Accepted ──owner gỡ──► Revoked
   │
   ├──staff reject──► Rejected
   └──owner huỷ──► Revoked
```
Quyền store-scoped của staff chỉ tính khi `Status == Accepted`.

**GET `/{id}/staff`** — Owner/Admin. `data` = mảng `StaffAssignmentResponse`:
```json
[{
  "id": "guid", "gardenStoreId": "guid",
  "staffId": "guid", "staffName": "Nguyễn Văn B",
  "staffEmail": "b@example.com", "staffPhone": "0901234567",
  "invitedBy": "guid", "invitedByName": "Nguyễn Văn A",
  "status": "Pending",
  "invitedAt": "...", "respondedAt": null, "unassignedAt": null
}]
```
Trả cả `Pending` và `Accepted`. Rejected/Revoked ẩn để list gọn.

**POST `/{id}/staff`** — Owner/Admin. Mời nhân viên (FE lấy `staffId` từ `GET /api/users/search`):
```json
{ "staffId": "guid" }
```
Vẫn chấp nhận `{ "staffEmail": "..." }` cho client cũ. BE tạo Pending + push Notification (`StaffInvited`, `ReferenceType.StaffInvitation`, `ReferenceId = assignment.id`).

Lỗi: `400 StaffNotFound` (user không tồn tại), `400 IdentifierRequired` (không gửi cả hai), `400 CannotInviteOwner`, `409 AlreadyInvited` (đang Pending), `409 AlreadyAssigned` (đang Accepted).

**DELETE `/{id}/staff/{assignmentId}`** — Owner/Admin. Gỡ hoặc huỷ lời mời (Pending/Accepted → `Revoked`).

### Lời mời (góc nhìn người được mời)

**GET `/api/stores/staff/invitations/mine`** — Authenticated. `data` = mảng `InvitationResponse`:
```json
[{
  "id": "guid", "gardenStoreId": "guid", "storeName": "Vườn Xanh",
  "invitedBy": "guid", "invitedByName": "Nguyễn Văn A",
  "status": "Pending", "invitedAt": "..."
}]
```

**POST `/api/stores/staff/{assignmentId}/accept`** — chỉ chủ lời mời (`StaffId == CurrentUserId`) và assignment đang `Pending`. Trả `StaffAssignmentResponse` mới (`Accepted`). Gửi Notification về cho owner.

**POST `/api/stores/staff/{assignmentId}/reject`** — tương tự. Chuyển `Rejected`. Gửi Notification về owner.

Lỗi: `404 InvitationNotFound`, `409 InvitationNotPending`.

---

## GET `/api/stores/{id}/statistics`

Owner (chính/đồng sở hữu) hoặc Admin; staff Accepted vẫn `403`.

| Query | Kiểu | Mặc định | Ý nghĩa |
|---|---|---|---|
| `range` | `week` \| `month` \| `quarter` \| `year` | `month` | Mốc chia cột của `revenueSeries`. Giá trị lạ → `month` (không lỗi) |

Mốc (nhắm 7-13 cột để biểu đồ đọc được): `week` = 7 ngày gần nhất · `month` = **nửa tuần một cột** (khối
3-4 ngày, ~9 cột — chia theo ngày thì 30 cột chen chúc, theo tuần thì chỉ 4 cột) · `quarter` = từng tuần
(bắt đầu Thứ Hai) của quý hiện tại, ~13 cột · `year` = 12 tháng của năm nay. **Mốc rỗng vẫn được trả về**
(revenue 0) — bỏ mốc rỗng thì biểu đồ co lại và đọc như "hôm nào cũng có đơn".

```json
{
  "totalRevenue": 1250000,          // Σ Subtotal các delivery Delivered
  "totalShippingFee": 90000,        // Σ ShippingFee các delivery Delivered
  "totalDeliveries": 7,
  "deliveriesByStatus": { "Pending": 2, "Shipped": 1, "Delivered": 4 },
  "activeDeliveries": 3,            // Pending + Confirmed + Preparing + Shipped — đã có đơn, chưa tới tay khách
  "activeDeliveriesValue": 640000,  // Σ Subtotal của các delivery đang xử lý (doanh thu sắp về)
  "awaitingPaymentOrders": 1,       // đơn PayOS khách đã đặt nhưng CHƯA trả tiền có hàng của store
  "awaitingPaymentValue": 430000,   // tiền chưa thu: đơn PayOS chưa trả + đơn COD đang trên đường

  // Sản phẩm trong đơn, gộp theo (SẢN PHẨM × TRẠNG THÁI), top 10 mỗi trạng thái theo giá trị
  "itemsByStatus": [
    { "productId": "guid", "productName": "Vòng tay thạch anh", "status": "Ordered", "quantity": 1, "value": 300000, "orderCount": 1, "shippingFee": 0 },
    { "productId": "guid", "productName": "Tượng Tỳ Hưu đồng",  "status": "Paid",    "quantity": 2, "value": 640000, "orderCount": 2, "shippingFee": 30000 }
  ],
  // Tổng phí ship theo trạng thái (dòng sản phẩm đã có cột `shippingFee` phân bổ riêng)
  "shippingFeeByStatus": { "Ordered": 0, "Paid": 30000, "Completed": 60000, "Refunded": 0 },

  // Đối soát (PayoutPolicy.HoldDays)
  "payoutHoldDays": 7,
  "availableForPayoutValue": 900000,    // đã giao và qua hết khoảng giữ
  "pendingClearanceValue": 250000,      // đã giao nhưng chưa đủ ngày
  "outstandingLiabilityValue": 0,       // công nợ chưa miễn, trừ vào kỳ chi kế tiếp

  "productCount": 12,
  "staffCount": 1,
  "revenueByMonth": [ { "year": 2026, "month": 9, "revenue": 1250000, "deliveredCount": 4 } ],  // giữ cho client cũ
  "range": "month",
  "revenueSeries": [ {
    "start": "2026-09-01T00:00:00Z", "labelVi": "01/09",
    "revenue": 250000, "deliveredCount": 1,      // = completed/completedCount, giữ tên cũ cho client cũ
    "awaitingPayment": 300000, "awaitingPaymentCount": 1,  // đặt trong mốc, CHƯA trả tiền
    "inProgress": 640000, "inProgressCount": 2,            // đã trả/COD, đơn giao tạo trong mốc, chưa xong
    "completed": 250000, "completedCount": 1,              // giao tới tay khách trong mốc
    "refunded": 0, "refundedCount": 0                      // RMA hoàn tiền xong trong mốc
  } ]
}
```

**Bốn lớp của một cột** xếp theo *mức chắc chắn của tiền*, mỗi lớp bucket theo mốc thời gian RIÊNG:

| Lớp | Nguồn | Mốc tính theo | Ý nghĩa |
|---|---|---|---|
| `awaitingPayment` | order `Pending` + PayOS chưa có delivery, **và delivery COD đang chạy** | `order.createdAt` / `delivery.createdAt` | chưa thu được đồng nào |
| `inProgress` | delivery `Pending/Confirmed/Preparing/Shipped` của đơn **PayOS đã trả** | `delivery.createdAt` | tiền đã vào, còn việc phải làm |
| `completed` | delivery `Delivered` | `deliveredAt ?? createdAt` | tiền chắc chắn |
| `refunded` | `refunds.status = Completed`, join ticket RMA → delivery của store | `completedAt ?? createdAt` | tiền chảy ngược |

⚠️ Vì mỗi lớp có mốc riêng, **tổng một cột KHÔNG phải doanh thu của mốc đó** mà là "tiền phát sinh ở trạng
thái nào trong mốc đó". Cùng một đơn sẽ lần lượt xuất hiện ở lớp khác nhau tại các mốc khác nhau — đó là
ý đồ: chủ vườn nhìn thấy dòng tiền đang đi tới đâu, không phải một con số tổng đã được làm phẳng.

Vì sao có hai nhóm "chưa hoàn thành" riêng (2026-09-22): delivery chỉ được tạo khi tiền về (COD: lúc checkout;
PayOS: lúc webhook). Đơn online chưa thanh toán vì thế **không có delivery** và không nằm trong
`deliveriesByStatus` — không đếm riêng thì store không hề biết có đơn đang treo. `Expired`/`Cancelled` không tính.

⚠️ **COD không phải "đã thanh toán"** (sửa 24/09/2026): COD thu tiền tại điểm giao, nên delivery COD đang
chạy xếp vào lớp `Ordered`/`awaitingPayment` chứ không phải `Paid`; chỉ khi `Delivered` mới coi là đã thu.
`activeDeliveries` thì vẫn đếm CẢ HAI — nó trả lời "còn bao nhiêu việc phải làm", không phải "tiền ở đâu".
Ở tầng đơn hàng, BE **chưa bao giờ** đặt `OrderStatus.Paid` cho COD: đơn COD đi thẳng `Pending` → rollup
theo delivery → `Completed`; chỉ webhook PayOS mới đặt `Paid`.

### Đối soát tiền hàng (2026-09-23)

`PayoutPolicy.HoldDays` (Application/Features/Vendor/Services) = **7 ngày** giữ tiền tính từ `DeliveredAt`,
đặt **bằng đúng** cửa sổ đổi trả `ReturnWorkflow.ReturnWindowDays` để tiền chỉ rời sàn sau khi khách hết
quyền mở ticket. Tiền của một delivery `Delivered` đi vào `pendingClearanceValue` trước, qua đủ ngày mới
sang `availableForPayoutValue`. `outstandingLiabilityValue` = Σ `vendor_liabilities` chưa `Waived`.

🔕 **Việc cộng tiền vào số dư đang TẮT** (`PayoutPolicy.CreditToBalanceEnabled = false`, 24/09/2026).
`PayoutCreditService` vẫn còn nguyên nhưng thoát ngay ở đầu hàm; `users.balance` không được ghi nữa.

Lý do tắt — ba lỗi nghiệp vụ chưa sửa:
1. đơn đã cộng vào số dư **vẫn** nằm trong `availableForPayoutValue` (hàm này tính lại từ `deliveries`, không
   đọc `payout_credited_at`) ⇒ một khoản tiền hiện ở hai nơi;
2. `VendorLiability` (hoàn hàng) **không** trừ khỏi số dư ⇒ đơn bị trả sau khi đã cộng là sàn mất phần đã ứng;
3. không có khoá ⇒ chạy nhiều instance sẽ cộng đôi.

Các trường `payoutHoldDays` / `availableForPayoutValue` / `pendingClearanceValue` **vẫn trả bình thường**
(chỉ là số tính toán), nhưng giao diện đã ẩn thẻ "Có thể rút" cho tới khi luồng chi tiền được làm đúng —
xem [`docs/adr/vendor-payout.md`](../adr/vendor-payout.md).

---

[← Locations](./14-locations.md) · [Tiếp: Workspace Profiles →](./16-workspace-profiles.md)
