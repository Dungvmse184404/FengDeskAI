# 03 — Categories

[← Mục lục](./README.md)

Controller: `CategoriesController` · Route gốc: `/api/categories` · Mặc định `[Authorize(Policy = ManagerOrAbove)]`; **đọc là Public**.

Danh mục sản phẩm (có thể phân cấp qua `parentId`).

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/categories` | Public | Danh sách danh mục |
| GET | `/api/categories/{id}` | Public | Chi tiết danh mục |
| POST | `/api/categories` | ManagerOrAbove | Tạo danh mục |
| PUT | `/api/categories/{id}` | ManagerOrAbove | Cập nhật |
| DELETE | `/api/categories/{id}` | ManagerOrAbove | Xóa |

---

## GET `/api/categories` · GET `/api/categories/{id}`

**Response `data`** = `CategoryResponse` (hoặc mảng):
```json
{ "id": "guid", "name": "Cây để bàn", "description": "...", "parentId": null, "isActive": true }
```

## POST `/api/categories`
**Request body** (`CreateCategoryRequest`)
```json
{ "name": "Cây để bàn", "description": "...", "parentId": null }
```

## PUT `/api/categories/{id}`
**Request body** (`UpdateCategoryRequest`)
```json
{ "name": "...", "description": "...", "parentId": null, "isActive": true }
```

## DELETE `/api/categories/{id}`
Xóa danh mục.

---

[← Products](./02-products.md) · [Tiếp: Tags →](./04-tags.md)
