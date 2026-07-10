# ADR — Architecture Decision Records

Nhật ký các quyết định / thay đổi thực tế đã làm trong quá trình code: feature design, refactor, bugfix, tích hợp bên ngoài. Đây là **lịch sử "đã làm gì, vì sao"** — khác với `docs/ard/` (tài liệu tham chiếu mô tả kiến trúc **hiện tại** đang là gì).

> Khi đọc để hiểu kiến trúc hệ thống bây giờ, ưu tiên `docs/ard/architecture-core/` và `docs/ard/bounded-contexts/`. Chỉ mở file ở đây khi cần biết bối cảnh / lý do của một thay đổi cụ thể.

## Feature design (trước khi code)

| File | Nội dung |
|---|---|
| [multi-role-workspace.md](./multi-role-workspace.md) | Switcher workspace theo role (Customer/Seller/Admin), chủ yếu FE |
| [feature-workspace-ai-intake.md](./feature-workspace-ai-intake.md) | AI parse mô tả không gian → điền workspace profile |
| [ai-order-tool-design.md](./ai-order-tool-design.md) | Thiết kế tool AI thao tác đơn hàng qua chat |
| [task-workspace-element-analysis.md](./task-workspace-element-analysis.md) | Phân tích ngũ hành từ input workspace |

## Refactor

| File | Nội dung |
|---|---|
| [refactor-ai-activity-module.md](./refactor-ai-activity-module.md) | Tách trạng thái AI realtime (`aiStatus`) thành module dùng chung |
| [refactor-create-shipment-flow.md](./refactor-create-shipment-flow.md) | Luồng tạo shipment (GHN/Ahamove) |
| [refactor-garden-staff-management.md](./refactor-garden-staff-management.md) | Quản lý staff theo store (invitation flow) |
| [refactor-model3d-request-flow.md](./refactor-model3d-request-flow.md) | Luồng request sinh model 3D (Meshy) |
| [refactor-workspace-input-relaxation.md](./refactor-workspace-input-relaxation.md) | Nới lỏng validate input workspace |

## Fix / hotfix

| File | Nội dung |
|---|---|
| [fix-garden-owner-flow.md](./fix-garden-owner-flow.md) | Luồng tự nâng cấp Garden Owner |
| [fix-shipping-fee-preview.md](./fix-shipping-fee-preview.md) | Preview phí ship trước khi đặt hàng |
| [fix-staff-seller-access.md](./fix-staff-seller-access.md) | Quyền truy cập của staff trong kênh người bán |
| [fix-store-address.md](./fix-store-address.md) | Địa chỉ garden store |
| [hotfix-product-image-link.md](./hotfix-product-image-link.md) | Link ảnh sản phẩm |

## Tích hợp bên ngoài

| File | Nội dung |
|---|---|
| [ghn-integration.md](./ghn-integration.md) | Giao Hàng Nhanh — fee API + webhook |
| [ahamove-integration.md](./ahamove-integration.md) | Ahamove — fee API + webhook |

## Recommendation engine — lịch sử phiên bản

| File | Nội dung |
|---|---|
| [recommendation-scoring-v3.md](./recommendation-scoring-v3.md) | Engine chấm điểm phong thủy v3 |
| [recommendation-scoring-v4-polarity.md](./recommendation-scoring-v4-polarity.md) | v4 — thêm polarity (tương sinh/tương khắc) |

> v2 đã bị xóa (chỉ còn stub trỏ sang v3, không còn nội dung). Engine hiện tại: xem `docs/ard/bounded-contexts/customer-care.md`.
