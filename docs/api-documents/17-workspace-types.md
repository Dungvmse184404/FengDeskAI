# 17 — Workspace Types

[← Mục lục](./README.md)

Controller: `WorkspaceTypesController` · Route gốc: `/api/workspace-types` · **Toàn bộ `[Authorize]`**.

Loại không gian làm việc — quyết định **trọng số cá nhân** (`personalWeight`) khi gợi ý. User thấy loại hệ thống + loại mình tạo; loại tự tạo mặc định trọng số `1.0`.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/workspace-types` | Authenticated | Loại khả dụng cho tôi |
| POST | `/api/workspace-types` | Authenticated | Tạo loại mới |

---

## GET `/api/workspace-types`
`data` = mảng `WorkspaceTypeResponse`:
```json
[{ "id": "guid", "name": "Personal Desk", "description": "...",
   "isPublic": true, "personalWeight": 1.0, "isSystemSeeded": true }]
```

## POST `/api/workspace-types`
**Request body** (`CreateWorkspaceTypeRequest`)
```json
{ "name": "Meeting Room", "description": "...", "isPublic": false, "personalWeight": 0.8 }
```
| Field | Ghi chú |
|-------|---------|
| `personalWeight` | Tùy chọn; bỏ trống → 1.0. Kẹp trong [0, 1] |

---

[← Workspace Profiles](./16-workspace-profiles.md) · [Tiếp: Recommendations →](./18-recommendations.md)
