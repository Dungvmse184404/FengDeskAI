# 23 — Ping

[← Mục lục](./README.md)

Controller: `PingController` · Area: `dev` · Route gốc: `/api/dev/ping`.

Các endpoint demo để **test authorization policies**. Có thể xóa/đổi sau khi auth đã verify hoạt động.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/dev/ping/public` | Public | Sanity check (không auth) |
| GET | `/api/dev/ping/authenticated` | Authenticated | Cần login bất kỳ role |
| GET | `/api/dev/ping/admin` | AdminOnly | Chỉ Admin |
| GET | `/api/dev/ping/manager` | ManagerOrAbove | Manager, Admin |
| GET | `/api/dev/ping/staff` | StaffOrAbove | Staff, Manager, Admin |

---

## Ví dụ response

**GET `/api/dev/ping/public`**
```json
{ "ok": true, "who": "anonymous" }
```

**GET `/api/dev/ping/authenticated`**
```json
{ "ok": true, "userId": "guid", "email": "user@example.com" }
```

**GET `/api/dev/ping/admin`** → `{ "ok": true, "who": "admin" }`
**GET `/api/dev/ping/manager`** → `{ "ok": true, "who": "manager_or_above" }`
**GET `/api/dev/ping/staff`** → `{ "ok": true, "who": "staff_or_above" }`

> Các endpoint này trả thẳng object (`Ok(...)`), không bọc trong envelope `ServiceResult`.

---

[← Uploads](./22-uploads.md) · [Tiếp: Dev Tools →](./24-dev-tools.md)
