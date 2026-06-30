# Tích hợp Multi-role Workspace (FE)

> Mục tiêu: 1 tài khoản có thể đồng thời nhiều vai trò (Customer + Garden Owner, hoặc Staff/Admin).
> Giao diện chia theo **"khu làm việc" (workspace)** — chọn qua switcher "Đổi khu" (như ảnh mockup),
> mỗi khu là một layout + vùng route riêng. Phần lớn là việc **Frontend** (`FengDeskAI_FE`); backend chỉ cần
> cung cấp đúng danh sách role của user.

## Bối cảnh

- User đăng ký mặc định là **Customer**; có thể tự nâng cấp thành **Garden Owner** (self-service, xem `FIX_GARDEN_OWNER_FLOW.md`).
- `UserRole` là **bit-flag** → một user giữ nhiều role cùng lúc (vd `Customer | GardenOwner`).
- Hiện tại menu trộn lẫn mục mua hàng (Đơn mua) với mục bán hàng (Kênh người bán) → cần tách theo workspace.

---

## 1. Mô hình Workspace

| Workspace | Route namespace | Hiện khi user có role | Nội dung chính |
|---|---|---|---|
| **Mua sắm** | `/` | luôn (mọi user là Customer) | trang chủ, sản phẩm, giỏ, đơn mua, chat với shop |
| **Kênh người bán** | `/seller` | `GardenOwner` hoặc `Staff` | sản phẩm của store, đơn cần giao (delivery), kho, chat khách |
| **Quản trị** | `/admin` | `Admin` / `Manager` | quản lý hệ thống, người dùng, danh mục, báo cáo |

Nguyên tắc: **mỗi workspace = 1 layout + 1 nhánh route riêng**, để UI mua sắm gọn cho số đông (đa số chỉ là customer).

---

## 2. Switcher "Đổi khu"

- Đặt ở header. **Chỉ hiện các khu user có quyền** — không show cứng cả 3.
  - Customer thuần → switcher chỉ có **Mua sắm** (hoặc ẩn hẳn switcher).
  - Có thêm role bán → hiện thêm **Kênh người bán**; Admin → hiện **Quản trị**.
- Đổi khu = điều hướng sang route namespace tương ứng + đổi layout.

```tsx
const WORKSPACES = [
  { key: 'shop',   label: 'Mua sắm',         route: '/',       allow: (r) => true },
  { key: 'seller', label: 'Kênh người bán',  route: '/seller', allow: (r) => r.isOwner || r.isStaff },
  { key: 'admin',  label: 'Quản trị',        route: '/admin',  allow: (r) => r.isAdmin },
];
const visible = WORKSPACES.filter(w => w.allow(roles));
// chỉ render switcher khi visible.length > 1
```

---

## 3. Tách 2 menu (tránh trùng)

| Menu | Vai trò | Mục |
|---|---|---|
| **Avatar menu** (góc phải) | mức **tài khoản** | Tài khoản của tôi, Đơn mua, Thông báo, Đăng xuất |
| **Switcher "Đổi khu"** | chuyển **khu làm việc** | Mua sắm / Kênh người bán / Quản trị |

> Hiện "Kênh người bán" đang nằm ở **cả 2 chỗ** → chọn **switcher làm nơi chính**, bỏ khỏi avatar menu
> (hoặc giữ làm shortcut nhưng phải nhất quán, đừng để 2 đường dẫn khác nhau).

---

## 4. Màn hình mặc định khi đăng nhập

```ts
function defaultWorkspace(roles, lastUsed) {
  if (lastUsed && isAllowed(lastUsed, roles)) return lastUsed;   // ưu tiên khu dùng lần cuối
  if (roles.isCustomer) return 'shop';     // số đông là customer → Mua sắm
  if (roles.isAdmin)    return 'admin';
  if (roles.isOwner || roles.isStaff) return 'seller';
  return 'shop';
}
```

- Mọi user thường → **Mua sắm** (mẫu số chung, an toàn).
- Vừa customer vừa owner → mặc định **Mua sắm**, nhưng **nhớ lần cuối**: hay vào bán hàng thì lần sau vào thẳng Kênh người bán.
- User **không phải** customer (chỉ staff / admin nội bộ) → vào thẳng khu công việc của họ.

> Phương án tối giản: bỏ `lastUsed`, luôn vào theo `defaultByRole`. Đủ dùng vì đa số là customer; nhớ lần cuối chỉ là tiện thêm.

---

## 5. Lưu `lastUsedWorkspace`

Đây là **preference UI** → lưu **`localStorage`** (FE), không cần đụng backend.

```ts
// khi đổi khu
localStorage.setItem('lastWorkspace', key);
// khi load app
const last = localStorage.getItem('lastWorkspace');
const ws = isAllowed(last, roles) ? last : defaultWorkspace(roles, null);
```

- **Luôn validate lại theo quyền hiện tại** (`isAllowed`) — phòng trường hợp user từng là seller, nay bị gỡ quyền mà localStorage vẫn còn `'seller'`.

| Nơi lưu | Ưu | Nhược |
|---|---|---|
| **localStorage** (khuyến nghị) | đơn giản, không sửa BE | chỉ theo từng trình duyệt/thiết bị |
| DB (user profile) | đồng bộ mọi thiết bị | thêm field + API; nặng cho 1 preference nhỏ |
| JWT claim | có sẵn lúc login | **không nên** — token bất biến, đổi khu liên tục không hợp |

---

## 6. Backend cần gì

Phần lớn là FE. Backend chỉ cần **trả đúng danh sách role** để FE quyết định workspace:

- `GET /api/auth/me` (hoặc claim trong JWT) phải chứa role của user (vd `roles: ["Customer","GardenOwner"]` hoặc bit-flag).
- **Authorization ở BE vẫn là chốt chặn thật** — ẩn UI chỉ là trải nghiệm; endpoint `/seller`, `/admin` phải được bảo vệ bằng role tương ứng (đã có `[Authorize(Roles=...)]` / guard ở service).

> Nếu `me`/token chưa expose role rõ ràng, đây là thay đổi BE duy nhất cần làm cho tính năng này.

---

## 7. Checklist

**Frontend (`FengDeskAI_FE`):**

- [ ] Định nghĩa `WORKSPACES` + hàm `isAllowed`, `defaultWorkspace` (mục 2 & 4).
- [ ] Header: switcher "Đổi khu" lọc theo role; chỉ render khi có >1 khu.
- [ ] Tách route theo namespace: `/`, `/seller`, `/admin` + layout riêng.
- [ ] Route guard mỗi namespace theo role; redirect về `defaultWorkspace` nếu không đủ quyền.
- [ ] Lưu/đọc `lastWorkspace` ở localStorage (mục 5), validate theo quyền.
- [ ] Dọn avatar menu: bỏ mục bán hàng khỏi avatar, chuyển về switcher (mục 3).

**Backend:**

- [ ] Đảm bảo `GET /api/auth/me` (hoặc JWT) trả role của user cho FE.
- [ ] Xác nhận các endpoint seller/admin đã được authorize đúng role.
