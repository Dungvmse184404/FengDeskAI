# 00 — Tổng quan & Quy ước

[Mục lục →](./README.md)

Tài liệu API cho hệ thống **FengDeskAI** (BackEnd `.NET 8`, kiến trúc Clean Architecture: `WebAPI → Application → Domain / Infrastructure`). Mọi nội dung được sinh từ chính source code (`FengDeskAI.WebAPI/Controllers`, DTO trong `FengDeskAI.Application/Features/*/DTOs`, enum trong `FengDeskAI.Domain/Enums`).

---

## Base URL

| Môi trường | URL |
|------------|-----|
| Dev (HTTPS) | `https://localhost:7016` |
| Dev (HTTP) | `http://localhost:5244` |

Swagger UI (chỉ Development): `GET /swagger`.

---

## Xác thực (JWT Bearer)

- Mọi endpoint có `[Authorize]` yêu cầu header: `Authorization: Bearer <accessToken>`.
- Access token + refresh token lấy từ `POST /api/Auth/login` hoặc luồng đăng ký. **Refresh token nằm trong body** của `AuthResponse` (không phải cookie) — FE tự lưu và gọi `POST /api/Auth/refresh` khi access token hết hạn.
- Thiếu/invalid token → `401` với envelope lỗi chuẩn.

### Phân quyền (roles & policies)

Role là `[Flags]` enum `UserRole` — một tài khoản có thể mang nhiều role cùng lúc.

| Role | Giá trị bit | Ý nghĩa |
|------|-------------|---------|
| `Customer` | 1 | Khách mua hàng |
| `Manager` | 2 | Quản lý |
| `Staff` | 4 | Nhân viên hỗ trợ |
| `Admin` | 8 | Quản trị |
| `GardenOwner` | 16 | Chủ store (người bán) |

Thứ tự quyền: `Customer < Staff < Manager < Admin`. Các policy dùng trong `[Authorize(Policy = ...)]`:

| Policy | Gồm role |
|--------|----------|
| `AdminOnly` | Admin |
| `StaffOrAbove` | Staff, Manager, Admin |
| `ManagerOrAbove` | Manager, Admin (không gồm Staff) |
| `CustomerOnly` | Customer |
| `GardenOwnerOrAbove` | GardenOwner, Admin |

> Nhiều endpoint còn kiểm quyền sở hữu (owner/staff của store) ở **service layer** chứ không chỉ qua attribute — phần mô tả từng endpoint sẽ ghi rõ.

---

## Định dạng response (envelope thống nhất)

Tất cả endpoint trả về cùng một bao `ServiceResult` (qua `ApiControllerBase.ToActionResult`). HTTP status code = `statusCode` trong body.

**Thành công có dữ liệu:**
```json
{
  "isSuccess": true,
  "statusCode": 200,
  "message": null,
  "errors": null,
  "data": { }
}
```

**Thành công không dữ liệu:**
```json
{ "isSuccess": true, "statusCode": 200, "message": "...", "errors": null }
```

**Lỗi:**
```json
{
  "isSuccess": false,
  "statusCode": 400,
  "message": "Mô tả lỗi",
  "errors": ["chi tiết 1", "chi tiết 2"]
}
```

> Trong tài liệu từng endpoint, mục **Response** chỉ mô tả phần `data` (nội dung bên trong envelope) cho gọn.

---

## Mã trạng thái dùng chung

| Code | Khi nào |
|------|---------|
| `200 OK` | Thành công |
| `201 Created` | Tạo mới thành công |
| `202 Accepted` | Đã nhận, xử lý nền (vd sinh model 3D) |
| `204 No Content` | Thành công không trả dữ liệu |
| `400 Bad Request` | Sai tham số / validation |
| `401 Unauthorized` | Thiếu/invalid token |
| `403 Forbidden` | Không đủ quyền |
| `404 Not Found` | Không tìm thấy / endpoint chỉ-Dev ở Production |
| `409 Conflict` | Xung đột trạng thái (vd trùng, sai state machine) |
| `422 Unprocessable Entity` | Dữ liệu hợp lệ cú pháp nhưng không xử lý được |
| `500 Internal Server Error` | Lỗi máy chủ |

---

## Phân trang

Các endpoint list nhận query params chuẩn (`PageRequest`):

| Param | Mặc định | Ghi chú |
|-------|----------|---------|
| `page` | 1 | < 1 sẽ bị ép về 1 |
| `pageSize` | 20 | Tối đa 100; ngoài [1,100] ép về 20 |

`data` trả về dạng `PagedResult<T>`:
```json
{
  "items": [ ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

---

## Quy ước chung

- **Enum serialize thành chuỗi** (nhờ `JsonStringEnumConverter`), vd `"Pending"`, `"Kim"`, `"PayOS"` — không phải số. Xem [99-Appendix-Models](./99-appendix-models.md).
- ID dùng `Guid` (chuỗi UUID).
- Thời gian dùng UTC (ISO 8601).
- Upload tệp dùng `multipart/form-data`; các field cụ thể ghi trong từng endpoint.
- Các endpoint webhook (`/api/payments/payos/webhook`, `/api/shipping/*/webhook`) là `[AllowAnonymous]` nhưng tự xác thực bằng chữ ký / secret.

---

[Mục lục →](./README.md) · [Tiếp: Authentication →](./01-authentication.md)
