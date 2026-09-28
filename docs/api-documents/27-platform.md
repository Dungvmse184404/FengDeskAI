# 27 — Platform

[← Mục lục](./README.md)

Controller: `PlatformController` · Route gốc: `/api/platform` · Thông tin chính sách chung của sàn, công khai.

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/platform/fee-policy` | Public | Chính sách phí sàn |

## GET `/api/platform/fee-policy`

🔓 Public. **Response `data`** (`PlatformFeePolicyResponse`):

```json
{ "commissionRate": 0.08, "maxPlatformFundedDiscountRate": 0.08, "payoutHoldDays": 7 }
```

| Field | Ý nghĩa |
|---|---|
| `commissionRate` | Phí sàn trên tiền hàng của delivery đã giao. Đơn đã đặt giữ tỉ lệ đã chốt (`deliveries.commission_rate`) |
| `maxPlatformFundedDiscountRate` | Trần giảm giá do sàn tài trợ trên mỗi delivery (tỉ lệ trên tiền hàng) = phí sàn — voucher sàn trừ vào phí sàn |
| `payoutHoldDays` | Số ngày giữ tiền sau khi giao trước khi vườn được chi |

**Làm tròn** (client phải làm đúng như vậy để số "thực nhận" khớp sổ): `phí = round(tiền hàng × commissionRate)`
tới đồng, nửa đồng làm tròn **lên**; `thực nhận = tiền hàng − phí`. Phí tính trên tổng tiền hàng của mỗi delivery,
nên số tính theo từng sản phẩm có thể lệch 1đ. Chi tiết: [`docs/adr/platform-fee-ledger.md`](../adr/platform-fee-ledger.md).

---

[← Model3D Requests](./26-model3d-requests.md) · [Tiếp: Vouchers →](./28-vouchers.md)
