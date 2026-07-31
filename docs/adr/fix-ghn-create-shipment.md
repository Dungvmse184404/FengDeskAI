# Fix: Luồng "Tạo vận đơn" GHN lỗi 400 — `Lỗi lấy thông tin shop`

> Trạng thái: **Đề xuất** (chưa apply code)
> Ngày: 2026-07-31
> Liên quan: [refactor-create-shipment-flow.md](./refactor-create-shipment-flow.md) (tách bước Nhận / Tạo ship), [ghn-integration.md](./ghn-integration.md)

## 1. Triệu chứng

Garden owner bấm **Tạo đơn ship** (`POST /api/orders/deliveries/{id}/shipment`) → API trả **500** (DeveloperExceptionPage), transaction rollback:

```
POST https://dev-online-gateway.ghn.vn/shiip/public-api/v2/shipping-order/create → 400
[GHN] tạo vận đơn delivery f8f3577c-… lỗi 400:
{"code":400,"message":"Lỗi lấy thông tin shop","data":null,"code_message":"SERVER_ERR_COMMON"}
UnitOfWork: ExecuteInTransactionAsync rollback do exception
System.Net.Http.HttpRequestException: GHN tạo vận đơn delivery f8f3577c-… thất bại (400).
   at GhnShippingProvider.PostAsync(…) GhnShippingProvider.cs:line 101
   at OrderService.CreateShipmentForDeliveryAsync(…) OrderService.cs:line 481
   at UnitOfWork.ExecuteInTransactionAsync(…) UnitOfWork.cs:line 108
   → DeveloperExceptionPageMiddleware: An unhandled exception has occurred
```

## 2. Chẩn đoán — nguyên nhân gốc

`Ghn:DefaultShopId` đang được set bằng **`client_id`**, không phải **ShopId**.

Gọi `POST /shiip/public-api/v2/shop/all` với đúng token đang cấu hình:

```json
{"_id": 200891, "name": "0772706428", "client_id": 2511847,
 "ward_code": "", "district_id": 0, "status": 1}
```

→ `2511847` là **client_id** của tài khoản; **ShopId thật là `200891`**. Kiểm chứng bằng `/shipping-order/fee` (read-only, cùng payload, chỉ đổi header `ShopId`):

| Header `ShopId` | Kết quả |
|---|---|
| `2511847` (đang cấu hình) | `400 — Lỗi lấy thông tin shop` — đúng lỗi production |
| `200891` (ShopId thật) | `200 Success`, `total: 20900` |

Mà **tất cả** `garden_stores.ghn_shop_id` đang `NULL`:

| Store | `ghn_shop_id` | `ghn_ward_code` | `sender_name/phone` |
|---|---|---|---|
| Vườn Phong Thủy Demo | null | 22205 | null / null |
| Nguyen Quoc Viet | null | 21210 | null / null |
| shop của tui | null | 20102 | null / null |
| Shop của Cat | null | 22401 | null / null |
| Vườn của dũng | null | **null** | null / null |

Nên `GhnShippingProvider.ResolveShopId` ([GhnShippingProvider.cs:82](../../src/FengDeskAI.Infrastructure/ExternalServices/Shipping/GhnShippingProvider.cs)) luôn rơi về `DefaultShopId` sai → **mọi** lần tạo vận đơn đều 400. Đây chính là mục checklist chưa tick trong [ghn-integration.md](./ghn-integration.md) — *"Set each active store's `GhnShopId` (+ `GhnServiceTypeId`) và `SenderPhone`"*.

> **Vì sao checkout vẫn chạy được?** `DeliveryFeeEstimator.EstimateAsync` bọc `EstimateFeeAsync` trong `try/catch` và fallback sang `IShippingFeeCalculator` ([DeliveryFeeEstimator.cs:41-49](../../src/FengDeskAI.Application/Features/Shipping/Services/DeliveryFeeEstimator.cs)). Lỗi ShopId đã âm thầm xảy ra ở **mọi lần checkout** từ trước tới nay (chỉ log `LogWarning`), khách bị tính phí theo calculator nội bộ thay vì giá GHN thật. Bug chỉ lộ ra ở bước tạo vận đơn vì chỗ đó không có fallback.

