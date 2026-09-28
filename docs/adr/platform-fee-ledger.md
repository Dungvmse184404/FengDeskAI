# ADR — Phí sàn 8% & sổ cái tiền (giai đoạn 1)

**Trạng thái:** ĐÃ LÀM (28/09/2026) · **Liên quan:** [`vendor-payout.md`](./vendor-payout.md) (bước 2), voucher (giai đoạn 2, chưa làm)

## 1. Bối cảnh — tiền đi qua sàn như thế nào

Mọi đồng tiền của một đơn đều đi qua tài khoản của **sàn**, không đi thẳng tới nhà vườn:

| Kênh | Ai thu tiền của khách | Ai trả nhà vận chuyển |
|---|---|---|
| PayOS | tài khoản PayOS của sàn | sàn (GHN chạy bằng **một token của sàn**, mỗi vườn chỉ là một `ShopId`; `payment_type_id = 1` = shop trả phí) |
| COD | GHN thu hộ `Subtotal + ShippingFee` rồi chuyển về tài khoản GHN của sàn | sàn (trừ thẳng vào tiền thu hộ) |

Vì vậy sàn là bên **chi lại** cho nhà vườn. Trước ADR này: không có phí sàn (vườn nhận 100% tiền hàng), không có
sổ cái, và việc cộng tiền vào số dư đang tắt vì 3 lỗi ([`vendor-payout.md`](./vendor-payout.md) §3b).

## 2. Chính sách (chốt 28/09/2026)

