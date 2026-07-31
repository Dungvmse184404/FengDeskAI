# 26 — Model3D Requests (staff sàn)

[← Mục lục](./README.md)

Controller: `Model3DRequestsController` · Route gốc: `/api/model3d-requests` · **Toàn bộ endpoint yêu cầu policy `StaffOrAbove`** (`UserRole.Staff` / `Manager` / `Admin` — nhân sự nền tảng, KHÁC `garden_staff_assignments` của từng store).

Xử lý **thủ công** hàng chờ Regenerate (yêu cầu "tạo lại model 3D" khi sản phẩm đã có model). Request Initial (lần sinh đầu tiên) chạy tự động, không đi qua controller này — xem [02-products.md](./02-products.md#model-3d-sinh-từ-ảnh-qua-meshy-ai--multi-image-1–4-ảnh). Thiết kế đầy đủ: `docs/adr/refactor-model3d-request-flow.md`.

**Không có bước claim/khóa** — bất kỳ staff nào cũng xem và xử lý được mọi request, `assignedStaffId` chỉ mang tính audit "ai làm gần nhất", không phải quyền độc quyền.

---

## 📋 Bảng endpoint

| Method | Path | Mô tả |
|--------|------|-------|
| GET | `/api/model3d-requests` | Hàng chờ (lọc theo `status`, `reason`) |
| POST | `/api/model3d-requests/{id}/generate` | Chọn ảnh → gửi task Meshy lần đầu |
| POST | `/api/model3d-requests/{id}/retry` | Chưa ưng ý → chọn lại ảnh, gửi lại (không giới hạn số lần) |
| GET | `/api/model3d-requests/{id}/preview` | Xem trước kết quả Meshy hiện tại (live poll) |
| POST | `/api/model3d-requests/{id}/accept` | Ưng ý → ghi đè model chính thức của sản phẩm |
| POST | `/api/model3d-requests/{id}/reject` | Từ chối xử lý request |

---

## GET `/api/model3d-requests`

Query:

| Param | Kiểu | Ghi chú |
|---|---|---|
| `status` | enum? | `Queued \| Processing \| AwaitingStaff \| InProgress \| Succeeded \| Failed \| Rejected` |
| `reason` | enum? | `InsufficientCredits \| GenerationFailed \| InvalidImage` — lọc theo lý do lỗi nội bộ |
| `skip`, `take` | int | Phân trang (mặc định `take=20`) |

Dùng `status=AwaitingStaff` để xem hàng chờ Regenerate cần xử lý. Dùng
`status=Queued&reason=InsufficientCredits` để xem các request **Initial** đang kẹt vì Meshy hết
credit (cần nạp thêm) — đây là chỗ **duy nhất** hiển thị lý do hết credit; garden owner/garden
staff không bao giờ thấy field này.

**Response `data`** = `Model3DRequestQueueResponse`:
```json
{
  "items": [{
    "id": "guid", "productId": "guid", "productName": "...", "storeName": "...",
    "requestType": "Regenerate", "status": "AwaitingStaff",
    "sourceImageIds": [], "meshyTaskId": null, "assignedStaffId": null,
    "internalFailureReason": null, "nextAttemptAt": null, "rejectedReason": null,
    "createdAt": "...", "updatedAt": "..."
  }],
  "total": 1
}
```

---

## POST `/api/model3d-requests/{id}/generate` · POST `/api/model3d-requests/{id}/retry`

Cùng shape body và hành vi (retry chỉ khác ở chỗ request đã có `meshyTaskId` trước đó — dùng khi
chưa ưng ý kết quả, gọi lại với ảnh khác). `multipart/form-data`:

| Field | Kiểu | Ghi chú |
|---|---|---|
| `sourceImageIds` | `guid[]` | Ảnh sản phẩm có sẵn được tick |
| `newImages` | `file[]` | Ảnh mới upload — tự tạo `ProductImage` |

Tổng 1–4 ảnh (giới hạn Meshy multi-image-to-3d). Trả `202` + `data` = `Model3DRequestQueueItemResponse` (status chuyển `InProgress`). Nếu Meshy hết credit → `503` (staff thấy lỗi thật ngay, khác với luồng Initial tự động requeue âm thầm).

---

## GET `/api/model3d-requests/{id}/preview`

Live poll trực tiếp tới Meshy bằng `meshyTaskId` hiện tại của request — **không lưu DB**, dùng URL
tạm của Meshy (tự hết hạn) để xem trước trước khi quyết định accept hay retry.

**Response `data`** = `Model3DPreviewResponse`:
```json
{ "state": "Succeeded", "progress": 100,
  "thumbnailUrl": "https://assets.meshy.ai/.../preview.png?...",
  "glbUrl": "https://assets.meshy.ai/.../model.glb?...", "error": null }
```
`state`: `Running | Succeeded | Failed`. `glbUrl`/`thumbnailUrl` là link tạm của Meshy — chỉ dùng
xem trước, KHÔNG lưu lâu dài.

---

## POST `/api/model3d-requests/{id}/accept`

Yêu cầu request đang `InProgress` và task Meshy đã `SUCCEEDED` (service tự poll lại để xác nhận
trước khi ghi — trả `409` kèm `Model3DRequestTaskNotSucceeded` nếu chưa xong). Tải GLB từ Meshy,
**re-host vĩnh viễn** lên Supabase Storage (`Product_models/{productId}/{guid}.glb`), xóa file GLB
cũ (best-effort), ghi đè `ProductModel3D` của sản phẩm. Trả `data` = `ProductModel3DResponse`
(giống `GET /api/products/{id}/model-3d`).

---

## POST `/api/model3d-requests/{id}/reject`

Body:
```json
{ "reason": "Ảnh nguồn không đủ rõ để dựng model." }
```
Chuyển request sang `Rejected` — không ghi đè model hiện tại của sản phẩm.

---

[← Scoring Config](./25-scoring-config.md) · [Tiếp: Phụ lục →](./99-appendix-models.md)