## 3. Các vấn đề khác lộ ra khi truy vết

| # | Vấn đề | Vị trí | Hệ quả |
|---|---|---|---|
| P1 | Gọi HTTP bên ngoài **bên trong DB transaction** | `OrderService.CreateDeliveryShipmentAsync` (~dòng 520) | Transaction giữ mở suốt thời gian round-trip mạng (giữ connection pool, khoá row); GHN chậm/timeout = transaction treo |
| P2 | Lỗi provider **thoát khỏi Result pattern** | `GhnShippingProvider.PostAsync` ném `HttpRequestException` | FE nhận **500 + stack trace**, không có mã lỗi nghiệp vụ. Trái quy ước "không ném exception cho lỗi nghiệp vụ" trong CLAUDE.md |
| P3 | **Mất message lỗi của GHN** | `PostAsync` ném `$"GHN {action} thất bại ({status})"` | Body GHN (`"Lỗi lấy thông tin shop"`) chỉ có trong log; không truyền được lý do cho garden owner |
| P4 | **Không check `code` trong body** | `SendAsync` chỉ check `dto.Data is null` | GHN có thể trả HTTP 200 kèm `code != 200` → coi như thành công sai |
| P5 | **Rủi ro vận đơn mồ côi** | `CreateDeliveryShipmentAsync` | GHN tạo đơn xong mà commit DB fail → GHN có vận đơn, DB rollback sạch, không ai biết. Đã gửi `client_order_code = delivery.Id` nhưng code chưa xử lý lỗi trùng để phục hồi |
| P6 | **Không validate dữ liệu điểm gửi trước khi gọi** | `CreateShipmentForDeliveryAsync` | Store "Vườn của dũng" không có address/ward → `from_*` toàn null, mà shop `200891` trên GHN cũng chưa có địa chỉ (`ward_code: ""`, `district_id: 0`) → vẫn lỗi dù đã sửa ShopId. Hiện chỉ validate `To*` |
| P7 | `sender_name`/`sender_phone` toàn null | bảng `stores_address` | `from_phone` rơi về `GardenStore.Hotline`; GHN bắt buộc SĐT VN hợp lệ 10 số |
| P8 | Secret nằm trong `appsettings.json` **local** (GHN Token dòng 77-82, PayOS ClientId/ApiKey/ChecksumKey dòng 54-59) | `WebAPI/appsettings.json` | **Không rò rỉ** — file bị `.gitignore` chặn (`**/appsettings.json`), chưa từng được commit (đã quét toàn bộ 79 commit trên mọi branch + mọi blob unreachable trong object db: 0 hit). Rủi ro còn lại chỉ là **chia sẻ máy/backup** và việc dev mới không có file này khi clone |

> Các điểm gọi provider (xác nhận qua codegraph — 3 call site):
> `OrderService.CreateShipmentForDeliveryAsync`, `ReturnService.CreateReplacementDeliveryAsync`, `DeliveryFeeEstimator.EstimateAsync`;
> thêm `ShippingService.RedeliverAsync` dùng `RedeliverAsync`. Mọi thay đổi contract phải cover đủ 4 chỗ.

---

## A. DATA / CONFIG — sửa để unblock ngay

