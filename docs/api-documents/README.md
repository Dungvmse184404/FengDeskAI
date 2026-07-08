# 📚 FengDeskAI — API Documentation

Tài liệu API đầy đủ cho hệ thống **FengDeskAI** (BackEnd `.NET 8`). Mỗi controller một file; nội dung phản ánh đúng source code trong `FengDeskAI.WebAPI/Controllers` và DTO ở `FengDeskAI.Application`.

**Tổng quan:** 24 controller · 147 endpoint.

---

## 🗂️ Mục lục

| # | Tài liệu | Controller | Số endpoint | Nội dung |
|---|----------|-----------|:-----------:|----------|
| 00 | [Tổng quan & Quy ước](./00-overview.md) | — | — | Base URL, Auth/JWT, envelope, lỗi, phân trang |
| 01 | [Authentication](./01-authentication.md) | `AuthController` | 7 | Đăng ký (OTP), đăng nhập, refresh, logout, me |
| 02 | [Products](./02-products.md) | `ProductsController` | 16 | Sản phẩm, SKU, ảnh, model 3D, phong thủy |
| 03 | [Categories](./03-categories.md) | `CategoriesController` | 5 | Danh mục sản phẩm |
| 04 | [Tags](./04-tags.md) | `TagsController` | 4 | Tag sản phẩm |
| 05 | [Styles](./05-styles.md) | `StylesController` | 3 | Tra cứu phong cách |
| 06 | [Vibes](./06-vibes.md) | `VibesController` | 3 | Tra cứu vibe |
| 07 | [Elements](./07-elements.md) | `ElementsController` | 2 | Tra cứu ngũ hành |
| 08 | [Cart](./08-cart.md) | `CartController` | 5 | Giỏ hàng |
| 09 | [Orders](./09-orders.md) | `OrdersController` | 8 | Đơn hàng, checkout, delivery |
| 10 | [Payments](./10-payments.md) | `PaymentsController` | 5 | Thanh toán PayOS, webhook |
| 11 | [Returns](./11-returns.md) | `ReturnsController` | 14 | Trả hàng / hoàn tiền / đổi (RMA) |
| 12 | [Shipping](./12-shipping.md) | `ShippingController` | 5 | Webhook GHN/AhaMove, tiến trình giao |
| 13 | [Addresses](./13-addresses.md) | `AddressesController` | 6 | Sổ địa chỉ user |
| 14 | [Locations](./14-locations.md) | `LocationsController` | 3 | Tỉnh → quận → phường |
| 15 | [Stores](./15-stores.md) | `StoresController` | 17 | Garden store, owner, staff, địa chỉ |
| 16 | [Workspace Profiles](./16-workspace-profiles.md) | `WorkspaceProfilesController` | 7 | Hồ sơ không gian làm việc |
| 17 | [Workspace Types](./17-workspace-types.md) | `WorkspaceTypesController` | 2 | Loại không gian + trọng số |
| 18 | [Recommendations](./18-recommendations.md) | `RecommendationsController` | 2 | Gợi ý sản phẩm phong thủy |
| 19 | [Reviews](./19-reviews.md) | `ReviewController` | 5 | Đánh giá sản phẩm |
| 20 | [Chat](./20-chat.md) | `ChatController` | 16 | Chat người↔người & người↔AI |
| 21 | [Notifications](./21-notifications.md) | `NotificationsController` | 4 | Thông báo |
| 22 | [Uploads](./22-uploads.md) | `UploadsController` | 1 | Upload ảnh dùng chung |
| 23 | [Ping](./23-ping.md) | `PingController` | 5 | Demo test authorization (Dev) |
| 24 | [Dev Tools](./24-dev-tools.md) | `DevToolsController` | 2 | Test AI tool (Dev) |
| 25 | [Scoring Config](./25-scoring-config.md) | `ScoringConfigController` | 11 | Admin cấu hình engine gợi ý v3 |
| A | [Phụ lục — Enums & Models](./99-appendix-models.md) | — | — | Toàn bộ enum, envelope, error codes |

---

## ⚡ Quick Start

```bash
# 1. Đăng nhập
curl -X POST https://localhost:7016/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{ "email": "user@example.com", "password": "..." }'
# → data.accessToken, data.refreshToken, data.user

# 2. Gọi endpoint cần auth
curl https://localhost:7016/api/cart \
  -H "Authorization: Bearer <accessToken>"
```

---

## 🔑 Tóm tắt nhanh

- **Auth:** `Authorization: Bearer <accessToken>`. Refresh token trong body, tự gọi `/api/Auth/refresh` khi `401`.
- **Roles:** `Customer`, `Staff`, `Manager`, `Admin`, `GardenOwner` (`[Flags]` — 1 account nhiều role).
- **Envelope:** mọi response bọc trong `{ isSuccess, statusCode, message, errors, data }`. Xem [00-Overview](./00-overview.md#định-dạng-response-envelope-thống-nhất).
- **Enum** serialize thành chuỗi (vd `"Pending"`, `"Kim"`).
- **Quy ước đặt tên file:** `NN-Ten.md` (2 chữ số, một controller mỗi file).
