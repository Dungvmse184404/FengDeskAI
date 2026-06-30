# Yêu cầu sửa chữa: Tách bước "Tạo vận đơn" ra khỏi thanh toán

> Mục tiêu: Sau khi thanh toán **không** tạo đơn ship ngay (garden owner cần thời gian chuẩn bị hàng).
> Đơn chỉ dừng ở **Paid**. Garden owner xem đơn → bấm **Nhận** (order → Processing) → bấm **Tạo đơn ship**
> (mới gọi nhà vận chuyển GHN/AhaMove).

## Bối cảnh hiện tại (đã đọc code)

Luồng PayOS hiện gộp **mọi thứ** vào `PaymentService.ApplyPaymentSuccessAsync` ngay khi webhook báo trả tiền:
transaction → Paid, order → Paid, tạo delivery, **gọi `CreateShipmentsAsync` (tạo vận đơn ngay)**, rollup order → Processing.

Đáng chú ý — **hệ thống đã có sẵn các mảnh ghép** cho luồng mong muốn, chỉ chưa tách bước:

| Thành phần đã có | Vị trí | Vai trò |
|---|---|---|
| Màn hình garden owner xem đơn của store | `GET /api/orders/stores/{storeId}/deliveries` → `OrderService.GetStoreDeliveriesAsync` | Đã có |
| Đổi trạng thái delivery (có check quyền owner/staff) | `PATCH /api/orders/deliveries/{deliveryId}/status` → `OrderService.UpdateDeliveryStatusAsync` | Đã có |
| Tạo vận đơn cho **một** delivery | `OrderService.CreateShipmentForDeliveryAsync` (private) | Đã có — đang dùng cho COD |

> Hiện COD đã đúng tinh thần "tạo ship khi store xác nhận": trong `UpdateDeliveryStatusAsync`, khi chuyển delivery
> sang `Confirmed` mà chưa có `ProviderOrderId` thì tự gọi `CreateShipmentForDeliveryAsync` (OrderService dòng ~205).
> Vấn đề: bước **Nhận** và **Tạo ship** đang **dính làm một**. Ta cần **tách** chúng ra cho cả COD lẫn PayOS.

## Quyết định thiết kế (đề xuất)

1. **Thanh toán PayOS thành công → order = `Paid`, delivery = `Pending`.** Không tạo ship, không nhảy `Processing`.
2. **Một luồng chung cho COD và PayOS** ở phía garden owner (2 nút bấm):
   - **Nhận đơn**: delivery `Pending → Confirmed`. Rollup order → `Processing`. *Chưa* tạo vận đơn.
   - **Tạo đơn ship**: delivery `Confirmed → Preparing`. Mới gọi GHN/AhaMove, lưu tracking.
3. **Ý nghĩa `DeliveryStatus` (không đổi enum, chỉ chỉnh cách dùng):**

   | Status | Ý nghĩa mới | Ai đẩy |
   |---|---|---|
   | `Pending` | Chờ store nhận (sau thanh toán / sau đặt COD) | hệ thống |
   | `Confirmed` | Store đã **Nhận**, chưa có vận đơn | nút "Nhận" |
   | `Preparing` | Đã **tạo vận đơn** (có tracking), chờ shipper lấy | nút "Tạo ship" |
   | `Shipped`…`Delivered` | webhook nhà vận chuyển | GHN/AhaMove |

> `OrderWorkflow.IsValidDeliveryTransition` đã cho phép `Pending→Confirmed` và `Confirmed→Preparing` → **không cần sửa**.
> `OrderWorkflow.ComputeOrderStatus` đã map `Confirmed`/`Preparing` → `Processing` → **không cần sửa**.

---

## A. APPLICATION — `PaymentService.cs`