| Mục | Thay đổi |
|---|---|
| `appsettings.json` → `Ghn:DefaultShopId` | `2511847` → **`200891`**. File này đã bị `.gitignore` chặn nên **không cần** dọn secret; chỉ cần đảm bảo bản deploy (Docker/Railway) truyền `Ghn__DefaultShopId=200891` qua env. |
| `garden_stores.ghn_shop_id` | Set ShopId GHN cho từng store đang hoạt động (multi-vendor thật). Giai đoạn dev có thể để null và dùng `DefaultShopId`. |
| `stores_address.sender_name` / `sender_phone` | Điền cho mọi store active; validate SĐT VN 10 số. |
| Shop `200891` trên portal GHN | Cấu hình địa chỉ lấy hàng (`ward_code`/`district_id` đang rỗng) để có fallback khi payload thiếu `from_*`. |

> **Lưu ý vận hành:** `DefaultShopId` chỉ là phao cứu sinh cho dev. Ở production, mỗi store **phải** có `ghn_shop_id` riêng, vì ShopId quyết định **địa chỉ lấy hàng** và **tài khoản đối soát COD**. Dùng chung một shop cho nhiều vendor = GHN đến sai kho và tiền COD về sai chỗ.

---

## B. INFRASTRUCTURE — `GhnShippingProvider.cs`

| Mục | Thay đổi |
|---|---|
| **Thêm** `GhnApiException` (Infrastructure, kế thừa `Exception`) | Mang `int HttpStatus`, `int GhnCode`, `string GhnMessage`, `string Action`. Thay cho `HttpRequestException` trống nghĩa. |
| `PostAsync` (dòng ~90-104) | Parse body lỗi thành `GhnResponse<object>` → ném `GhnApiException` kèm `message` gốc của GHN. Giữ nguyên `LogError`. |
| `SendAsync<T>` (dòng ~106-113) | **Thêm** check `dto.Code != 200` → ném `GhnApiException` (P4). Hiện chỉ check `Data is null`. |
| ✅ Trường số tiền | GHN trả `total_fee` lúc là **number** lúc là **string** — khai cứng `string?` làm `/shipping-order/create` ném `JsonException: Cannot get the value of a token type 'Number' as a string`. Đã thêm `FlexibleDecimalConverter` nhận cả hai, áp cho `CreateOrderData.TotalFee` và `FeeData.Total/ServiceFee`; giá trị lạ → null để caller fallback. |
| `ResolveShopId` (dòng ~82-88) | Giữ nguyên guard `shopId <= 0`, nhưng đổi thành `GhnApiException`/`ShippingConfigurationException` để Application phân biệt "lỗi cấu hình" vs "GHN từ chối". |
| `CreateShipmentAsync` | Nhận diện lỗi **trùng `client_order_code`** của GHN → trả về `ShipmentResult` của vận đơn đã tồn tại (idempotent), thay vì ném lỗi. Đây là mảnh còn thiếu để P5 tự phục hồi. |

```csharp
// PostAsync — giữ được lý do GHN từ chối
if (!res.IsSuccessStatusCode)
{
    var err = await res.Content.ReadAsStringAsync(ct);
    _logger.LogError("[GHN] {Action} lỗi {Status}: {Body}", action, (int)res.StatusCode, err);
    res.Dispose();
    throw GhnApiException.FromBody(action, (int)res.StatusCode, err);
}
```

---

## C. APPLICATION — `OrderService.cs`

| Mục | Thay đổi |
|---|---|
| `CreateDeliveryShipmentAsync` (dòng ~508-552) | **Đưa lời gọi provider ra NGOÀI transaction** (P1). Trình tự mới: ① load + check quyền/trạng thái (giữ nguyên) → ② **validate dữ liệu vận đơn** (mục D) → ③ gọi `_shipping.CreateShipmentAsync` **ngoài** transaction, bắt `GhnApiException` → `ServiceResult.Failure(BadGateway, …)` → ④ `ExecuteInTransactionAsync` chỉ ghi DB (gán tracking, `Status = Preparing`, progress log, notification). |
| `CreateShipmentForDeliveryAsync` (dòng ~475-502) | Tách làm 2: `BuildShipmentRequestAsync(delivery)` (đọc store + shipTo + items) và `ApplyShipmentResult(delivery, result)` (gán tracking). Việc gọi provider do caller làm — để caller kiểm soát ranh giới transaction. |
| **Thêm** xử lý P5 | Nếu commit DB fail sau khi GHN đã tạo đơn: log `LogCritical` kèm `ProviderOrderId` + `DeliveryId` để đối soát thủ công. Kết hợp idempotency ở mục B, lần bấm lại sẽ khớp đúng vận đơn cũ thay vì tạo đơn thứ hai. |

