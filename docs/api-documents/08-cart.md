# 08 — Cart

[← Mục lục](./README.md)

Controller: `CartController` · Route gốc: `/api/cart` · **Toàn bộ `[Authorize]`** — thao tác trên giỏ của user đang đăng nhập.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/cart` | Authenticated | Lấy giỏ của tôi |
| POST | `/api/cart/items` | Authenticated | Thêm món vào giỏ |
| PUT | `/api/cart/items/{itemId}` | Authenticated | Cập nhật số lượng |
| DELETE | `/api/cart/items/{itemId}` | Authenticated | Xóa món khỏi giỏ |
| DELETE | `/api/cart` | Authenticated | Xóa sạch giỏ |

---

## GET `/api/cart`
**Response `data`** = `CartResponse`:
```json
{
  "id": "guid", "customerId": "guid", "subtotal": 320000,
  "items": [{
    "id": "guid", "productItemId": "guid", "productName": "...",
    "variantName": "Chậu nhỏ", "unitPrice": 120000, "quantity": 2,
    "stock": 8, "lineTotal": 240000
  }]
}
```

## POST `/api/cart/items`
**Request body** (`AddCartItemRequest`)
```json
{ "productItemId": "guid", "quantity": 1 }
```

## PUT `/api/cart/items/{itemId}`
**Request body** (`UpdateCartItemRequest`)
```json
{ "quantity": 3 }
```

## DELETE `/api/cart/items/{itemId}`
Xóa một món khỏi giỏ.

## DELETE `/api/cart`
Xóa toàn bộ giỏ.

---

[← Elements](./07-elements.md) · [Tiếp: Orders →](./09-orders.md)
