# 01 — Authentication

[← Mục lục](./README.md)

Controller: `AuthController` · Route gốc: `/api/Auth` · Hầu hết **Public** (trừ `logout`, `me`).

Luồng đăng ký gồm 3 bước: gửi OTP về email (`initiate`) → xác thực OTP nhận `registrationToken` (`verify`) → đặt mật khẩu + thông tin hoàn tất (`finalize`).

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/Auth/register/initiate` | Public | Gửi OTP tới email |
| POST | `/api/Auth/register/verify` | Public | Xác thực OTP → `registrationToken` |
| POST | `/api/Auth/register/finalize` | Public | Hoàn tất đăng ký (mật khẩu + hồ sơ) |
| POST | `/api/Auth/login` | Public | Đăng nhập email + mật khẩu |
| POST | `/api/Auth/refresh` | Public | Cấp access token mới từ refresh token |
| POST | `/api/Auth/logout` | Authenticated | Thu hồi refresh token |
| GET | `/api/Auth/me` | Authenticated | Thông tin user hiện tại |

---

## POST `/api/Auth/register/initiate`

Gửi mã OTP tới email để bắt đầu đăng ký.

**Request body**
```json
{ "email": "user@example.com" }
```
**Response:** `data` = thông báo OTP đã gửi.

---

## POST `/api/Auth/register/verify`

Xác thực OTP, nhận `registrationToken` để dùng ở bước finalize.

**Request body**
```json
{ "email": "user@example.com", "otp": "123456" }
```
**Response `data`** (`VerifyRegisterResponse`)
```json
{ "registrationToken": "<token>", "expiresAt": "2026-06-29T10:00:00Z" }
```

---

## POST `/api/Auth/register/finalize`

Hoàn tất đăng ký bằng `registrationToken` + mật khẩu + hồ sơ.

**Request body** (`FinalizeRegisterRequest`)
```json
{
  "registrationToken": "<token>",
  "password": "MatKhau@123",
  "fullName": "Nguyễn Văn A",
  "phone": "0901234567",
  "dateOfBirth": "1998-05-20",
  "gender": "Male"
}
```
| Field | Kiểu | Bắt buộc | Ghi chú |
|-------|------|:--------:|---------|
| `registrationToken` | string | ✓ | Từ bước verify |
| `password` | string | ✓ | |
| `fullName` | string | ✓ | |
| `phone` | string? | | |
| `dateOfBirth` | date? | | Dùng tính mệnh/phong thủy |
| `gender` | enum `Gender` | | `Unspecified`/`Male`/`Female`/`Other` |

**Response `data`** = `AuthResponse` (xem `login`).

---

## POST `/api/Auth/login`

Đăng nhập bằng email + mật khẩu.

**Request body**
```json
{ "email": "user@example.com", "password": "MatKhau@123" }
```
**Response `data`** (`AuthResponse`)
```json
{
  "accessToken": "eyJ...",
  "accessTokenExpiresAt": "2026-06-29T11:00:00Z",
  "refreshToken": "<refresh>",
  "refreshTokenExpiresAt": "2026-07-06T10:00:00Z",
  "user": {
    "id": "guid", "email": "...", "fullName": "...", "phone": "...",
    "role": "Customer, GardenOwner",
    "roles": ["Customer", "GardenOwner"],
    "dateOfBirth": "1998-05-20", "gender": "Male",
    "fengShui": {
      "element": "Hoa", "kuaNumber": 3, "kuaGroup": "East",
      "favorableDirections": ["North", "Southeast"]
    }
  }
}
```
> `user.fengShui` được **tính sẵn** từ ngày sinh + giới tính; `null` nếu chưa có ngày sinh. `role` là chuỗi gộp `[Flags]`; `roles` là danh sách tách rời.

---

## POST `/api/Auth/refresh`

Cấp access token mới (token rotation).

**Request body**
```json
{ "refreshToken": "<refresh>" }
```
**Response `data`** = `AuthResponse` mới.
**Lỗi:** `401` nếu refresh token hết hạn / đã thu hồi / không hợp lệ.

---

## POST `/api/Auth/logout`

🔒 Authenticated. Thu hồi refresh token hiện tại.

**Request body**
```json
{ "refreshToken": "<refresh>" }
```

---

## GET `/api/Auth/me`

🔒 Authenticated. Thông tin user đang đăng nhập (dùng test JWT có hoạt động).

**Response `data`** = `UserSummary` (giống `user` trong `AuthResponse`).

---

[← Overview](./00-overview.md) · [Tiếp: Products →](./02-products.md)
