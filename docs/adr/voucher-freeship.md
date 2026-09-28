# ADR — Voucher miễn phí vận chuyển (giai đoạn 2)

**Trạng thái:** ĐÃ LÀM (28/09/2026) · **Dựa trên:** [`platform-fee-ledger.md`](./platform-fee-ledger.md)

## 1. Bối cảnh

Trang thanh toán FE từng **tự** cho phí ship = 0 khi tạm tính ≥ 500.000đ ("Free Ship… từ 500.000đ"), trong khi
BE không có luật đó: khách thấy 650 000đ, bị ghi nợ 665 000đ. E2E `Checkout_SubtotalOver500k…` bắt được lỗi này.
Quyết định: biến "freeship 500k" thành **voucher thật ở BE**, FE chỉ hiển thị số BE trả.

## 2. Chính sách (chốt với chủ dự án)

| # | Quy tắc |
|---|---|
| 1 | Voucher **sàn tài trợ** trừ vào chính phí sàn 8%: khoản giảm của **mỗi vườn** trong đơn ≤ `min(phí ship của vườn, phí sàn của vườn)`. Sàn không bù lỗ quá phần mình thu; nhà vườn luôn nhận đủ `tiền hàng − phí sàn` |
| 2 | Điều kiện xét trên **cả đơn**: tổng tiền hàng mọi vườn ≥ `min_order_subtotal` |
| 3 | `FREESHIP500` (seed): ngưỡng 500 000đ, **tự áp**, toàn quốc (`province_id` null), không giới hạn lượt |
| 4 | Khách tự nhập mã mà mã không dùng được ⇒ checkout bị **từ chối 422** (không âm thầm đặt với giá khác số đã thấy). Không nhập mã ⇒ tự áp voucher tự động giảm nhiều nhất |
| 5 | Một đơn một voucher. Hủy/hết hạn đơn ⇒ trả lượt. Đơn hết hạn mà tiền (giá đã giảm) vẫn về ⇒ giữ lại lượt |
| 6 | Hoàn hàng **không** hoàn khoản giảm thành tiền (tiền hoàn = giá trị hàng bị trả, như cũ) |

Ví dụ đơn 510 000đ gồm vườn A 450 000đ (ship 30 000đ) + vườn B 60 000đ (ship 30 000đ): A giảm 30 000đ,
B chỉ giảm **4 800đ** (= 8% × 60 000đ). Khách trả 510 000 + 60 000 − 34 800.

## 3. Thiết kế

**Dữ liệu** (migration `AddVouchersAndStoreCharges`, chỉ cộng thêm):

| Bảng / cột | Vai trò |
|---|---|
| `vouchers` | mã (UNIQUE, chữ hoa), loại (`FreeShipping`), `funded_by` (`Platform`), ngưỡng, trần tổng, tỉnh, thời hạn, `usage_limit`, `usage_limit_per_user`, `used_count` (CHECK ≥ 0), `is_auto_apply`, `is_active` |
| `voucher_redemptions` | một lượt dùng của một đơn (UNIQUE `order_id`), `Applied`/`Released` — đếm giới hạn theo người, trả lượt khi hủy |
| `order_store_charges` | tiền hàng / phí ship / khoản giảm **theo từng vườn chốt lúc checkout**. Đơn PayOS sinh delivery lúc webhook đọc lại đúng số này (thay cách chia tỉ lệ của giai đoạn 1; đơn cũ không có dòng nào vẫn chia tỉ lệ) |
| `orders.shipping_discount`, `orders.voucher_code`, `deliveries.shipping_discount` | `total_amount = subtotal + total_shipping_fee − shipping_discount`; số COD thu hộ = `subtotal + shipping_fee − shipping_discount` của delivery |

**Code:**
- `ShippingVoucherCalculator` (thuần, không DB): điều kiện + chia khoản giảm, vườn tiền hàng lớn được chia trước
  khi có trần tổng — thứ tự cố định để preview và checkout ra cùng một số.
- `VoucherService`: chọn voucher (mã nhập / tự áp), kiểm giới hạn lượt, giữ/trả lượt.
- Giữ lượt = **một câu UPDATE nguyên tử** (`used_count + 1 WHERE used_count < usage_limit`) chạy **đầu tiên** trong
  transaction checkout — hai khách tranh lượt cuối thì đúng một người được; người kia nhận 409, chưa có gì phải
  rollback.
- Preview và checkout gọi **cùng** `VoucherService.SelectAsync` ⇒ số xem trước = số bị tính.
- Sổ cái: khi giao xong, sàn ghi `ShippingVoucherSubsidy = −shipping_discount`; sổ vườn không đổi.

**API:** `POST /api/orders` và `/shipping-fee-preview` nhận `voucherCode`; preview trả `shippingDiscount`,
`appliedVoucher`, `voucherMessage`, `stores[].shippingDiscount`. `GET /api/vouchers/available` (công khai);
Manager: `GET/POST /api/vouchers`, `PATCH /api/vouchers/{id}/active`. Chi tiết: [`28-vouchers`](../api-documents/28-vouchers.md).

**FE:** bỏ luật 500k viết cứng; hiển thị phí ship, dòng "Giảm phí vận chuyển (MÃ)", tổng = đúng số BE trả;
ô nhập mã; **chặn nút "Đặt hàng" khi phí chưa tính xong/lỗi** (trước đây hook trả phí 0 khi lỗi). Hộp ưu đãi lấy
từ `/vouchers/available` thay cho câu chữ viết cứng "Free Ship TP.HCM…".

## 4. Lỗi FE sửa kèm (E2E bắt được khi test ô nhập mã)

`AppLayout` bọc `<Outlet />` trong `AnimatePresence mode="wait"`: khung đang chạy hiệu ứng thoát vẫn render route
MỚI ⇒ **mọi trang mount hai lần** khi điều hướng (API gọi gấp đôi, mất thao tác ~0,3s đầu — ở trang thanh toán
là mất mã giảm giá, ghi chú, và phương thức thanh toán bật về PayOS). Sửa bằng `useOutlet()`. Cùng lúc, trang
thanh toán chỉ chắn spinner ở lần nạp giỏ đầu tiên.

## 5. Chưa làm

- ~~Trang quản trị voucher trên FE~~ — ĐÃ LÀM: `/manager/vouchers` (tạo, bật/tắt; Staff thấy thông báo không có quyền).
- Voucher do nhà vườn tài trợ, voucher giảm tiền hàng (`FundedBy`/`Type` đã có enum để mở rộng).
- Phạm vi "TP.HCM" như câu chữ cũ: model hỗ trợ (`province_id`), seed đang để toàn quốc — đổi bằng API.

## 6. Kiểm thử

- Unit `ShippingVoucherCalculatorTests`: 499 999 / 500 000, trần phí sàn từng vườn, trần tổng, tỉnh, thời hạn,
  hết lượt, giới hạn theo người, tranh lượt cuối; định dạng tiền VN trong câu báo lỗi.
- API `VoucherFlowTests` (VOUCHER-01…10): tự áp, dưới ngưỡng, trần vườn nhỏ, preview = checkout, mã sai bị từ
  chối không sinh đơn, hủy trả lượt, giới hạn 1 lượt, ghi sổ + bảo toàn tiền, **PayOS dùng đúng số chốt**, API công khai/validate.
- E2E: ≥ 500k thấy dòng giảm FREESHIP500 và bị tính đúng số thấy; nhập mã khi chưa đủ ngưỡng thấy lý do và không
  đặt được; bất biến tiền đơn/delivery gồm khoản giảm; bảo toàn tiền qua sổ cái.
