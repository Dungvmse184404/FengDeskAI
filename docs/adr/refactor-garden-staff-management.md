# Refactor — Garden Owner: Quản lý nhân viên (Garden Staff)

> Mục tiêu: Garden Owner mời nhân viên (yêu cầu **đồng ý**) thay vì gán thẳng, có **tìm user** kiểu GitHub (email/tên có dấu/SDT). FE mới + BE bổ sung schema/endpoints.

---

## 1. Hiện trạng (code đã có)
- API: `GET/POST/DELETE /api/stores/{id}/staff` (quyền Owner/Admin), entity `GardenStaffAssignment(GardenStoreId, StaffId, AssignedBy, IsActive, AssignedAt, UnassignedAt)`.
- Quyền store-scoped: owner toàn quyền; staff chỉ nhận đơn + ship (enforce qua membership).
- **Thiếu:** tìm user, luồng mời/đồng ý, thông báo, và FE cho garden owner.

> Quyết định thiết kế: **không** thêm role `GardenStaff` vào `UserRole`. Vẫn dùng membership theo store (đúng cho multi-store).

---

## 2. Tính năng mới cần làm
1. **Tìm user kiểu GitHub:** gõ **email / họ tên (có dấu) / số điện thoại** → filter ra danh sách user phù hợp (debounce, dropdown).
2. **Mời + đồng ý:** owner mời → assignment ở trạng thái **Pending** + gửi **Notification** cho người được mời. Người đó **Accept** mới có quyền; **Reject** thì không. Pending = chưa có quyền.

---

## 3. Backend

### 3.1. Tìm user (GitHub-style search)
**Endpoint mới:** `GET /api/users/search?q={term}&limit=10` — quyền **GardenOwner trở lên** (hoặc Staff+).
- Match khi `term` khớp **email** HOẶC **fullName** HOẶC **phone** (substring, không phân biệt hoa/thường).
- **Không phân biệt dấu tiếng Việt:** "nguyen" khớp "Nguyễn", "Nguyễn" khớp "Nguyen".
- Trả tối đa `limit` kết quả, **chỉ field công khai tối thiểu**.

**Xử lý không dấu (PostgreSQL) — chọn 1:**
- **Cách A (khuyên dùng): cột chuẩn hoá.** Thêm cột `users.search_normalized` = lowercase + bỏ dấu (gồm đ→d) của `full_name + ' ' + email + ' ' + phone`, cập nhật khi tạo/sửa user; tạo **index** (`pg_trgm` GIN) để search nhanh. Query: `WHERE search_normalized LIKE '%' || normalize(@q) || '%'`. Chuẩn hoá `@q` ở C# (bỏ dấu Unicode `FormD` + thay `đ/Đ`).
- **Cách B: `unaccent`.** Bật extension `unaccent` + `pg_trgm`; query `WHERE unaccent(lower(full_name)) LIKE unaccent(lower(@q)) ...`. Lưu ý `unaccent` mặc định **không** xử lý `đ→d` — cần thêm rule hoặc normalize phía C#.