```csharp
// Bố cục mong muốn
var req = await BuildShipmentRequestAsync(delivery, ct);
var invalid = ShipmentRequestValidator.Validate(req);         // mục D
if (invalid is not null)
    return ServiceResult<DeliveryResponse>.Failure(ApiStatusCodes.BadRequest, invalid);

ShipmentResult shipment;
try { shipment = await _shipping.CreateShipmentAsync(req, ct); }   // NGOÀI transaction
catch (Exception ex) when (ex is GhnApiException or HttpRequestException or TaskCanceledException)
{
    _logger.LogError(ex, "[Shipment] Tạo vận đơn delivery {Id} thất bại.", delivery.Id);
    return ServiceResult<DeliveryResponse>.Failure(
        ApiStatusCodes.BadGateway, ApiStatusMessages.Order.ShipmentProviderFailed);
}

await _uow.ExecuteInTransactionAsync<object?>(async _ => { /* chỉ ghi DB */ }, ct);
```

> `GhnApiException` nằm ở Infrastructure nên Application **không** được `catch` trực tiếp (sai chiều phụ thuộc). Hai lựa chọn:
> **(a)** khai báo `ShippingProviderException` trong `Application/Interfaces/External/` và cho provider ném type đó — **khuyến nghị**;
> **(b)** đổi `IShippingProvider.CreateShipmentAsync` trả `Result<ShipmentResult>` thay vì ném. (b) sạch hơn về mặt thiết kế nhưng đụng cả 4 call site + 3 provider impl.

---

## D. APPLICATION — validate trước khi gọi provider (P6, P7) — ✅ **ĐÃ CODE**

`StoreShippingReadiness` (`Features/Shipping/Services/`) đánh giá cửa hàng **trước** khi gọi provider, trả danh sách mục còn thiếu có mã ổn định để FE map sang UI:

| Code | Điều kiện | Trường | Section (FE điều hướng) |
|---|---|---|---|
| `PICKUP_ADDRESS_MISSING` | `store.Address` null hoặc `StreetAddress` rỗng | Địa chỉ lấy hàng | `StoreAddress` |
| `PICKUP_WARD_GHN_CODE_MISSING` | `Ward.GhnWardCode` rỗng hoặc `District.GhnDistrictId` null | Phường/xã điểm lấy hàng | `StoreAddress` |
| `PICKUP_PHONE_INVALID` | cả `SenderPhone` lẫn `Hotline` đều không phải di động VN 10 số | Số điện thoại người gửi | `StoreAddress` |
| `PICKUP_NAME_MISSING` | `SenderName` và `store.Name` đều rỗng | Tên người gửi | `StoreAddress` |
| `CARRIER_SHOP_ID_MISSING` | `GhnShopId` null **và** provider khai `RequiresStoreShopId` | Mã shop nhà vận chuyển | `StoreProfile` |

**Xử lý SĐT (P7).** `VietnamPhone` (`Common/Validation/`) tách 2 mức:
- `IsCarrierValid` — di động VN 10 số (`0[35789]` + 8 số). GHN chỉ nhận mức này.
- `IsContactValid` — rộng hơn (di động | 1900/1800 | cố định 10-11 số), dùng validate `Hotline` khi tạo/sửa store.

Vì hotline hợp lệ để khách gọi **chưa chắc** dùng được cho GHN (1900/số cố định bị từ chối), luồng là: hotline không phải di động → readiness báo thiếu → owner nhập `SenderPhone` riêng cho địa chỉ cửa hàng. `SenderPhone` được `Normalize` về dạng `0xxxxxxxxx` (chấp nhận `+84`, dấu cách, gạch nối) trước khi lưu.

