# TestData — bộ dữ liệu test

Dữ liệu đầu vào của bộ test **nằm ở đây, không nằm trong code**. Thêm một bộ mới = thêm một file
JSON, không phải sửa file `.cs` nào.

## datasets/ — dữ liệu quét toàn bộ endpoint

Mỗi file là một bộ. Bộ nào cũng cung cấp hai thứ mà trước đây bị hardcode: **giá trị thay tham số
route** và **body request**.

```jsonc
{
  "name": "normal",
  "kind": "Normal",                                  // Normal | Boundary | Abnormal
  "description": "...",                              // in ra trong output test
  "appliesTo": [ "authorization", "smoke", "robustness" ],

  "routeValues": {
    "*": "test",                                     // mặc định
    "guid": "00000000-0000-0000-0000-000000000001",  // theo RÀNG BUỘC: {storeId:guid}
    "id": "00000000-0000-0000-0000-000000000001",    // theo TÊN tham số
    "code": "TEST"
  },

  "bodies": {
    "*": {},                                          // mặc định
    "POST /api/Auth/login": { "email": "a@b.c" }      // riêng cho 1 endpoint
  },

  "rawBodies": {
    "*": "{ \"unclosed\": "                           // chuỗi thô — mô tả được JSON hỏng cú pháp
  }
}
```

### Thứ tự tra `routeValues`

1. đúng tên tham số — `storeId`
2. ràng buộc kiểu trong template — `{storeId:guid}` tra khóa `guid`
3. tên có chứa `id` nhưng không khai ràng buộc → khóa `id`
4. `*`

**Bước 2 không được bỏ.** `{storeId:guid}` mà nhận chuỗi thường thì khâu chọn action loại endpoint
và trả 404 **trước** middleware phân quyền — cả ma trận 401/403 sẽ sai hàng loạt mà nhìn như lỗi
phân quyền.

### `appliesTo` — bộ nào chạy với suite nào

| Suite | Khẳng định | Vì sao không nhận mọi bộ |
|---|---|---|
| `authorization` | không token → 401 · sai role → 403 | Dữ liệu phải **thỏa ràng buộc route**, nếu không sẽ 404 trước khi tới phân quyền |
| `smoke` | GET không tham số không được 5xx | — |
| `robustness` | không endpoint nào được 5xx | Nhận mọi loại dữ liệu, kể cả rác |

Bộ `abnormal` cố ý chỉ khai `robustness`.

## Ba bộ hiện có

| Bộ | Loại | Dùng để |
|---|---|---|
| `normal` | Normal | Tái lập đúng hành vi trước khi tách dữ liệu ra file |
| `boundary` | Boundary | GUID toàn số 0, chuỗi 1 ký tự, field rỗng, số 0 |
| `abnormal` | Abnormal | GUID sai định dạng, JSON hỏng cú pháp, số âm, chuỗi chứa cú pháp SQL |

Phân loại `Normal / Boundary / Abnormal` khớp đúng cột **N / B / A** mà `Report5_Unit Test.xls` yêu
cầu, nên khi làm tài liệu test có thể lấy thẳng.

## cases/ — ca test nghiệp vụ (tầng 2)

Mỗi file mô tả các ca test của một luồng. **Mỗi dòng thành một ca xunit riêng**, nên số test chạy
trong CI khớp đúng số dòng khi lập `Report5_Test Report.xlsx`.

```jsonc
{
  "feature": "Authentication - Login",
  "requirement": "UC-01 Register / Login",
  "cases": [
    {
      "id": "AUTH-LOGIN-01",
      "kind": "Normal",                       // N / B / A
      "description": "...",                   // → cột Test Case Description
      "preCondition": "...",                  // → cột Pre-conditions
      "procedure": "...",                     // → cột Test Case Procedure
      "expectedResult": "...",                // → cột Expected Results
      "method": "POST",
      "path": "/api/Auth/login",
      "role": "Anonymous",                    // Anonymous | Customer | Staff | Manager | Admin | GardenOwner
      "body": { "email": "{{email.Customer}}", "password": "{{password}}" },
      "expectedStatus": 200,
      "expectedMessageContains": "Đăng nhập thành công"
    }
  ]
}
```

Tên trường đặt đúng theo cột của `Report5_Test Report.xlsx` để xuất thẳng ra Excel, khỏi gõ lại.

### Chỗ giữ chỗ

| Token | Thay bằng |
|---|---|
| `{{password}}` | Mật khẩu của các user mẫu trong phiên chạy |
| `{{email.Customer}}`, `{{email.Admin}}`… | Email user mẫu theo role |
| `{{newEmail}}` | Email chưa từng đăng ký, **sinh mới mỗi lần gọi** |

Cần chỗ giữ chỗ vì mật khẩu sinh ngẫu nhiên mỗi lần chạy — không có secret nào nằm trong file.

### Ca một-request vs ca nhiều bước

File JSON chỉ mô tả được ca **một request**. Ca nhiều bước nối trạng thái (đăng ký 3 bước, quên
mật khẩu trọn vẹn, đổi email 4 bước) nằm trong `Endpoints/AuthFlowTests.cs` vì phải bắt OTP từ
`FakeEmailSender` rồi chuyền token giữa các bước. Chúng vẫn mang mã `AUTH-REG-01`, `AUTH-FP-09`…
đặt trong `DisplayName` nên vẫn gom vào bảng test case được.

## Thêm bộ mới

Thả thêm file `.json` vào `datasets/`. Nó tự xuất hiện thành ca test mới trong mọi suite được khai
ở `appliesTo` — số ca test tăng lên, không phải sửa code.

> File được copy sang thư mục output qua mục `Content` trong `FengDeskAI.ApiTests.csproj`. Thêm thư
> mục con mới vẫn chạy được vì pattern là `TestData/**/*.json`.