**Hàm bỏ dấu (C#) tham khảo:**
```csharp
static string RemoveDiacritics(string s)
{
    s = s.Replace('đ','d').Replace('Đ','D');
    var norm = s.Normalize(NormalizationForm.FormD);
    var sb = new StringBuilder();
    foreach (var c in norm)
        if (CharUnicodeCategory.NonSpacingMark != CharUnicodeInfo.GetUnicodeCategory(c)) sb.Append(c);
    return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
}
```

**Response `data`** = mảng:
```json
[{ "id": "guid", "fullName": "Nguyễn Văn B", "email": "b@example.com", "phone": "0901234567" }]
```

**Lưu ý quyền riêng tư (nên ghi vào Business Rule):**
- Yêu cầu `q` tối thiểu **3 ký tự**; rate-limit; chỉ trả field tối thiểu (không trả ngày sinh, balance...).
- (Tùy chọn) đánh dấu user đã là thành viên / đang được mời để FE disable.

### 3.2. Đổi `GardenStaffAssignment` thành mô hình LỜI MỜI
Thêm trạng thái + mốc thời gian:
```csharp
public class GardenStaffAssignment : BaseEntity
{
    public Guid GardenStoreId { get; set; }
    public Guid StaffId { get; set; }            // người được mời
    public Guid InvitedBy { get; set; }          // (đổi tên từ AssignedBy)
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending; // + mới
    public DateTime InvitedAt { get; set; }       // (đổi tên từ AssignedAt)
    public DateTime? RespondedAt { get; set; }    // + mới: lúc accept/reject
    public DateTime? UnassignedAt { get; set; }   // lúc owner gỡ
    public GardenStore Store { get; set; } = null!;
}
```
```csharp
public enum InvitationStatus { Pending, Accepted, Rejected, Revoked }
```
> `IsActive` cũ → suy ra từ `Status == Accepted` (bỏ cột, hoặc giữ và đồng bộ).

### 3.3. Quy tắc quyền (QUAN TRỌNG)
Hàm kiểm tra "user có quyền store" (`IsOwnerOrStaffAsync` / `HasStoreAccessAsync`) phải chỉ tính **Accepted**:
```
owner  → có quyền
staff  → CHỈ khi assignment.Status == Accepted
Pending/Rejected/Revoked → KHÔNG có quyền
```

### 3.4. State machine lời mời
```
(owner mời)        (staff accept)
   Pending ───────────► Accepted ──(owner gỡ)──► Revoked
      │
      ├──(staff reject)──► Rejected
      └──(owner huỷ lời mời / gỡ)──► Revoked
```

### 3.5. Endpoints
| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/users/search?q=&limit=` | Owner+ | Tìm user theo email/tên(có dấu)/phone |
| POST | `/api/stores/{id}/staff` | Owner/Admin | **Mời** nhân viên → tạo `Pending` + gửi Notification |
| GET | `/api/stores/{id}/staff` | Owner/Admin | Danh sách nhân viên + **trạng thái** (Pending/Accepted) |
| DELETE | `/api/stores/{id}/staff/{assignmentId}` | Owner/Admin | Gỡ / huỷ lời mời → `Revoked` |
| GET | `/api/stores/staff/invitations/mine` | Authenticated | Lời mời **gửi cho tôi** (Pending) |
| POST | `/api/stores/staff/{assignmentId}/accept` | Người được mời | Đồng ý → `Accepted` |
| POST | `/api/stores/staff/{assignmentId}/reject` | Người được mời | Từ chối → `Rejected` |

- `POST /{id}/staff` body: `{ "staffId": "guid" }` (FE lấy từ search). Check: owner/admin, user tồn tại, chưa có assignment Pending/Accepted (`409 AlreadyAssigned/AlreadyInvited`), không mời chính owner.
- Accept/Reject chỉ cho **đúng người được mời** (so `StaffId == CurrentUserId`) và assignment đang `Pending`.

### 3.6. Notification
- Khi **mời**: tạo `Notification` cho `StaffId` → `NotificationType.StaffInvited`, `ReferenceType.StaffInvitation` (enum mới) hoặc `Store`, `ReferenceId = assignmentId`. Title/Message: "Bạn được mời làm nhân viên của {storeName}".
- (Tùy chọn) Khi **accept/reject**: notify lại owner (`StaffInvitationAccepted/Rejected`).
- Realtime: đẩy qua SignalR (đã có hub notification) để badge hiện ngay.

---

## 4. Frontend

### 4.1. Owner — mời & quản lý (`features/garden-owner/`)
```
api/storeStaff.api.ts     # searchUsers, inviteStaff, getStaff, revokeStaff
hooks/useStoreStaff.ts
pages/StoreStaffPage.tsx
components/
  StaffTable.tsx          # cột: tên, email, phone, trạng thái(Pending/Accepted), ngày, [Gỡ]
  InviteStaffModal.tsx     # ô search kiểu GitHub
  UserSearchCombobox.tsx   # debounce 300ms → searchUsers → dropdown (avatar/tên/email)
```
- **UserSearchCombobox** (kiểu GitHub): gõ → debounce → `GET /users/search?q=` → dropdown list; chọn 1 user → `inviteStaff(storeId, staffId)`. Disable user đã là thành viên/đang mời.
- **StaffTable**: hiển thị badge trạng thái (`Pending` = chờ đồng ý, `Accepted` = đang làm). Nút "Gỡ"/"Huỷ lời mời".

### 4.2. Người được mời — đồng ý/từ chối
- Tận dụng **Notification** sẵn có: click thông báo "Lời mời làm nhân viên" → mở `MyInvitationsPage` hoặc dialog.
```
features/invitations/
  pages/MyInvitationsPage.tsx
  components/InvitationCard.tsx   # tên store + người mời + [Đồng ý] [Từ chối]
  hooks/useMyInvitations.ts       # getMine, accept, reject
```
- Sau **Accept** → invalidate; khu vực garden (nhận đơn/ship của store) xuất hiện. Sau **Reject** → ẩn lời mời.

### 4.3. Guard
- Trang owner: chỉ owner store/Admin.
- Khu nhận đơn/ship của staff: chỉ hiện khi có assignment **Accepted** với store đó.

---

## 5. Acceptance Criteria

- [ ] Owner gõ email/tên(có dấu)/phone → ra đúng người; "nguyen" khớp "Nguyễn".
- [ ] Search yêu cầu ≥ 3 ký tự; chỉ trả field tối thiểu (không lộ PII nhạy cảm).
- [ ] Mời → người được mời thấy **Notification**; assignment ở `Pending`.
- [ ] Khi **Pending**, người được mời **chưa** vào/thao tác được store.
- [ ] **Accept** → có quyền nhận đơn/ship store đó; **Reject** → không.
- [ ] Owner thấy trạng thái từng nhân viên (Pending/Accepted); gỡ được → `Revoked`, mất quyền.
- [ ] Không mời trùng (đang Pending/Accepted) → `409`.
- [ ] Accept/Reject chỉ đúng người được mời, chỉ khi `Pending`.

---

## 6. Checklist công việc

**Backend**
- [ ] Endpoint `GET /api/users/search` (+ chuẩn hoá bỏ dấu: cột `search_normalized` + `pg_trgm` GIN index, hoặc `unaccent`).
- [ ] Enum `StaffInvitationStatus`; cập nhật `GardenStaffAssignment` (Status, InvitedBy, InvitedAt, RespondedAt) + migration.
- [ ] Sửa hàm quyền store: staff chỉ tính `Accepted`.
- [ ] Endpoints invite/list/revoke + invitations/mine + accept/reject; validate state machine.
- [ ] `NotificationType.StaffInvited` (+ accepted/rejected), `ReferenceType.StaffInvitation`; gửi noti khi mời (+ SignalR).
- [ ] Enrich response staff: name/email/phone + status.
- [ ] Cập nhật `docs/api-documents/15-stores.md` + `99-appendix-models.md` (enum mới).

**Frontend**
- [ ] `UserSearchCombobox` (debounce, dropdown kiểu GitHub) + `InviteStaffModal`.
- [ ] `StaffTable` có cột trạng thái + gỡ/huỷ.
- [ ] `MyInvitationsPage` + `InvitationCard` (accept/reject), nối từ Notification.
- [ ] Guard owner / staff-accepted.
- [ ] Toast + xử lý lỗi từ envelope `ServiceResult.message`.

**Kiểm thử**
- [ ] Search có dấu/không dấu, email, phone.
- [ ] Mời → Pending → Accept → có quyền; Reject → không quyền.
- [ ] Mời trùng / accept hộ người khác / accept khi không Pending → lỗi đúng.
- [ ] Owner gỡ nhân viên Accepted → mất quyền ngay.

---

## 7. Ghi chú thiết kế
- Không thêm role `GardenStaff` global; quyền vẫn store-scoped membership.
- `GardenStaffAssignment` giờ vừa là **lời mời** vừa là **membership** (qua `Status`). Trong ERD/SDD: thêm cột `status`, `responded_at`; chú thích state machine.
- Cập nhật Report3: BR mới cho "mời nhân viên cần đồng ý", "search ≥ 3 ký tự / không lộ PII"; thêm use case "Accept/Reject lời mời".
