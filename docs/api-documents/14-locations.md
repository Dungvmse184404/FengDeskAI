# 14 — Locations

[← Mục lục](./README.md)

Controller: `LocationsController` · Route gốc: `/api/locations` · **Toàn bộ `[AllowAnonymous]`** (Public).

Tra cứu dữ liệu hành chính theo cấp: tỉnh → quận/huyện → phường/xã. Dùng để đổ dropdown chọn `wardId` khi tạo địa chỉ.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/locations/provinces` | Public | Danh sách tỉnh/thành |
| GET | `/api/locations/provinces/{provinceId}/districts` | Public | Quận/huyện theo tỉnh |
| GET | `/api/locations/districts/{districtId}/wards` | Public | Phường/xã theo quận |

---

## GET `/api/locations/provinces`
`data` = mảng `ProvinceResponse`:
```json
[{ "id": "guid", "name": "TP. Hồ Chí Minh", "code": 79 }]
```

## GET `/api/locations/provinces/{provinceId}/districts`
`data` = mảng `DistrictResponse`:
```json
[{ "id": "guid", "provinceId": "guid", "name": "Quận 1", "code": 760 }]
```

## GET `/api/locations/districts/{districtId}/wards`
`data` = mảng `WardResponse`:
```json
[{ "id": "guid", "districtId": "guid", "name": "Phường Bến Nghé", "code": 26734 }]
```

---

[← Addresses](./13-addresses.md) · [Tiếp: Stores →](./15-stores.md)
