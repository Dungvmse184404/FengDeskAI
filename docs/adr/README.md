# ADR — Architecture Decision Records

Nhật ký các quyết định / thay đổi thực tế đã làm trong quá trình code: feature design, refactor, bugfix, tích hợp bên ngoài. Đây là **lịch sử "đã làm gì, vì sao"** — khác với `docs/ard/` (tài liệu tham chiếu mô tả kiến trúc **hiện tại** đang là gì).

> Khi đọc để hiểu kiến trúc hệ thống bây giờ, ưu tiên `docs/ard/architecture-core/` và `docs/ard/bounded-contexts/`. Chỉ mở file ở đây khi cần biết bối cảnh / lý do của một thay đổi cụ thể.

## Hạ tầng & quy trình

| File | Nội dung |
|---|---|
| [api-integration-testing.md](./api-integration-testing.md) | Bộ test API in-process + cổng chặn CI trước khi deploy VPS |
| [authorization-hardening.md](./authorization-hardening.md) | Siết phân quyền: policy, resource-based authorization, audit log |

## Feature design (trước khi code)

| File | Nội dung |
|---|---|
| [multi-role-workspace.md](./multi-role-workspace.md) | Switcher workspace theo role (Customer/Seller/Admin), chủ yếu FE |
| [feature-workspace-ai-intake.md](./feature-workspace-ai-intake.md) | AI parse mô tả không gian → điền workspace profile |
| [ai-order-tool-design.md](./ai-order-tool-design.md) | Thiết kế tool AI thao tác đơn hàng qua chat |
| [task-workspace-element-analysis.md](./task-workspace-element-analysis.md) | Phân tích ngũ hành từ input workspace |
| [product-placement-personal-recommendation.md](./product-placement-personal-recommendation.md) | `ProductPlacement` + gợi ý vật phẩm mang theo người (tool `recommend_personal_items`) |
| [product-template-shared-fengshui.md](./product-template-shared-fengshui.md) | **Proposal** — bản mẫu sản phẩm: nguồn phong thủy dùng chung giữa các shop |
| [platform-sku-generation.md](./platform-sku-generation.md) | Sinh SKU ở backend (`FD-XXXXXXXX`), bỏ 2 generator trùng lặp bên FE |
| [ai-chat-rewind-design.md](./ai-chat-rewind-design.md) | Rewind hội thoại AI: sửa & gửi lại tin của mình, cắt đuôi lịch sử |
| [workspace-element-insights-design.md](./workspace-element-insights-design.md) | Diễn giải ngũ hành phòng thành lời khuyên đọc được |
| [refactor-product-create-fengshui-flow.md](./refactor-product-create-fengshui-flow.md) | **Proposal** — luồng khai phong thủy khi tạo sản phẩm |
| [refactor-ai-tool-handles.md](./refactor-ai-tool-handles.md) | **Proposal (chưa code)** — handle cho AI tool thay vì truyền GUID trần |
| [product-item-size-class.md](./product-item-size-class.md) | `SizeClass` chuyển từ `Product` xuống `ProductItem` (kích thước biến thiên theo SKU) |

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
| [fix-ghn-create-shipment.md](./fix-ghn-create-shipment.md) | Tạo vận đơn GHN lỗi 400 "Lỗi lấy thông tin shop" (ShopId sai) + gọi HTTP trong transaction |
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
| [vibe-soft-scoring.md](./vibe-soft-scoring.md) | Vibe: bộ lọc cứng → tham số điểm (+ `MIN_SCORE_THRESHOLD`, kill-switch) |
| [personalized-recommendation-v3.1.md](./personalized-recommendation-v3.1.md) | **Proposal** — v3.1: trục cá nhân (`personalScore` × `Wp` theo `Scope`) cho luồng workspace + lọc `Aspiration` |

> v2 đã bị xóa (chỉ còn stub trỏ sang v3, không còn nội dung). Engine hiện tại: xem `docs/ard/bounded-contexts/customer-care.md`.

> `note.md` là **sổ tay tra cứu phong thủy** (bảng Nạp Âm, Bát Trạch, Cung Mệnh) mà `DestinyCalculator` lấy dữ liệu — không phải ADR, cố ý không xếp vào nhóm nào.
