# SEP490 — Checklist chuẩn bị tài liệu Review 2

> Trạng thái tự đánh giá dựa trên source code (`FengDeskAI` BE + `FengDeskAI_FE`, CodeGraph) và template `Documents/1_SEP490`, `Documents/2_SEP490`.
>
> Quy ước: `[x]` = đã có / đã hoàn thành · `[ ]` = chưa làm hoặc cần bổ sung.
>
> TRỌNG TÂM HIỆN TẠI: **Conceptual ERD** (xem mục 1.2 → 3.1.5).

---

## 1. Reports (theo template 1_SEP490 / 2_SEP490)

### 1.1. Report1 & Report2
- [x] `Report1_Project Introduction.docx`
- [x] `Report2_Project Management Plan.docx`
- (`FengDeskAI/docs/reports`)

### 1.2. Report3 Software Requirement Specification (SRS)
- [ ] 1. Product Overview
- [ ] 2. User Requirements
  - [ ] 2.1 Actors (Customer, GardenOwner, Staff, Manager, Admin)
  - [ ] 2.2 Use Cases — diagram + descriptions *(trang "Use case diagram" đã có trong drawio, cần đối chiếu)*
- [ ] 3. Functional Requirements
  - [ ] 3.1.1 Screens Flow · 3.1.2 Screen Descriptions · 3.1.3 Screen Authorization · 3.1.4 Non-Screen Functions
  - [ ] **3.1.5 Entity Relationship Diagram** ← ĐANG LÀM
    - [ ] **Conceptual ERD** — đang sửa: thêm Cart, Delivery, Payment, Return Request, Refund, Review, Notification, Conversation/Message, Category, Workspace Type, Address; bỏ "Report"; tách Feng Shui Attribute vs Feng Shui Rule
    - [x] Logical ERD đã có (`docs/erd` — "Logical Diagram" / "v2"); cần đối chiếu lại theo Domain entities
  - [ ] 3.2+ Đặc tả Feature/Function (theo từng controller/nghiệp vụ)
- [ ] 4. Non-Functional Requirements (4.1 External Interfaces, 4.2 Quality Attributes)
- [ ] 5. Requirement Appendix
  - [ ] 5.1 Business Rules · 5.2 Common Requirements
  - [ ] 5.3 Application Messages List *(tái dùng `ApiStatusMessages` + envelope lỗi trong code)*

### 1.3. Report4 — Software Design Document (SDD)
- [ ] 1.1 System Architecture — đã phân tích (Clean Architecture), **chưa vẽ diagram** (dùng C4 Container)
- [ ] 1.2 Package Diagram — chưa vẽ (`WebAPI / Application / Domain / Infrastructure` + sub-packages)
- [ ] 2. Database Design — ERD logical đã có; **thiếu data dictionary** (bảng-cột-kiểu-khóa)
- [ ] 3. Detailed Design — mỗi feature cần Class Diagram + Sequence Diagram (**phần lớn còn thiếu**; tái dùng `docs/api-documents` làm input)
- Record of Changes — **bỏ qua** (theo yêu cầu)

### 1.4. Sơ đồ bổ trợ (đặt trong SRS/SDD tùy mục)
- [ ] System Context Diagram (hệ thống + actor + external system: PayOS, GHN, AhaMove, Meshy, AI, SMTP, Storage) — **tách riêng khỏi Conceptual ERD**
- [ ] State Machine Diagrams — nội dung sẵn (Order/Delivery/Return/Payment/Refund/Model3D/Recommendation ở `docs/api-documents/99-appendix-models`), **chưa vẽ**
- [ ] Activity Diagrams / Flowcharts cho nghiệp vụ chính (đăng ký, checkout, thanh toán, giao hàng, RMA, gợi ý, chat) — **chưa vẽ**

---

## 2. Product / Tech

### 2.1. Dịch vụ bên thứ 3 (đã xác định từ code)
- [x] Authentication: JWT tự phát hành; OTP qua email SMTP (Gmail).
- [x] Storage: Supabase Storage.
- [x] Database hosting: Supabase (PostgreSQL).
- [x] Payment: PayOS.
- [x] Shipping: GHN (mặc định), AhaMove, Mock.
- [x] AI APIs: Ollama (chat LLM) + AI Recommendation client; Meshy AI (model 3D); Firebase AI (FE).
- [ ] Xác nhận provider AI ở môi trường thật (Ollama self-host hay đổi OpenAI/Gemini?).

### 2.2. Công nghệ Develop
- [x] Back-end: .NET 8, ASP.NET Core Web API, Clean Architecture, EF Core (Npgsql), SignalR.
- [x] Front-end: React 19 + TypeScript + Vite (pnpm), Redux Toolkit, TanStack Query, React Router, Tailwind, Three.js / react-three-fiber, Leaflet, i18next, axios, SignalR client.
- [x] Database: PostgreSQL.
- [x] Mobile: không có project mobile — ghi "Không áp dụng".

### 2.3. Source code & DevOps (thiếu dữ liệu)
- [ ] Dịch vụ quản lý source (GitHub / GitLab / Azure DevOps) — cần link repo.
- [ ] CI/CD pipeline — không thấy config; có `Dockerfile`. Cần xác nhận.

### 2.4. Môi trường Deploy (một phần)
- [x] Có `Dockerfile` (containerized).
- [ ] Chỉ có `appsettings.json` (Dev); chưa có cấu hình Staging/Production.
- [ ] Danh sách môi trường deploy thật + host + URL — cần cung cấp.

---

## 3. Team Contribution & Effort Tracking (thiếu dữ liệu)

> Có thể nằm trong `Documents/SE_CapstoneProject_SU26_Review1.xlsx` hoặc `1_SEP490/Project Weekly Report_GroupName.xlsx`.

- [ ] Danh sách thành viên: họ tên + vai trò.
- [ ] Phân công Development & Documentation từng người.
- [ ] Man-hour Week 1 → Week 8.
- [ ] Tỷ lệ đóng góp (%) theo effort.
- [ ] Ghi chú kết quả / mức độ hoàn thành.
- [ ] Form/template: xác nhận dùng `Project Weekly Report_GroupName.xlsx` (mẫu trong 1_SEP490) làm chuẩn.

---

## Việc làm ngay (không cần chờ dữ liệu)

1. **Sửa Conceptual ERD** (SRS 3.1.5) — đang làm.
2. Tách **System Context Diagram** riêng (có external system).
3. Vẽ **System Architecture + Package Diagram** (SDD 1.1, 1.2).
4. Đối chiếu Logical ERD + dựng **data dictionary** (SDD 2).

## Cần cung cấp thêm

| # | Hạng mục | Cần gì |
|---|----------|--------|
| 1 | Source & DevOps | Link repo (GitHub/GitLab/Azure), có CI/CD không |
| 2 | Môi trường Deploy | Danh sách env thật + host + URL |
| 3 | Team Contribution | Tên + vai trò + man-hour W1–W8 + %, hoặc xác nhận file Excel mẫu |
| 4 | AI provider | Môi trường thật dùng Ollama hay đổi sang dịch vụ khác |
