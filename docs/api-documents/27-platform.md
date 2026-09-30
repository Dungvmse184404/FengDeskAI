# 27 — Platform

[← Mục lục](./README.md)

Controller: `PlatformController` · Route gốc: `/api/platform` · Chính sách chung của sàn. Xem công khai; đổi phí sàn
cần Manager trở lên (28/09/2026 — trước đó tỉ lệ là hằng số 8% trong code).

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/platform/fee-policy` | Public | Chính sách phí sàn đang áp |
| PUT | `/api/platform/fee-policy` | ManagerOrAbove | Đổi phí sàn (áp cho đơn đặt sau thời điểm lưu) |
| GET | `/api/platform/fee-policy/history` | ManagerOrAbove | Lịch sử thay đổi, mới nhất trước (tối đa 50) |

## GET `/api/platform/fee-policy`

🔓 Public. **Response `data`** (`PlatformFeePolicyResponse`):

```json
{ "commissionRate": 0.08, "maxPlatformFundedDiscountRate": 0.08, "payoutHoldDays": 7, "effectiveFrom": "2026-01-01T00:00:00Z" }
```

| Field | Ý nghĩa |
|---|---|
| `commissionRate` | Phí sàn trên tiền hàng của delivery đã giao. Đơn đã đặt giữ tỉ lệ đã chốt (`deliveries.commission_rate`) |
| `maxPlatformFundedDiscountRate` | Trần giảm giá do sàn tài trợ trên mỗi delivery (tỉ lệ trên tiền hàng) = phí sàn — voucher sàn trừ vào phí sàn |
| `payoutHoldDays` | Số ngày giữ tiền sau khi giao trước khi vườn được chi |
| `effectiveFrom` | Tỉ lệ hiện tại áp từ lúc nào; `null` = mặc định hệ thống (bảng `platform_fee_rates` trống) |

BE cache tỉ lệ 1 phút (xoá ngay trên instance nhận lệnh đổi) — nhiều instance thì instance khác trễ tối đa 1 phút.

**Làm tròn** (client phải làm đúng như vậy để số "thực nhận" khớp sổ): `phí = round(tiền hàng × commissionRate)`
tới đồng, nửa đồng làm tròn **lên**; `thực nhận = tiền hàng − phí`. Phí tính trên tổng tiền hàng của mỗi delivery,
nên số tính theo từng sản phẩm có thể lệch 1đ. Chi tiết: [`docs/adr/platform-fee-ledger.md`](../adr/platform-fee-ledger.md).

---

## PUT `/api/platform/fee-policy`

🔒 ManagerOrAbove. **Request:**

```json
{ "commissionRate": 0.085, "note": "Điều chỉnh theo chính sách quý IV" }
```

| Field | Ràng buộc |
|---|---|
| `commissionRate` | **Bắt buộc**, 0 – 0.3, tối đa 4 chữ số thập phân (0.0825 = 8,25%). Thiếu trường ⇒ `400` (không âm thầm thành 0%) |
| `note` | Tuỳ chọn, ≤ 500 ký tự — hiện trong lịch sử |

Ghi **thêm** một dòng `platform_fee_rates` (không sửa dòng cũ), `effective_from = now`. Người đổi = `created_by`.
Đơn đặt sau đó chốt tỉ lệ mới vào `orders.commission_rate` và mọi delivery của đơn; trần voucher sàn tài trợ đi theo.
Đơn đã đặt **không** đổi. Response như `GET`.

| Lỗi | Khi nào |
|---|---|
| `400` | Thiếu tỉ lệ, ngoài 0–30%, lẻ quá 4 chữ số, ghi chú > 500 ký tự |
| `403` | Không phải Manager/Admin |
| `409` | Trùng mức đang áp |

## GET `/api/platform/fee-policy/history`

🔒 ManagerOrAbove. `data`: `[{ id, commissionRate, effectiveFrom, note, changedByName }]` — `changedByName` null =
hệ thống (dòng khởi tạo của migration).

---

[← Model3D Requests](./26-model3d-requests.md) · [Tiếp: Vouchers →](./28-vouchers.md)
