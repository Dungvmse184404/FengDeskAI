# Fix — Garden Staff (đã Accept) không vào được khu Người bán (`/seller`)

> Triệu chứng: user đã **Accept** lời mời làm garden staff nhưng trang seller không load store của owner đã mời; thậm chí không vào được `/seller`.

---

## Nguyên nhân (chuỗi 4 điểm)

1. **BE — `/stores/mine` chỉ trả store của OWNER.**
   `StoreService.GetMineAsync` → `StoreRepository.GetByOwnerAsync` (`StoreRepository.cs:41-47`):
   ```csharp
   .Where(s => s.Owners.Any(o => o.OwnerUserId == ownerUserId))  // chỉ owner
   ```
   → staff (không có dòng owner) → store không nằm trong list.

2. **BE — `/stores/staff/invitations/mine` chỉ trả `Pending`.**
   `GetPendingInvitationsForUserAsync` (`StoreRepository.cs:110`): `a.Status == InvitationStatus.Pending`.
   → sau khi Accept, lời mời thành `Accepted` và **biến mất** khỏi endpoint.

3. **FE — quyền vào seller lại suy từ endpoint (2).**
   `useShopStaff.ts:82`:
   ```ts
   hasSellerWorkspaceAccess: invitations.some(inv => inv.status === "Accepted")
   ```
   `invitations` lấy từ endpoint chỉ-Pending → luôn **false** cho staff đã accept.

4. **FE — guard `/seller` khoá cứng theo role `GardenOwner`.**
   `ProtectedRoute.tsx:61` `if (requireGardenOwner && !has("GardenOwner"))` → đá về `/become-seller`.
   Staff accept **không** có role flag `GardenOwner` → bị chặn.

→ Nguồn dữ liệu chuẩn để biết "store nào mình được vào" phải là **`/stores/mine` (owner + accepted-staff)**, không phải endpoint lời mời.

---

## Fix Backend

### B1. `IStoreRepository` — thêm signature
`FengDeskAI.Application/.../Interfaces/Repositories/IStoreRepository.cs` (cạnh `GetByOwnerAsync`):
```csharp
/// <summary>Store mà user là owner HOẶC garden staff đã Accepted (dùng cho /stores/mine).</summary>
Task<List<GardenStore>> GetForUserAsync(Guid userId, CancellationToken ct = default);
```

### B2. `StoreRepository` — thêm method
`FengDeskAI.Infrastructure/Persistence/Repositories/StoreRepository.cs` (cạnh `GetByOwnerAsync`, dòng ~47):
```csharp
public Task<List<GardenStore>> GetForUserAsync(Guid userId, CancellationToken ct = default)
    => _set.AsNoTracking()
        .Where(s => s.Owners.Any(o => o.OwnerUserId == userId)
            || _context.Set<GardenStaffAssignment>().Any(a =>
                   a.GardenStoreId == s.Id
                   && a.StaffId == userId
                   && a.Status == InvitationStatus.Accepted))
        .Include(s => s.Address).ThenInclude(a => a!.Ward)
        .Include(s => s.Owners)
        .OrderByDescending(s => s.CreatedAt)
        .ToListAsync(ct);
```

### B3. `StoreService.GetMineAsync` — gọi method mới
`FengDeskAI.Application/Features/Vendor/Services/StoreService.cs:411`:
```csharp
public async Task<IServiceResult<List<StoreResponse>>> GetMineAsync(Guid userId, CancellationToken ct = default)
    => ServiceResult<List<StoreResponse>>.Success(
        _mapper.Map<List<StoreResponse>>(await _uow.Stores.GetForUserAsync(userId, ct)));  // đổi GetByOwnerAsync → GetForUserAsync
```

> (Tùy chọn) Thêm `bool IsOwner` vào `StoreResponse` (tính = user có trong `Owners`) để FE **ẩn** nút owner-only (sửa/đóng store, thêm staff) đối với staff. BE vẫn chặn 403 ở service nếu staff cố gọi.

---

## Fix Frontend

