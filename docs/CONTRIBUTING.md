# HƯỚNG DẪN ĐÓNG GÓP CHO CÁC THÀNH VIÊN (CONTRIBUTING GUIDELINES)

Guidlines này nhằm mục đích để đảm bảo chất lượng code và tránh xung đột, dưới đây là những nguyên tắc đặt tên cho các Branch, Pull Request (PR), và Commit.

---
## 1. Quy tắc phân nhánh (Branching Strategy)
Chúng tôi sử dụng mô hình Git cơ bản. **Tuyệt đối không push trực tiếp lên nhánh `main` hoặc `develop`.**
- **`main`**: Chứa code đã release, ổn định 100%.
- **`develop`**: Nhánh tích hợp chính cho môi trường dev.
- **Các branch**: Phải được rẽ nhánh từ `develop` với cú pháp:
  - **`feature/`** hoặc **`feat/`**: Phát triển một **tính năng mới**.
  - **`bugfix/`** hoặc **`fix/`**: Sửa một **lỗi thông thường**.
  - **`hotfix/`**: Sửa lỗi **khẩn cấp trên production**.
  - **`docs/`**: Chỉ cập nhật, thêm mới **tài liệu**.
  - **`refactor/`**: Tái cấu trúc, dọn dẹp code nhưng **không làm thay đổi logic**.
  - **`chore/`**: Cập nhật **cấu hình**, CI/CD, hoặc dependencies.
**Ví dụ:** 
- `feature/assign-staff`
- `chore/update-npm-packages`

---
## 2. Quy ước đặt tên Commit (Conventional Commits)
Mọi commit message **BẮT BUỘC** viết bằng **Tiếng Anh** và theo cấu trúc:
`<type>(<scope>): <mô tả ngắn>`

**`<scope>`:** phạm vi ảnh hưởng của lần commit này

**Các `<type>` hợp lệ:**
- **`feat`**: Thêm một **tính năng mới**. 
- **`fix`**: **Sửa lỗi** (bug fix). 
- **`docs`**: Chỉ thay đổi **tài liệu** (documentation). 
- **`style`**: Thay đổi **không ảnh hưởng đến logic code** (khoảng trắng, định dạng format, thiếu dấu chấm phẩy, v.v.). 
- **`refactor`**: Thay đổi code **không thêm tính năng cũng không sửa lỗi** (cấu trúc lại code cho sạch/dễ đọc hơn). 
- **`perf`**: Thay đổi code giúp **cải thiện hiệu suất** (performance). 
- **`test`**: Thêm các **file test bị thiếu** hoặc sửa các test hiện có. 
- **`chore`**: Thay đổi **quá trình build**, công cụ phụ trợ, hoặc cấu hình thư viện.

**Ví dụ:** 
- `feat(store): add assign staff endpoint`
- `fix(db): remove missing invited_at column in LINQ query`

---
## 3. Quy trình làm việc (Workflow)
1. **Pull** code mới nhất từ nhánh `develop` về máy.
2. Tạo nhánh mới (`feature/...` hoặc `bugfix/...`).
3. Thực hiện thay đổi (Tuân thủ Clean Architecture và không phá vỡ UI/UX hiện tại).
4. **Viết/Chạy Test** (Nếu có) để đảm bảo các tính năng cũ không bị ảnh hưởng.
5. Commit code theo đúng chuẩn.
6. Push nhánh lên repository và tạo **Pull Request (PR)** hướng vào nhánh `develop`.

---
## 4. Checklist trước khi tạo Pull Request
- [ ] Code đã tự chạy thử và không có lỗi (Zero errors).
- [ ] Đã dọn dẹp các dòng `console.log()` hoặc comment nháp.
- [ ] Đã cập nhật tài liệu trong thư mục `/docs` nếu có thay đổi về API hoặc Database.
- [ ] Đã tự review lại code của chính mình (Self-review).

---
## 5. Quy tắc xử lý khi một Commit có quá nhiều thay đổi

Nguyên tắc cốt lõi là: **Mỗi commit chỉ nên giải quyết MỘT tác vụ duy nhất**. Nếu lỡ sửa nhiều thứ cùng lúc (quên commit sau mỗi thay đổi), hãy chọn một trong hai phương án đơn giản sau:
### Option 1: Tách nhỏ Commit bằng giao diện (Khuyên dùng)
Thay vì gõ lệnh `git add .` bừa bãi, hãy dùng giao diện trực quan của IDE (VS Code, Visual Studio, GitKraken):
1. Tích chọn (Stage) những file thuộc về tính năng mới -> Commit với type `feat`.
2. Tích chọn (Stage) những file thuộc về phần dọn dẹp code -> Commit với type `refactor`.

### Option 2: Luật 80/20 - Ưu tiên thay đổi lớn nhất 
Nếu các **thay đổi đan xen quá phức tạp** trong cùng một file và không thể tách rời:
- **Xác định mục đích chính:** Nhìn lại xem mục đích chính của lần ngồi code này là gì (Thêm tính năng hay Sửa lỗi?).
- **Chọn 1 Type duy nhất:** Chọn Type đại diện cho 80% khối lượng công việc.
  - *Ví dụ:* Bạn vừa thêm tính năng `Assign Staff` vừa tiện tay xóa vài dòng code thừa ở module cũ. Hãy chọn `feat(store): add assign staff endpoint` (bỏ qua phần refactor nhỏ lẻ).

>  **Lưu ý tối kỵ:** Không bao giờ đặt tiêu đề commit vô nghĩa như: `update code`, `fix bug`, `chỉnh sửa`, `done`. 

### xử lý và đặt tên Commit khi bị Conflict (Xung đột code)
**Đặt tên Commit:** 
- Cách 1 (Ưu tiên): `fix(git): resolve merge conflict with develop` hoặc `chore(git)`
- Cách 2: Sử dụng tiêu đề mặc định do Git tự sinh (Ví dụ: `Merge branch 'develop' into...`)