| Mục | Thay đổi |
|---|---|
| `ApplyPaymentSuccessAsync` | **Bỏ** lời gọi `await CreateShipmentsAsync(order, now, ct);` (dòng ~308). **Bỏ** khối rollup `ComputeOrderStatus` + gán `order.Status = rolled` (dòng ~310–315) để order **giữ nguyên `Paid`**. Vẫn giữ: txn→Paid, order→Paid, tạo delivery (`CreateAndLinkDeliveriesAsync`), trừ-lại-kho-nếu-Expired, khôi phục delivery bị huỷ, notification "Thanh toán thành công". |
| Method `CreateShipmentsAsync` (dòng ~369–422) | **Xoá** — logic tạo vận đơn theo từng delivery đã có ở `OrderService.CreateShipmentForDeliveryAsync`. |
| Constructor + field `IShippingProvider _shipping` | **Bỏ** (sau khi xoá `CreateShipmentsAsync` thì PaymentService không còn dùng `_shipping`). Cập nhật `DependencyInjection` nếu cần. |
| `SimulatePaidAsync` (dev mark-paid) | Không sửa code, nhưng **lưu ý**: nay nó chỉ đưa đơn về `Paid`. Khi test luồng giao, tester phải bấm Nhận + Tạo ship như thật. |

> Sau thay đổi, đơn đã trả tiền nằm `Paid` với các delivery `Pending` — đúng cái garden owner sẽ thấy.

---

## B. APPLICATION — `OrderService.cs`

| Mục | Thay đổi |
|---|---|
| `UpdateDeliveryStatusAsync` | **Bỏ** khối tự tạo ship khi `Confirmed` (dòng ~203–206: `if (request.Status == Confirmed && ProviderOrderId rỗng) await CreateShipmentForDeliveryAsync(...)`). Giờ "Nhận" (→Confirmed) **chỉ** đổi trạng thái + rollup + notify, **không** gọi nhà vận chuyển. |
| **Thêm method** `CreateDeliveryShipmentAsync(Guid deliveryId, Guid userId, bool isAdmin, ct)` | Bước "Tạo đơn ship". Trình tự: ① load delivery (kèm Order + Items + ProductItem dims — xem mục D); ② check quyền `Stores.CanManageAsync(delivery.GardenStoreId, userId)` (hoặc isAdmin), sai → `Forbidden`; ③ nếu `delivery.Status != Confirmed` → `BadRequest` (DeliveryNotConfirmed); ④ nếu đã có `ProviderOrderId` → `BadRequest` (ShipmentAlreadyCreated); ⑤ trong `ExecuteInTransactionAsync`: gọi `CreateShipmentForDeliveryAsync`, set `delivery.Status = Preparing`, ghi `DeliveryProgressLog` (`Confirmed→Preparing`, note "Tạo vận đơn …"), notify khách "Đang chuẩn bị hàng"; ⑥ trả `DeliveryResponse`. |
| `CreateShipmentForDeliveryAsync` | Bỏ `AddProgressLogAsync` bên trong nó (hiện log `Pending→Confirmed` — sai ngữ cảnh mới). Để **caller** (`CreateDeliveryShipmentAsync`) tự ghi log `Confirmed→Preparing`. Method chỉ còn: gọi provider + gán tracking vào delivery. |

---

## C. APPLICATION — Interface `IOrderService.cs`

Thêm khai báo:

```
Task<IServiceResult<DeliveryResponse>> CreateDeliveryShipmentAsync(
    Guid deliveryId, Guid userId, bool isAdmin, CancellationToken ct = default);
```

---

## D. INFRASTRUCTURE — Repository (nạp dữ liệu)

| File | Thay đổi |
|---|---|
| `IOrderRepository` / `OrderRepository` (hoặc `ShippingRepository`) | Đảm bảo có method nạp delivery **kèm** `Order` (cần `PaymentMethod`, `ShippingAddressId`) + `Items` + `Items.ProductItem` (cần `WeightGram`, `LengthCm`, `WidthCm`, `HeightCm`). `CreateShipmentForDeliveryAsync` dùng các trường này. Kiểm tra `GetDeliveryWithOrderAsync` đã include `Items.ProductItem` chưa; nếu chưa → thêm `GetDeliveryWithItemsAsync` (Include Order + Items.ProductItem). |

> Không có method nào sai sẽ ném runtime null/lazy-load. Kiểm tra kỹ Include trước khi gọi provider.

---

## E. WEBAPI — `OrdersController.cs`

| Mục | Thay đổi |
|---|---|
| Nút **Nhận đơn** | **Tái dùng** endpoint sẵn có: `PATCH /api/orders/deliveries/{deliveryId}/status` với body `{ "status": "Confirmed" }`. Không cần endpoint mới. |
| Nút **Tạo đơn ship** | **Thêm** `POST /api/orders/deliveries/{deliveryId:guid}/shipment` → gọi `_service.CreateDeliveryShipmentAsync(deliveryId, CurrentUserId, IsAdmin, ct)`. |

