# 13 — Addresses

[← Mục lục](./README.md)

Controller: `AddressesController` · Route gốc: `/api/addresses` · **Toàn bộ `[Authorize]`** — sổ địa chỉ giao hàng của user đang đăng nhập.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/addresses` | Authenticated | Địa chỉ của tôi |
| GET | `/api/addresses/{id}` | Authenticated | Chi tiết địa chỉ |
| POST | `/api/addresses` | Authenticated | Thêm địa chỉ |
| PUT | `/api/addresses/{id}` | Authenticated | Cập nhật địa chỉ |
| PATCH | `/api/addresses/{id}/set-default` | Authenticated | Đặt làm mặc định |
| DELETE | `/api/addresses/{id}` | Authenticated | Xóa địa chỉ |

---

## GET `/api/addresses` · `/{id}`
**Response `data`** = `UserAddressResponse`:
```json
{
  "id": "guid", "userId": "guid", "wardId": "guid",
  "streetAddress": "12 Nguyễn Trãi", "recipientName": "Nguyễn Văn A",
  "recipientPhone": "0901234567", "latitude": 10.77, "longitude": 106.69,
  "isDefault": true, "label": "Nhà", "createdAt": "...", "updatedAt": "..."
}
```

## POST `/api/addresses`
**Request body** (`CreateUserAddressRequest`)
```json
{
  "wardId": "guid", "streetAddress": "12 Nguyễn Trãi",
  "recipientName": "Nguyễn Văn A", "recipientPhone": "0901234567",
  "latitude": 10.77, "longitude": 106.69, "isDefault": true, "label": "Nhà"
}
```

## PUT `/api/addresses/{id}`
**Request body** (`UpdateUserAddressRequest`) — giống create nhưng **không có** `isDefault`:
```json
{ "wardId": "guid", "streetAddress": "...", "recipientName": "...",
  "recipientPhone": "...", "latitude": null, "longitude": null, "label": "Cơ quan" }
```

## PATCH `/api/addresses/{id}/set-default`
Đặt địa chỉ làm mặc định (tự bỏ default ở địa chỉ khác).

## DELETE `/api/addresses/{id}`
Xóa địa chỉ.

> `wardId` lấy từ [Locations](./14-locations.md).

---

[← Shipping](./12-shipping.md) · [Tiếp: Locations →](./14-locations.md)
