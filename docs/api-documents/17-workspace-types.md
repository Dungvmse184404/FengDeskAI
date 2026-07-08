# 17 — Workspace Types

[← Mục lục](./README.md)

Controller: `WorkspaceTypesController` · Route gốc: `/api/workspace-types` · **Toàn bộ `[Authorize]`**.

Loại không gian làm việc. User thấy loại hệ thống + loại mình tạo; loại tự tạo mặc định trọng số `1.0`.

> **Engine v3:** loại phòng giờ còn gắn với **bộ vector ngũ hành** (`Ideal`/`Interior`) qua bảng `workspace_type_elements` — admin quản ở [Scoring Config](./25-scoring-config.md). Ngoài ra bảng có cột **`scope`** (`Private/Shared/Public`) quyết định lọc mệnh là "hard" (loại) hay "soft" (trừ điểm).
>
> ⚠️ **Legacy:** `personalWeight` là cách chấm điểm cũ, **engine v3 không dùng**. Cột `scope` **hiện chưa được lộ ra** trong request/response dưới đây (mới có ở DB).

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