```csharp
/// <summary>Garden owner tạo vận đơn (gọi GHN/AhaMove) cho delivery đã ở trạng thái Confirmed.</summary>
[HttpPost("deliveries/{deliveryId:guid}/shipment")]
public async Task<IActionResult> CreateShipment(Guid deliveryId, CancellationToken ct)
    => ToActionResult(await _service.CreateDeliveryShipmentAsync(deliveryId, CurrentUserId, IsAdmin, ct));
```

---

## F. APPLICATION — `ApiStatusMessages` (Constants)

Thêm message cho nhóm `Order` (hoặc `Shipping`):

| Key | Nội dung |
|---|---|
| `DeliveryNotConfirmed` | "Chỉ tạo vận đơn khi đơn giao đã được xác nhận (Nhận đơn)." |
| `ShipmentAlreadyCreated` | "Đơn giao này đã có vận đơn." |
| `ShipmentCreated` | "Đã tạo vận đơn thành công." |

---

## G. Luồng mới (end-to-end)

**PayOS (online):**
1. Webhook PayOS báo trả tiền → `ApplyPaymentSuccessAsync`: txn `Paid`, order `Paid`, tạo delivery `Pending`. (Dừng ở đây.)
2. Garden owner mở màn hình store → `GET /api/orders/stores/{storeId}/deliveries` thấy delivery `Pending`.
3. Bấm **Nhận** → `PATCH .../status {Confirmed}` → delivery `Confirmed`, order rollup → `Processing`.
4. Chuẩn bị hàng xong, bấm **Tạo đơn ship** → `POST .../shipment` → gọi GHN/AhaMove → delivery `Preparing` + tracking.
5. Webhook nhà vận chuyển đẩy tiếp `Shipped → Delivered`. Khi tất cả delivery `Delivered` → order `Completed`.

**COD:** giống hệt từ bước 2 (delivery `Pending` đã tạo lúc đặt hàng). Nhờ tách bước, COD cũng phải bấm **Nhận** rồi **Tạo ship** tách biệt — không còn tự tạo ship khi Confirmed.

---

## H. Câu hỏi nghiệp vụ cần chốt (chưa xử lý trong scope này)

1. **Đơn `Paid` mà garden owner không bao giờ Nhận** → delivery kẹt `Pending` mãi. Có cần SLA + auto-huỷ + hoàn tiền không? (`OrderExpirationWorker` hiện chỉ xử lý đơn `Pending` chưa trả tiền, không đụng đơn `Paid`.)
2. **Nhận rồi nhưng không Tạo ship** → delivery kẹt `Confirmed`. Có cần nhắc/nhắc hạn?
3. **Đơn nhiều store**: order → `Processing` ngay khi **delivery đầu tiên** được Nhận (đúng theo `ComputeOrderStatus`). Xác nhận đây là hành vi mong muốn.

---

## Tổng kết file bị tác động

| Loại | File |
|---|---|
| **Sửa** | `Application/Features/Payment/Services/PaymentService.cs` (bỏ tạo ship + rollup, bỏ `_shipping`) |
| **Sửa** | `Application/Features/Sales/Services/OrderService.cs` (bỏ auto-ship khi Confirmed, thêm `CreateDeliveryShipmentAsync`, gọn `CreateShipmentForDeliveryAsync`) |
| **Sửa** | `Application/Features/Sales/Services/IOrderService.cs` (thêm khai báo) |
| **Sửa** | `Infrastructure/Persistence/Repositories/OrderRepository.cs` (đảm bảo Include Order+Items+ProductItem) |
| **Sửa** | `WebAPI/Controllers/OrdersController.cs` (thêm `POST .../deliveries/{id}/shipment`) |
| **Sửa** | `Application/Common/Constants/ApiStatusMessage.cs` (3 message mới) |
| **Không đổi** | `OrderWorkflow.cs` (transition + rollup đã đúng), `PaymentsController.cs`, enums `OrderStatus`/`DeliveryStatus` |
| **Cập nhật doc** | `Documents/ard/GHN_INTEGRATION.md`, `AHAMOVE_INTEGRATION.md`, `Documents/API_GUIDE.md` (mô tả 2 bước Nhận / Tạo ship) |
