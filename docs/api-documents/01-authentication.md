# 01 — Authentication

[← Mục lục](./README.md)

Controller: `AuthController` · Route gốc: `/api/Auth` · Các endpoint đăng ký / đăng nhập / quên mật khẩu là **Public**; nhóm `me*` và `logout` yêu cầu **đăng nhập**.

Ba luồng nhiều bước, đều dùng chung hạ tầng OTP qua email (`OtpService`) nhưng **key cache tách riêng theo mục đích** nên chạy song song không đè nhau:

| Luồng | Các bước | Token ràng buộc phiên |
|---|---|---|
| Đăng ký | `register/initiate` → `register/verify` → `register/finalize` | `registrationToken` (15′, 1 lần) |
| Quên mật khẩu | `forgot-password/initiate` → `forgot-password/verify` → `forgot-password/reset` | `resetPasswordToken` (15′, 1 lần) |
| Đổi email | `me/email/initiate` → `me/email/verify-current` → `me/email/request-new` → `me/email/confirm` | `changeEmailToken` (15′, dùng lại qua các bước) |

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/Auth/register/initiate` | Public | Gửi OTP tới email |
| POST | `/api/Auth/register/verify` | Public | Xác thực OTP → `registrationToken` |
| POST | `/api/Auth/register/finalize` | Public | Hoàn tất đăng ký (mật khẩu + hồ sơ) |
| POST | `/api/Auth/login` | Public | Đăng nhập email + mật khẩu |
| POST | `/api/Auth/google` | Public | Đăng nhập/đăng ký bằng Google ID token |
| POST | `/api/Auth/forgot-password/initiate` | Public | Gửi OTP đặt lại mật khẩu tới email |
| POST | `/api/Auth/forgot-password/verify` | Public | Xác thực OTP → `resetPasswordToken` |
| POST | `/api/Auth/forgot-password/reset` | Public | Đặt mật khẩu mới, thu hồi mọi phiên cũ |
| POST | `/api/Auth/refresh` | Public | Cấp access token mới từ refresh token |
| POST | `/api/Auth/logout` | Authenticated | Thu hồi refresh token |
| GET | `/api/Auth/me` | Authenticated | Thông tin user hiện tại |
| PUT | `/api/Auth/me` | Authenticated | Cập nhật họ tên / SĐT / giới tính / ngày sinh |
| PUT | `/api/Auth/me/birth-time` | Authenticated | Cập nhật (hoặc xóa) giờ sinh |
| POST | `/api/Auth/me/email/initiate` | Authenticated | Đổi email B1 — OTP tới email **hiện tại** |
| POST | `/api/Auth/me/email/verify-current` | Authenticated | Đổi email B2 → `changeEmailToken` |
| POST | `/api/Auth/me/email/request-new` | Authenticated | Đổi email B3 — OTP tới email **mới** |
| POST | `/api/Auth/me/email/confirm` | Authenticated | Đổi email B4 — áp dụng + cấp lại token |

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
| `registrationToken` | string | ✓ | Từ bước verify, dùng 1 lần |
| `password` | string | ✓ | Tối thiểu **6 ký tự** (`PasswordPolicy.MinLength`) |
| `fullName` | string | ✓ | |
| `phone` | string? | | |
| `dateOfBirth` | date? | | Dùng tính mệnh/phong thủy |
| `gender` | enum `Gender` | | `Unspecified`/`Male`/`Female`/`Other` |

**Response `data`** = `AuthResponse` (xem `login`), status **201 Created**.

| Mã lỗi | Trường hợp |
|--------|------------|
| `400` | Mật khẩu để trống hoặc dưới 6 ký tự |
| `401` | `registrationToken` sai / hết hạn / đã dùng |
| `409` | Email hoặc số điện thoại đã được sử dụng |

> Mật khẩu được kiểm tra **trước** khi tiêu thụ `registrationToken`, nên user gõ mật khẩu quá ngắn có thể sửa và gửi lại với cùng token — không phải làm lại OTP.

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

## POST `/api/Auth/google`

Đăng nhập **hoặc** đăng ký bằng Google — một endpoint cho cả hai.

**Request body** (`GoogleLoginRequest`)
```json
{ "idToken": "<credential từ Google Identity Services>" }
```

| Field | Kiểu | Bắt buộc | Ghi chú |
|-------|------|:--------:|---------|
| `idToken` | string | ✓ | Chuỗi JWT `credential` mà Google Identity Services trả về ở FE — **không** phải access token |

**Response `data`** = `AuthResponse` (giống `login`).

Backend xác thực `idToken` với Google rồi xử lý theo 3 nhánh:

1. `GoogleId` đã link sẵn → đăng nhập thẳng.
2. Chưa link nhưng **email trùng** một tài khoản Local đã có → **tự động link** (an toàn vì Google đã xác thực quyền sở hữu email — `email_verified = true`), rồi đăng nhập.
3. Email hoàn toàn mới → **tạo tài khoản mới** với `PasswordHash = null`, `AuthProvider = Google`, `Role = Customer`.

| Mã lỗi | Trường hợp |
|--------|------------|
| `401` | `idToken` không hợp lệ / hết hạn, hoặc email Google chưa được xác thực |
| `403` | Tài khoản đã bị vô hiệu hóa |

> Tài khoản tạo ở nhánh 3 **chưa có mật khẩu** → không đăng nhập được bằng `/login` cho tới khi user đặt mật khẩu qua luồng **quên mật khẩu**.

---

## POST `/api/Auth/forgot-password/initiate`

**B1** — gửi mã OTP tới email của tài khoản cần lấy lại mật khẩu.

**Request body** (`ForgotPasswordRequest`)
```json
{ "email": "user@example.com" }
```

**Response:** `data` = null, `message` = "Đã gửi mã xác thực đến email. Vui lòng kiểm tra hộp thư."

| Mã lỗi | Trường hợp |
|--------|------------|
| `400` | Email sai định dạng, hoặc đang trong thời gian chờ gửi lại (mặc định 60 giây — `Otp:ResendCooldownSeconds`) |
| `403` | Tài khoản đã bị vô hiệu hóa |
| `404` | Email chưa được đăng ký |
| `500` | Không gửi được email (lỗi SMTP) — OTP đã bị hủy, có thể gọi lại ngay |

> Tài khoản đăng ký bằng **Google** (chưa từng có mật khẩu) **vẫn dùng được** luồng này — OTP về đúng hòm thư Google đó nên an toàn, và sau khi đặt xong user có thêm cách đăng nhập bằng mật khẩu (vẫn giữ nguyên đăng nhập Google).

---

## POST `/api/Auth/forgot-password/verify`

**B2** — xác thực OTP, nhận `resetPasswordToken` để dùng ở bước reset.

**Request body** (`VerifyForgotPasswordOtpRequest`)
```json
{ "email": "user@example.com", "otp": "123456" }
```

**Response `data`** (`ResetPasswordTokenResponse`)
```json
{ "resetPasswordToken": "<token>", "expiresAt": "2026-08-13T10:15:00Z" }
```

| Mã lỗi | Trường hợp |
|--------|------------|
| `400` | OTP sai / hết hạn / nhập sai quá số lần cho phép (mặc định 5 — `Otp:MaxVerifyAttempts`) |
| `403` | Tài khoản đã bị vô hiệu hóa |
| `404` | Email chưa được đăng ký |

> Token sống **15 phút**, dùng **một lần**. OTP bị xóa ngay khi verify đúng — muốn thử lại phải gọi `initiate` lần nữa.

---

## POST `/api/Auth/forgot-password/reset`

**B3** — đặt mật khẩu mới bằng token của B2.

**Request body** (`ResetPasswordRequest`)
```json
{ "resetPasswordToken": "<token>", "newPassword": "MatKhauMoi@123" }
```

| Field | Kiểu | Bắt buộc | Ghi chú |
|-------|------|:--------:|---------|
| `resetPasswordToken` | string | ✓ | Từ bước verify, dùng 1 lần |
| `newPassword` | string | ✓ | Tối thiểu **6 ký tự**, không trùng mật khẩu hiện tại |

**Response:** `data` = null, `message` = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại."

| Mã lỗi | Trường hợp |
|--------|------------|
| `400` | Mật khẩu để trống / dưới 6 ký tự / trùng mật khẩu hiện tại |
| `401` | Token sai, hết hạn, hoặc đã dùng rồi |
| `403` | Tài khoản đã bị vô hiệu hóa |
| `404` | Không tìm thấy người dùng |

> ⚠️ **FE lưu ý:** thành công thì **mọi phiên đăng nhập cũ bị thu hồi** — toàn bộ refresh token bị revoke và `TokenVersion` tăng lên nên access token còn hạn cũng chết ngay (kể cả trên thiết bị khác). Endpoint **không** trả `AuthResponse`; FE phải điều hướng về màn đăng nhập.
>
> Token chỉ bị tiêu thụ **sau** khi mật khẩu qua vòng kiểm tra định dạng, nên user gõ mật khẩu quá ngắn có thể sửa và gửi lại với cùng token.

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

| Mã lỗi | Trường hợp |
|--------|------------|
| `403` | Tài khoản đã bị vô hiệu hóa |
| `404` | Không tìm thấy người dùng |

---

## PUT `/api/Auth/me`

🔒 Authenticated. Cập nhật hồ sơ cá nhân. **Email không nằm ở đây** — đổi email đi qua luồng 4 bước riêng bên dưới.

**Request body** (`UpdateProfileRequest`)
```json
{
  "fullName": "Nguyễn Văn A",
  "phone": "0901234567",
  "gender": "Male",
  "dateOfBirth": "1998-05-20"
}
```

| Field | Kiểu | Bắt buộc | Ghi chú |
|-------|------|:--------:|---------|
| `fullName` | string | ✓ | Không được để trống |
| `phone` | string? | | 10 số bắt đầu bằng `0`. Null/rỗng = **xóa** số điện thoại |
| `gender` | enum `Gender` | ✓ | `Unspecified`/`Male`/`Female`/`Other` |
| `dateOfBirth` | date? | | Không được ở tương lai, năm ≥ 1900. Null = **xóa** |
| `occupationCode` | string? | | Mã nghề, vd `"IT"` — **tuỳ chọn**, không chặn gì. `null` = **giữ nguyên**, `""` = **xóa**. Mã lạ hoặc nghề đã tắt → 400. Danh sách ở `GET /api/occupations` |

**Response `data`** = `UserSummary` đã cập nhật (kèm `fengShui` tính lại).

| Mã lỗi | Trường hợp |
|--------|------------|
| `400` | Họ tên trống / SĐT sai định dạng / ngày sinh ở tương lai hoặc trước 1900 |
| `404` | Không tìm thấy người dùng |
| `409` | SĐT đã được tài khoản khác dùng |

> ⚠️ `occupationCode` là field **duy nhất** trong body này phân biệt `null` với chuỗi rỗng. `PUT` ghi
> đè cả hồ sơ, nên nếu `null` cũng xóa thì mọi client cũ chưa biết field này sẽ âm thầm xóa nghề của
> user mỗi lần họ sửa số điện thoại.

> ⚠️ Đổi `dateOfBirth` hoặc `gender` làm **đổi mệnh Nạp Âm / cung Kua** đã tính → khối `fengShui` và mọi gợi ý sản phẩm thay đổi theo. FE nên xác nhận với user trước khi gửi. Gửi lại đúng SĐT cũ của chính mình **không** bị báo trùng.

---

## PUT `/api/Auth/me/birth-time`

🔒 Authenticated. Cập nhật giờ sinh — cần cho Tứ Trụ/Bát Tự đầy đủ, không bắt buộc.

**Request body** (`UpdateBirthTimeRequest`)
```json
{ "birthTime": "07:30" }
```

| Field | Kiểu | Bắt buộc | Ghi chú |
|-------|------|:--------:|---------|
| `birthTime` | string? | | Định dạng `HH:mm`. Null/rỗng = **xóa** giờ sinh |

**Response `data`** = `UserSummary` đã cập nhật.

| Mã lỗi | Trường hợp |
|--------|------------|
| `400` | Sai định dạng `HH:mm` (chặn ngay tại Controller, chưa chạm service) |
| `404` | Không tìm thấy người dùng |

> Xóa giờ sinh **không** làm mất hồ sơ phong thủy — engine vẫn tính được mệnh/cung từ ngày sinh + giới tính, chỉ thiếu trụ giờ.

---

# Đổi email — 4 bước

🔒 Cả 4 endpoint đều Authenticated. Thứ tự bắt buộc, mỗi bước sau cần kết quả của bước trước; từ B2 trở đi ràng bằng `changeEmailToken` (sống **15 phút**) để không ai nhảy cóc qua bước xác thực email cũ.

```
B1 initiate       → OTP về email HIỆN TẠI   (chứng minh đúng chủ tài khoản)
B2 verify-current → đổi OTP lấy changeEmailToken
B3 request-new    → khai email MỚI, OTP về hòm thư đó
B4 confirm        → xác thực OTP mới, ghi email + cấp lại token
```

---

## POST `/api/Auth/me/email/initiate`

**B1** — gửi OTP tới email **hiện tại**. Không có request body.

**Response:** `message` = "Đã gửi mã xác nhận tới email hiện tại."

| Mã lỗi | Trường hợp |
|--------|------------|
| `400` | Đang trong thời gian chờ gửi lại (60 giây) |
| `404` | Không tìm thấy người dùng |
| `500` | Không gửi được email (lỗi SMTP) |

---

## POST `/api/Auth/me/email/verify-current`

**B2** — xác thực OTP của email hiện tại, nhận `changeEmailToken`.

**Request body** (`VerifyCurrentEmailRequest`)
```json
{ "otp": "123456" }
```

**Response `data`** (`ChangeEmailTokenResponse`)
```json
{ "changeEmailToken": "<token>", "expiresAt": "2026-08-13T10:15:00Z" }
```

| Mã lỗi | Trường hợp |
|--------|------------|
| `400` | OTP sai / hết hạn / nhập sai quá số lần cho phép |
| `404` | Không tìm thấy người dùng |

---

## POST `/api/Auth/me/email/request-new`

**B3** — khai email mới, hệ thống gửi OTP tới hòm thư đó.

**Request body** (`RequestNewEmailRequest`)
```json
{ "changeEmailToken": "<token>", "newEmail": "moi@example.com" }
```

**Response:** `message` = "Đã gửi mã xác nhận tới email mới."

| Mã lỗi | Trường hợp |
|--------|------------|
| `400` | Email mới sai định dạng, hoặc trùng chính email hiện tại |
| `401` | `changeEmailToken` sai / hết hạn / không thuộc user đang đăng nhập |
| `404` | Không tìm thấy người dùng |
| `409` | Email mới đã được tài khoản khác dùng |

> Email mới chỉ được ghi vào phiên **sau khi gửi OTP thành công** — SMTP lỗi thì phiên không bị khóa vào địa chỉ chưa gửi được, user khai lại địa chỉ khác ngay với cùng token.

---

## POST `/api/Auth/me/email/confirm`

**B4** — xác thực OTP của email mới, áp dụng thay đổi.

**Request body** (`ConfirmNewEmailRequest`)
```json
{ "changeEmailToken": "<token>", "otp": "654321" }
```

**Response `data`** = `AuthResponse` **mới** (access + refresh token đã cấp lại).

| Mã lỗi | Trường hợp |
|--------|------------|
| `400` | OTP sai / hết hạn / nhập sai quá số lần cho phép |
| `401` | `changeEmailToken` sai / hết hạn / chưa qua B3 (chưa có email đang chờ) |
| `404` | Không tìm thấy người dùng |
| `409` | Email mới vừa bị tài khoản khác đăng ký mất trong lúc chờ OTP |

> Access token mang claim email nên backend **cấp lại cặp token mới ngay** — FE phải thay token đang giữ bằng token trong response, không cần bắt user đăng nhập lại. `changeEmailToken` bị hủy sau khi thành công.

---

[← Overview](./00-overview.md) · [Tiếp: Products →](./02-products.md)