> **Bug kèm theo đã sửa:** `StoreAddress.SenderName`/`SenderPhone` tồn tại trong entity nhưng **không có API nào set được** — nên chúng luôn null và requirement này vốn không thể thoả. Đã thêm 2 field vào `Create/UpdateStoreAddressRequest` + `StoreAddressResponse` và ghi trong `StoreService`.

### D.1 Phân biệt vai trò owner / garden staff

Cả garden owner lẫn garden staff (assignment `Accepted`) đều nhận đơn được, nhưng **chỉ owner sửa được hồ sơ cửa hàng**. Nên cùng một tình trạng thiếu thông tin cho ra 2 trải nghiệm:

| Vai trò | `canFix` | `section` | Message |
|---|---|---|---|
| Owner / Admin | `true` | mục cần sửa đầu tiên | "Cửa hàng đang thiếu thông tin giao hàng: {trường}. Vui lòng **bổ sung** trước khi tạo vận đơn." |
| Garden staff | `false` | `null` | "Cửa hàng đang thiếu thông tin giao hàng: {trường}. Vui lòng **liên hệ chủ cửa hàng** bổ sung trước khi tạo vận đơn." |

`section` chỉ trả cho người sửa được — staff không có quyền vào trang sửa nên không đưa đích điều hướng.

**Hai điểm tiêu thụ:**

1. `GET /api/shipping/stores/{storeId}/readiness` — FE gọi khi mở màn hình đơn giao để hiện cảnh báo **trước** khi bấm nút. Trả `isReady`, `canFix`, `role`, `message`, `section`, `issues[]`.
2. `OrderService.CreateDeliveryShipmentAsync` — guard cứng phía server, trả **422 UnprocessableEntity** kèm đúng message theo vai trò. Chạy trước `ExecuteInTransactionAsync` nên không mở transaction vô ích.

> Phân biệt vai trò dùng `IStoreRepository.IsOwnerAsync` / `IsAcceptedStaffAsync` sẵn có — không thêm khái niệm quyền mới.
> `IShippingProvider.RequiresStoreShopId` là default interface member (`false`); GHN override thành `_cfg.DefaultShopId <= 0` nên môi trường dev có ShopId mặc định sẽ không bị cảnh báo thừa.

---

### D.2 Ràng buộc điều kiện giao hàng ngay tại khâu đặt hàng — ✅ **ĐÃ CODE**

Chặn ở bước "Tạo đơn ship" là **quá muộn**: đơn đã thu tiền rồi mới phát hiện không giao được. `OrderService.CheckoutAsync` nay validate **cả hai phía** trước khi tạo đơn (`ValidateOrderShipping`), trả **422**:

| Phía | Kiểm tra | Ghi chú |
|---|---|---|
| Người mua | `RecipientName` không rỗng; `RecipientPhone` là di động 10 số; ward điểm giao có `GhnWardCode` + `GhnDistrictId` | FE đã chặn nhưng server không được tin FE |
| Người bán | **mọi** store trong đơn phải qua `StoreShippingReadiness` | Trước đây hoàn toàn không kiểm |

Message phía bán nêu đích danh cửa hàng: *"Cửa hàng "X" chưa đủ thông tin giao hàng (Địa chỉ lấy hàng, Số điện thoại người gửi) nên tạm thời chưa nhận đơn…"*.

> `PreviewShippingFeeAsync` **không** chặn — giỏ hàng vẫn hiển thị được phí; việc cảnh báo do endpoint readiness lo. `CheckoutAsync` và `PreviewShippingFeeAsync` giờ tự nạp `shipTo` + `stores` rồi truyền vào `ComputeStoreFeesAsync` (trước đây method này tự nạp) — checkout dùng lại đúng dữ liệu đã validate, không query lặp.