| # | Quy tắc | Ở đâu trong code |
|---|---|---|
| 1 | **Phí sàn 8% trên tiền hàng** của delivery đã giao thành công | `PlatformFeePolicy.CommissionRate` |
| 2 | Tỉ lệ **chốt vào từng delivery lúc tạo** (`deliveries.commission_rate`) — đổi chính sách không ảnh hưởng đơn đã đặt. Delivery có trước chính sách mang **0%** (không thu hồi tố) | `OrderWorkflow.GroupItemsIntoDeliveries`, `PaymentService`, `OrderService.EnsureDeliveriesAsync` |
| 3 | Làm tròn phí tới đồng, **nửa đồng làm tròn lên** (không phải banker's rounding mặc định của .NET) — FE dùng đúng quy tắc này | `PlatformFeePolicy.ComputeCommission`, FE `utils/platform-fee.ts` |
| 4 | **Phí ship thuộc về sàn** (sàn trả nhà vận chuyển); chênh lệch phí khách trả − phí GHN thực tính là lãi/lỗ của sàn | `LedgerEntryType.ShippingCollected` / `CarrierShippingCost` |
| 5 | Hoàn tiền: vườn chịu phần tiền hàng bị hoàn (`VendorLiability`), **sàn trả lại phần phí sàn tương ứng**. Manager miễn công nợ ⇒ đảo cả hai | `LedgerService.PostLiabilityRaisedAsync` / `PostLiabilityWaivedAsync` |
| 6 | **Voucher do sàn tài trợ trừ vào chính 8% đó**: giảm giá sàn gánh cho một delivery ≤ phí sàn của delivery đó. Sàn không bù lỗ quá phần mình thu; vườn luôn nhận đủ `tiền hàng − phí sàn` | `ShippingVoucherCalculator` (áp theo từng delivery) |
| 7 | Tiền của vườn khả dụng sau **7 ngày** kể từ khi giao (= cửa sổ đổi trả) | `PayoutPolicy.HoldDays` |

**Ví dụ một món 150 000đ, phí ship 15 000đ, GHN tính thật 17 500đ:**

| | Vườn | Sàn | Ra ngoài |
|---|---|---|---|
| Khách trả | | | +165 000 |
| Tiền hàng | +150 000 | | |
| Phí sàn 8% | −12 000 | +12 000 | |
| Phí ship khách trả | | +15 000 | |
| GHN | | −17 500 | −17 500 |
| **Cộng** | **138 000** | **9 500** | 147 500 = 165 000 − 17 500 ✔ |

## 3. Sổ cái — bảng `ledger_entries`

Mỗi dòng là một bút toán **chỉ thêm**: `account` (`GardenStore` | `Platform`), `garden_store_id` (bắt buộc với sổ
vườn — có CHECK constraint), `type`, `amount` có dấu, `available_at`, nguồn (`order_id`, `delivery_id`,
`refund_id`, `vendor_liability_id`) và **`idempotency_key` UNIQUE** dạng `{account}:{type}:{nguồn}:{id}`.

Số dư một sổ = Σ `amount`. "Có thể chi" = Σ các dòng `available_at ≤ now`.

| Sự kiện | Loại | Sổ vườn | Sổ sàn | `available_at` (sổ vườn) |
|---|---|---|---|---|
| Delivery → Delivered | `SaleCredit` | +tiền hàng | | giao + 7 ngày |
| | `Commission` | −phí | +phí | giao + 7 ngày |
| | `ShippingCollected` | | +phí ship (trước giảm) | |
| | `ShippingVoucherSubsidy` | | −khoản giảm voucher sàn tài trợ ([voucher-freeship.md](./voucher-freeship.md)) | |
| | `CarrierShippingCost` | | −phí GHN thực tính (nếu có) | |
| Refund Completed | `RefundPaidOut` | | −tiền hoàn | |
| Sinh công nợ | `RefundLiability` | −số công nợ | +số công nợ | **max(now, mốc hết giữ của delivery bị hoàn)** |
| | `CommissionReversal` | +phí trả lại | −phí trả lại | như trên |
| Miễn công nợ | `LiabilityWaived` | +số công nợ | −số công nợ | như bút toán gốc |
| | `CommissionReinstated` | −phí trả lại | +phí trả lại | như bút toán gốc |

**Bất biến bảo toàn** (kiểm ở unit test, API test `LEDGER-06` và E2E): với mọi đơn,
`Σ bút toán hai sổ = tiền khách trả − tiền nhà vận chuyển − tiền hoàn cho khách`, với tiền khách trả =
`tiền hàng + phí ship − khoản giảm voucher`. Không bút toán nào tự sinh
hay làm mất tiền.

**Vì sao khoản trừ công nợ lấy mốc hết giữ của delivery bị hoàn:** hoàn hàng gần như luôn xảy ra trong 7 ngày
giữ (cửa sổ đổi trả = khoảng giữ). Nếu khoản trừ có hiệu lực ngay trong khi tiền hàng nó triệt tiêu còn "chờ",
"có thể chi" bị âm dù vườn không nợ gì — E2E đã bắt được đúng trường hợp này (−73 600đ) trước khi sửa.

### Điểm ghi sổ

Chỉ `ILedgerService` ghi sổ, luôn **trong cùng transaction** với thay đổi trạng thái:

- `OrderService.UpdateDeliveryStatusAsync` (vendor cập nhật tay) và `ShippingService.ProcessWebhookAsync`
  (webhook GHN/AhaMove, cả giả lập) → `PostDeliveryCompletedAsync`. Delivery đổi hàng (`IsExchange`) bỏ qua.
- `VendorLiabilityService.CreateForRefundAsync` → `PostRefundPaidOutAsync` + `PostLiabilityRaisedAsync`.
- `VendorLiabilityService.ResolveAsync` (vendor thắng) → `PostLiabilityWaivedAsync`. `Settled` không ghi gì — khoản
  trừ đã có từ lúc sinh công nợ.

### Dữ liệu cũ

`LedgerBackfillSeeder` (chạy trong `dotnet run -- seed`, idempotent) ghi sổ cho delivery đã giao, refund đã
xong, công nợ đã sinh/đã miễn **trước** migration `AddLedgerAndPlatformFee`. Delivery cũ giữ `commission_rate = 0`.

## 4. Lỗi phí ship cũ đã sửa trong ADR này

1. **Ghi đè phí ship khách trả bằng phí GHN.** Khi tạo vận đơn, `delivery.ShippingFee` bị gán bằng phí GHN trả
   về, còn `Order.TotalShippingFee` (và số tiền COD GHN thu hộ) vẫn là phí ước tính lúc checkout. Với GHN thật,
   hai số này lệch nhau ⇒ Σ phí ship các delivery ≠ phí ship của đơn, và sàn mất dấu mình lãi/lỗ bao nhiêu trên
   phí ship. E2E trước đây xanh chỉ vì chạy `Shipping__Provider=Mock` (không trả phí). Nay phí GHN nằm ở cột
   riêng `deliveries.carrier_shipping_fee`; `shipping_fee` giữ nguyên số khách trả.
2. **Đơn PayOS mất phí ship ở cấp delivery.** Delivery của đơn online chỉ sinh khi webhook báo đã trả, và được
   tạo với `ShippingFee = 0` — phí theo từng vườn tính lúc checkout không được lưu. Nay
   `OrderWorkflow.AllocateOrderShippingFee` chia `Order.TotalShippingFee` về từng delivery theo tỉ trọng tiền
   hàng (phần lẻ dồn delivery cuối), tổng luôn khớp từng đồng.

## 5. API & FE

- `GET /api/platform/fee-policy` (công khai) → `{ commissionRate, maxPlatformFundedDiscountRate, payoutHoldDays }`.
- `GET /api/stores/{id}/statistics` thêm `commissionRate`, `platformCommission`, `ledgerBalance`,
  `ledgerAvailable`, `ledgerPending` — đọc từ sổ (thêm **một** lượt đi về DB).
- FE: ô "Giá bán" (tạo sản phẩm, thêm/sửa phân loại) hiện **Khách trả / Phí sàn / Bạn nhận được**; thẻ doanh thu
  ở tab Thống kê hiện thêm "Thực nhận sau phí sàn".

## 6. Chưa làm (đừng giả định đã có)

- **Lệnh chi (payout)**: yêu cầu rút, duyệt, thông tin ngân hàng vendor, bút toán `PayoutDebit`. Thẻ "Có thể rút"
  vẫn ẩn; `PayoutCreditService` (cộng `users.balance`) **vẫn tắt** và sẽ được thay bằng lệnh chi đọc từ sổ.
- ~~Voucher (giai đoạn 2)~~ — ĐÃ LÀM 28/09/2026, xem [`voucher-freeship.md`](./voucher-freeship.md).
- Phí cổng PayOS chưa ghi sổ (sàn chịu, coi như nằm trong phí sàn).
- Delivery giao thất bại/hoàn về kho: phí GHN của chiều đi chưa ghi sổ (chỉ ghi khi Delivered).
- Phân loại công nợ theo lỗi (vd hư hại do vận chuyển ⇒ sàn chịu thay vì vườn) — hiện mọi hoàn tiền đều sinh
  công nợ cho vườn, Manager miễn thủ công qua dispute.

## 7. Kiểm thử

- Unit (`PlatformFeeLedgerTests`): làm tròn, chia phí ship, chốt tỉ lệ, cân bằng từng sự kiện, idempotent,
  mốc hiệu lực công nợ trong/sau khoảng giữ, đảo khi miễn công nợ.
- API (`LedgerFlowTests`, LEDGER-01…06): chính sách công khai, chốt tỉ lệ khi checkout, giao xong ghi đúng số,
  hoàn trọn về 0 và trả lại phí sàn, miễn công nợ khôi phục số dư, bảo toàn tiền.
- E2E (repo FE): số trên UI nhập giá khớp từng đồng; `ledgerBalance`/`platformCommission` = tính lại độc lập từ
  bảng nghiệp vụ = tính tay; bảo toàn tiền cho từng đơn đã giao/hoàn.
