# ADR — Architecture Decision Records

Nhật ký các quyết định / thay đổi thực tế đã làm trong quá trình code: feature design, refactor, bugfix, tích hợp bên ngoài. Đây là **lịch sử "đã làm gì, vì sao"** — khác với `docs/ard/` (tài liệu tham chiếu mô tả kiến trúc **hiện tại** đang là gì).

> Khi đọc để hiểu kiến trúc hệ thống bây giờ, ưu tiên `docs/ard/architecture-core/` và `docs/ard/bounded-contexts/`. Chỉ mở file ở đây khi cần biết bối cảnh / lý do của một thay đổi cụ thể.

## Hạ tầng & quy trình

| File | Nội dung |
|---|---|
| [api-integration-testing.md](./api-integration-testing.md) | Bộ test API in-process + cổng chặn CI trước khi deploy VPS |
| [authorization-hardening.md](./authorization-hardening.md) | Siết phân quyền: policy, resource-based authorization, audit log |
| [perf-scoring-config-cache.md](./perf-scoring-config-cache.md) | Đo tốc độ site thật: ảnh 7,9MB→57KB; cache cấu hình chấm điểm (`element-analysis` 10→5 lượt hỏi DB); bật RLS cho 4 bảng anon còn **TRUNCATE** được |
| [migration-squash-2026-09.md](./migration-squash-2026-09.md) | Gộp 43 migration đầu thành một baseline mang **id cũ** — remote chưa ở tip vẫn deploy được; quy trình lặp lại |

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
| [personalized-recommendation-v3.1.md](./personalized-recommendation-v3.1.md) | v3.1: trục cá nhân (`personalScore` × `Wp` theo `Scope`) cho luồng workspace + lọc `Aspiration` — **§3.3/§3.4 superseded một phần bởi v3.2** |
| [score-explainability-v3.2.md](./score-explainability-v3.2.md) | **v3.2 (ACCEPTED)** — chuẩn hoá `gapScore` về ±1.0, `PersonalConflictMode.Scaled` (L2), `ScoreBreakdown` + radar `priorityVector`, yếu tố nghề nghiệp |
| [occupation-product-fit-v1.md](./occupation-product-fit-v1.md) | **v3.4 (IMPLEMENTED 2026-09-11)** — nghề nghiệp thành trục thứ ba N3: `d = (1−Wp−Wo)·ĝ + Wp·r + Wo·ô` (phòng) và `(1−Wo)·n̂ + Wo·ô` (Carry); hồ sơ Σ=1 seed sẵn; % theo nghề ở trang sản phẩm; gỡ N1 |
| [personal-need-v3.6.md](./personal-need-v3.6.md) | **v3.6 (IMPLEMENTED 2026-09-20)** — luồng Carry: dụng thần có **kỵ thần** (suy từ Tứ Trụ / hành khắc mệnh), điểm = `Σ min(n̂,p) − Σ_{kỵ} p` thay tích trong ⇒ khớp hoàn hảo 100 %, gỡ trần 0.6 |
| [vendor-payout.md](./vendor-payout.md) | **ĐANG MỞ (2026-09-23)** — giữ tiền **7 ngày** sau khi giao (`PayoutPolicy.HoldDays`, bằng cửa sổ đổi trả); qua hạn thì `PayoutCreditService` cộng thẳng vào `users.balance` của chủ vườn **primary**. Sổ cái + lệnh rút + thông tin ngân hàng **chưa có** |
| [current-tag-votes-cap-v3.5.md](./current-tag-votes-cap-v3.5.md) | **v3.5 (IMPLEMENTED 2026-09-19)** — `TAG_VOTES_CAP = 5` chặn trần tổng phiếu tag trong `current`; `PERSONAL_WEIGHT_PRIVATE` 0.5 → 0.3, `SHARED` 0.3 → 0.2 để điểm không ngược radar |

> v2 đã bị xóa (chỉ còn stub trỏ sang v3, không còn nội dung). Engine hiện tại: xem [`docs/ard/bounded-contexts/customer-care.md`](../ard/bounded-contexts/customer-care.md).

> [`note.md`](./note.md) là **sổ tay tra cứu phong thủy** (bảng Nạp Âm, Bát Trạch, Cung Mệnh) mà `DestinyCalculator` lấy dữ liệu — không phải ADR, cố ý không xếp vào nhóm nào.