---

## E. APPLICATION — mỗi store một `GhnShopId` riêng + tự cấp phát — ✅ **ĐÃ CODE**

`ShopId` quyết định **địa chỉ shipper đến lấy hàng** và **tài khoản đối soát COD** → dùng chung một shop cho nhiều vendor là sai bản chất: GHN đến sai kho, tiền COD về sai chỗ. Vì vậy mỗi garden store phải là một shop riêng dưới cùng tài khoản (client) của sàn.

**Cơ chế cấp phát** — `IStoreShopProvisioner.EnsureShopIdAsync(store)`:
1. Store đã có `GhnShopId` → xong.
2. Dựng payload từ địa chỉ store (`district_id`, `ward_code`, `name`, `phone`, `address`) — thiếu trường nào thì dừng, để readiness báo owner bổ sung. Điều kiện dựng payload **trùng khớp** với `StoreShippingReadiness` nên hai bên không lệch.
3. Gọi `IShippingProvider.RegisterShopAsync` → GHN `POST /shiip/public-api/v2/shop/register` (endpoint duy nhất **không** cần header `ShopId` vì nó sinh ra ShopId).
4. Ghi `GhnShopId` bằng `SetCarrierShopIdAsync` — `ExecuteUpdate` 1 cột, **không đụng change-tracker** (nhiều luồng gọi vào đã tracked sẵn entity store → `Update()` sẽ ném lỗi trùng key). Vì bỏ qua interceptor nên tự set `UpdatedAt`.

Toàn bộ là **best-effort**: lỗi nhà vận chuyển chỉ log, không làm hỏng request đang chạy.

**Ba điểm kích hoạt:**

| Điểm | Khi nào | Vì sao |
|---|---|---|
| `StoreService.AddAddressAsync` / `UpdateAddressAsync` | Owner vừa lưu địa chỉ | Đúng lúc dữ liệu vừa đủ để đăng ký |
| `OrderService.CreateDeliveryShipmentAsync` | Ngay trước khi đánh giá readiness | Cứu store cũ tạo trước khi có cơ chế này |
| `CarrierShopSyncWorker` | Định kỳ (mặc định 30 phút, 20 store/lượt) | Backfill store cũ + các lần đăng ký trước bị lỗi mạng |
| `POST /api/shipping/carrier-shops/sync` | Nhân viên sàn bấm tay | Không phải chờ hết chu kỳ worker; trả báo cáo có lý do bỏ qua |
| `POST /api/shipping/stores/{storeId}/carrier-shop/sync` | Nhân viên sàn bấm trên từng dòng | Trả readiness sau khi chạy để UI cập nhật ngay |

**Chỉ store đủ điều kiện mới gửi request.** Lọc 2 tầng, không gọi nhà vận chuyển một cách vô ích:
- **SQL** (`GetMissingCarrierShopIdAsync`): `IsActive` · `GhnShopId == null` · có địa chỉ, `StreetAddress` khác rỗng · `Ward.GhnWardCode` khác null/rỗng · `District.GhnDistrictId` khác null.
- **Bộ nhớ** (`BuildRegistration`): SĐT phải chuẩn hoá (bỏ dấu cách, `+84`) rồi mới validate được nên không dịch sang SQL — lọc nốt ở đây trước khi gọi provider.

Store bị loại ở tầng 2 vào `Skipped[]` kèm `Reason` (`PICKUP_PHONE_INVALID` | `CARRIER_REGISTRATION_FAILED`) để màn hình nhân viên sàn hiển thị việc cần xử lý, thay vì log trôi mỗi 30 phút.

> Store **chưa có địa chỉ** bị loại ngay ở SQL và không xuất hiện trong báo cáo — đó là việc của chủ cửa hàng, tra qua endpoint readiness của từng store.

