# 28 — Vouchers

[← Mục lục](./README.md)

Controller: `VouchersController` · Route gốc: `/api/vouchers` · Mặc định Manager trở lên. Thiết kế:
[`docs/adr/voucher-freeship.md`](../adr/voucher-freeship.md).

Áp mã **không** có endpoint riêng: gửi `voucherCode` trong `POST /api/orders` và
`POST /api/orders/shipping-fee-preview` ([09-orders](./09-orders.md)) — để số xem trước và số bị tính luôn đi qua
cùng một hàm.

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/vouchers/available` | Public | Mã đang bật, còn hạn, còn lượt |
| GET | `/api/vouchers` | Manager+ | Tất cả mã (paged: `page`, `pageSize`) |
| POST | `/api/vouchers` | Manager+ | Tạo mã (hiện chỉ miễn phí vận chuyển do sàn tài trợ) |
| PATCH | `/api/vouchers/{id}/active` | Manager+ | Bật/tắt mã — body `{ "isActive": false }` |

## `VoucherResponse`

```json
{
  "id": "guid", "code": "FREESHIP500", "name": "Miễn phí vận chuyển cho đơn từ 500.000đ",
  "description": "...", "type": "FreeShipping", "fundedBy": "Platform",
  "minOrderSubtotal": 500000, "maxDiscountAmount": null, "provinceId": null,
  "startsAt": null, "endsAt": null, "usageLimit": null, "usageLimitPerUser": null,
  "usedCount": 12, "isAutoApply": true, "isActive": true
}
```

## POST `/api/vouchers`

```json
{
  "code": "SALE10", "name": "...", "description": "...",
  "minOrderSubtotal": 300000, "maxDiscountAmount": 50000, "provinceId": null,
  "startsAt": "2026-10-01T00:00:00Z", "endsAt": "2026-10-31T23:59:59Z",
  "usageLimit": 100, "usageLimitPerUser": 1, "isAutoApply": false
}
```

`code` 3–50 ký tự `A-Z 0-9 - _` (tự viết hoa). Lỗi: `400` dữ liệu sai, `409` trùng mã.

## Luật tính (tóm tắt)

- Điều kiện trên **cả đơn**: bật, trong thời hạn, đúng tỉnh giao (nếu có), tổng tiền hàng ≥ `minOrderSubtotal`,
  còn lượt (tổng và theo người).
- Khoản giảm mỗi vườn = `min(phí ship của vườn, round(8% × tiền hàng của vườn))` — voucher sàn tài trợ không vượt
  phí sàn. `maxDiscountAmount` chặn tổng, chia cho vườn tiền hàng lớn trước.
- Lỗi khi checkout với mã khách nhập: `422` (kèm lý do, vd "Đơn cần tối thiểu 500.000đ tiền hàng…"); lượt cuối
  vừa bị người khác lấy: `409`.

---

[← Platform](./27-platform.md) · [Phụ lục: Enums & Models →](./99-appendix-models.md)