### F1. `useHasSellerWorkspaceAccess` — suy từ `/stores/mine`, không từ pending-invitations
`features/shop/hooks/useShopStaff.ts:79-87` — thay bằng:
```ts
import { getMyShopsRequest } from "../api/shop.api";

export function useHasSellerWorkspaceAccess(enabled = true) {
  const query = useQuery({
    queryKey: ["my-shops"],
    queryFn: async () => {
      const res = await getMyShopsRequest();
      if (!res.isSuccess) throw new Error(res.message || "Không thể tải cửa hàng");
      return res.data ?? [];
    },
    enabled,
  });
  const shops = query.data ?? [];
  return {
    hasSellerWorkspaceAccess: shops.length > 0,   // có ≥1 store (owner hoặc accepted-staff)
    shops,
    isLoading: query.isLoading,
    error: query.error,
  };
}
```
> Các nơi dùng (`WorkspaceSwitcher`, `Navbar`) chỉ đọc `hasSellerWorkspaceAccess` nên không vỡ. Nếu chỗ nào đang đọc `invitations` từ hook này thì đổi sang `useMyStoreInvitations` trực tiếp.

### F2. Guard `/seller` — cho phép owner HOẶC accepted-staff
`app/ProtectedRoute.tsx` — thêm prop + check (đừng redirect khi đang loading):
```tsx
requireSellerAccess?: boolean;
// ...
const { hasSellerWorkspaceAccess, isLoading: sellerLoading } =
  useHasSellerWorkspaceAccess(requireSellerAccess === true);
// ... sau các check auth:
if (requireSellerAccess) {
  if (sellerLoading) return <LoadingScreen />;                 // tránh đá nhầm khi chưa tải xong
  if (!has("GardenOwner") && !hasSellerWorkspaceAccess)
    return <Navigate to="/become-seller" replace />;
}
```
`app/router.tsx` — các route `/seller`, `/seller/:storeId/deliveries`, `/seller/:storeId/staff`: đổi `requireGardenOwner` → `requireSellerAccess`.

> `workspace.ts:25` (`allow: r => r.includes("GardenOwner")`) và `:74` để nguyên cũng được vì `WorkspaceSwitcher` đã tự thêm khu seller qua `hasSellerWorkspaceAccess`. Nếu muốn gọn, có thể để switcher là nguồn duy nhất quyết định hiển thị.

---

## Kiểm thử

- [ ] Owner: `/stores/mine` vẫn trả store của mình; vào `/seller` bình thường.
- [ ] Staff **chưa** accept (Pending): **không** thấy khu seller, `/stores/mine` không chứa store đó.
- [ ] Staff **đã** accept: `/stores/mine` **có** store của owner; khu "Kênh người bán" hiện trong switcher; vào `/seller` được; xem/nhận đơn + ship của store đó.
- [ ] Staff **bị gỡ** (Revoked) hoặc **Reject**: mất store khỏi `/stores/mine`, mất quyền vào seller.
- [ ] Owner-only actions (sửa/đóng store, thêm staff): staff bấm → BE trả `403` (và FE ẩn nếu đã thêm `IsOwner`).

---

## Checklist

**Backend**
- [ ] `IStoreRepository.GetForUserAsync` (interface).
- [ ] `StoreRepository.GetForUserAsync` (owner OR `Status==Accepted`).
- [ ] `StoreService.GetMineAsync` gọi `GetForUserAsync`.
- [ ] (Tùy chọn) `StoreResponse.IsOwner` + mapping.
- [ ] Cập nhật `docs/api-documents/15-stores.md` (mô tả `/stores/mine` gồm cả accepted-staff).

**Frontend**
- [ ] Sửa `useHasSellerWorkspaceAccess` dùng `getMyShopsRequest`.
- [ ] `ProtectedRoute` thêm `requireSellerAccess` (+ loading guard).
- [ ] `router.tsx` đổi guard các route `/seller` sang `requireSellerAccess`.
- [ ] (Tùy chọn) Ẩn nút owner-only trong seller UI khi `!isOwner`.

**Nguyên tắc**
- Nguồn sự thật cho "được vào seller" = `/stores/mine` (owner + accepted-staff), **không** phải endpoint lời mời (chỉ Pending).
- Không thêm role `GardenStaff` global; quyền vẫn store-scoped qua `GardenStaffAssignment.Status == Accepted`.