Worker bật/tắt qua `CarrierShopSync:IsActive` (đọc lại mỗi tick bằng `IOptionsMonitor`) — **tắt khi chạy `Shipping:Provider = "Mock"`**.

> **Quy ước nguồn sự thật:** `stores_address` trong DB là nguồn chính; payload luôn gửi `from_*` nên DB thắng. Địa chỉ shop bên GHN chỉ là fallback khi payload thiếu. Hệ quả cần lưu ý: **đăng ký shop chỉ chụp địa chỉ tại thời điểm đăng ký** — owner đổi địa chỉ sau đó thì shop bên GHN vẫn giữ địa chỉ cũ. Hiện chưa đồng bộ ngược; chấp nhận được vì `from_*` luôn ghi đè, nhưng nếu sau này bỏ gửi `from_*` thì phải bổ sung update shop.

---

## E'. APPLICATION — `ReturnService.cs` (đổi hàng)

`CreateReplacementDeliveryAsync` (dòng ~559+) cũng gọi `_shipping.CreateShipmentAsync` và cũng nằm trong transaction của `ApproveExchangeAsync` → **cùng bệnh P1/P2**. Áp dụng cùng cách chữa: dựng request + gọi provider trước, transaction chỉ ghi DB. Nếu tạo vận đơn đổi hàng fail, delivery thay thế vẫn nên được tạo ở trạng thái `Pending` để store bấm tạo ship lại — **không** rollback cả quyết định duyệt đổi hàng.

---

## F. APPLICATION — Constants

`ApiStatusCodes.cs` — **thêm**:

```csharp
public const int BadGateway = 502;   // lỗi từ dịch vụ bên thứ ba (GHN/PayOS)
```

`ApiStatusMessage.cs` → `Order` (đã có `DeliveryNotConfirmed`, `ShipmentAlreadyCreated`, `ShipmentCreated`) — **thêm**:

| Key | Nội dung |
|---|---|
| `ShipmentProviderFailed` | "Không tạo được vận đơn với nhà vận chuyển. Vui lòng thử lại sau." |
| `ShipmentStoreNotConfigured` | "Cửa hàng chưa cấu hình mã shop GHN." |
| `ShipmentPickupAddressInvalid` | "Địa chỉ lấy hàng của cửa hàng chưa được đồng bộ mã vùng GHN." |
| `ShipmentPhoneInvalid` | "Số điện thoại người gửi/người nhận không hợp lệ." |

---

## G. Quan sát & giám sát

| Mục | Thay đổi |
|---|---|
| `DeliveryFeeEstimator` (dòng 46-49) | Nâng `LogWarning` → `LogError` khi provider trả lỗi **cấu hình** (ShopId/mã vùng), giữ `LogWarning` cho lỗi tạm thời. Fallback im lặng chính là lý do bug này sống sót lâu. |
| Health check khởi động | Nếu `Shipping:Provider = "Ghn"`, gọi `/shop/all` lúc start và log ShopId hợp lệ. Sai cấu hình phát hiện ngay lúc boot thay vì lúc garden owner bấm nút. |
| Dev exception page | Xác nhận `DeveloperExceptionPageMiddleware` chỉ bật ở Development; production phải trả JSON lỗi chuẩn, không lộ stack trace. |

---

## H. Việc phải làm ngoài code

1. Cấu hình địa chỉ lấy hàng cho shop GHN `200891` trên portal (`ward_code`/`district_id` đang rỗng).
2. Điền `sender_name`/`sender_phone` cho mọi `stores_address` active.
3. Quy trình onboarding store: bắt buộc có `ghn_shop_id` trước khi store được bán hàng.
4. **Không cần rotate secret** — xác nhận `appsettings.json` chưa từng vào git (xem P8). Chỉ cần đảm bảo env production có `Ghn__DefaultShopId` đúng và dev mới được cấp `appsettings.json` qua kênh riêng.

---

## I. Thứ tự triển khai đề xuất

| Ưu tiên | Việc | Vì sao |
|---|---|---|
| **P0** | Mục A — sửa `DefaultShopId` = `200891` | 1 dòng, unblock toàn bộ việc test luồng giao hàng |
| **P1** | Mục B + C — không gọi HTTP trong transaction, không ném exception ra controller | Sửa lỗi kiến trúc, hết 500 |
| ✅ **P1** | Mục D + F — validate + message theo vai trò | Garden owner biết cần sửa gì thay vì thấy "lỗi hệ thống" — **đã code** |
| **P2** | Mục E — ReturnService | Cùng bệnh, ít gặp hơn |
| **P2** | Mục G — logging + health check | Chống tái diễn |
| **P3** | Idempotency `client_order_code` (B, cuối bảng) | Chống vận đơn mồ côi/trùng |

---

## Tổng kết file bị tác động

| Loại | File |
|---|---|
| **Sửa** | `WebAPI/appsettings.json` (chỉ đổi `DefaultShopId`; file không được track nên phải sửa thủ công trên từng môi trường) |
| **Sửa** | `Infrastructure/ExternalServices/Shipping/GhnShippingProvider.cs` (✅ `RequiresStoreShopId`; còn lại: exception có ngữ nghĩa, check `code`, idempotency) |
| **Thêm** | `Application/Interfaces/External/ShippingProviderException.cs` |
| ✅ **Thêm** | `Application/Common/Validation/VietnamPhone.cs` |
| ✅ **Thêm** | `Application/Features/Shipping/Services/StoreShippingReadiness.cs` + `DTOs/StoreShippingReadinessDtos.cs` |
| ✅ **Thêm** | `Application/Features/Shipping/Services/StoreShopProvisioner.cs` (tự cấp `GhnShopId`) |
| ✅ **Thêm** | `WebAPI/Workers/CarrierShopSyncWorker.cs` (+ options, đăng ký ở `Program.cs`, section `CarrierShopSync` trong appsettings) |
| ✅ **Sửa** | `Application/Interfaces/Repositories/IStoreRepository.cs` + `Infrastructure/…/StoreRepository.cs` (`GetMissingCarrierShopIdAsync`, `SetCarrierShopIdAsync`) |
| ✅ **Sửa** | `Application/Interfaces/External/ShippingProviderModels.cs` (`ShopRegistrationRequest`) |
| ✅ **Sửa** | `Application/Interfaces/External/IShippingProvider.cs` (`RequiresStoreShopId`) |
| ✅ **Sửa** | `Application/Features/Shipping/Services/ShippingService.cs` + `IShippingService.cs` (`GetStoreReadinessAsync`) |
| ✅ **Sửa** | `Application/Features/Vendor/Services/StoreService.cs` + `DTOs/StoreDtos.cs` (SenderName/SenderPhone, validate hotline) |
| ✅ **Sửa** | `WebAPI/Controllers/ShippingController.cs` (`GET /api/shipping/stores/{storeId}/readiness`) |
| **Sửa** | `Application/Features/Sales/Services/OrderService.cs` (✅ guard readiness 422; còn lại: provider ra ngoài transaction, map lỗi → ServiceResult) |
| **Sửa** | `Application/Features/Returns/Services/ReturnService.cs` (`CreateReplacementDeliveryAsync`) |
| **Sửa** | `Application/Features/Shipping/Services/DeliveryFeeEstimator.cs` (log level) |
| **Sửa** | `Application/Common/Constants/ApiStatusCodes.cs` + `ApiStatusMessage.cs` |
| **Không đổi** | `OrderWorkflow.cs`, `OrdersController.cs`, `ShipmentRequestBuilder.cs`, enums |
| **Dữ liệu** | `garden_stores.ghn_shop_id`, `stores_address.sender_name/sender_phone` |
| **Cập nhật doc** | [ghn-integration.md](./ghn-integration.md) — ghi rõ `ShopId ≠ client_id` và tick checklist §652 |
