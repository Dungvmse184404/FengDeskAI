# Hướng dẫn vận hành VPS — FengDesk

> VPS Ubuntu chạy nginx (reverse proxy + SSL) → Docker container `fengdeskai-api` (BE .NET, port 8080) → DB Supabase.
> Domain: `api.fengdesk.io.vn`. FE deploy riêng trên Vercel.

## 1. Truy cập

```bash
ssh <user>@<vps-ip>          # ví dụ: ssh dungvu@103.241.43.36
```

Quy tắc: **không gửi mật khẩu qua chat/AI tool**. Dùng SSH key cá nhân — bỏ **public key** (`id_ed25519.pub`, bắt đầu `ssh-ed25519 AAAA...`) vào `~/.ssh/authorized_keys`, **KHÔNG bỏ private key**.

### Quyền chạy lệnh (quan trọng)

Docs này viết theo kiểu **root**. Tài khoản thường (vd `dungvu`) đã được cấu hình để chạy được mọi lệnh:

- **docker / docker compose / đọc log nginx** (`tail /var/log/nginx/*`): chạy trực tiếp, KHÔNG cần `sudo`.
- **Lệnh hệ thống** (nginx, systemctl, sửa `/etc/nginx`, ...): cần quyền root. Chọn 1 trong 2:
  - Thêm `sudo` phía trước từng lệnh: `sudo nginx -t`, `sudo systemctl reload nginx`.
  - Hoặc gõ `sudo -i` **một lần** để thành root, rồi chạy mọi lệnh docs y nguyên (không cần `sudo`).

Tài khoản `dungvu` có sudo không hỏi mật khẩu — tiện nhưng đồng nghĩa key của nó tương đương quyền root, nên **giữ private key thật kỹ**.

## 2. Kiến trúc & vị trí quan trọng

| Thành phần | Vị trí |
|---|---|
| Source BE (build context) | `/opt/fengdeskai/backend` |
| Docker compose | `/opt/fengdeskai/backend/docker-compose.yml` |
| Nginx config | `/etc/nginx/sites-available/fengdesk-api` |
| Nginx logs | `/var/log/nginx/access.log`, `error.log` |
| SSL cert (Certbot tự gia hạn) | `/etc/letsencrypt/live/api.fengdesk.io.vn/` |

Luồng request: `Internet → nginx :443 → container :8080`.
Riêng `/hubs/` (SignalR) cần WebSocket — nginx đã cấu hình `proxy_http_version 1.1` + header `Upgrade`. **Đừng xóa location này** khi sửa config.

## 3. Kiểm tra trạng thái

```bash
# Container có chạy không
docker ps

# App log — nơi xem stack trace khi API trả 500
docker logs --tail 200 fengdeskai-api
docker logs -f fengdeskai-api                    # theo dõi realtime
docker logs --tail 2000 fengdeskai-api 2>&1 | grep -A 20 -i exception   # lọc lỗi

# Nginx
systemctl status nginx
tail -50 /var/log/nginx/error.log

# Test nhanh từ ngoài
curl -s -o /dev/null -w "%{http_code}\n" https://api.fengdesk.io.vn/swagger/index.html   # mong đợi 200
curl -s -o /dev/null -w "%{http_code}\n" -X POST "https://api.fengdesk.io.vn/hubs/chat/negotiate?negotiateVersion=1"   # mong đợi 401 (KHÔNG phải 400/404)
```

## 4. Đọc nginx log

```bash
# realtime
tail -f /var/log/nginx/access.log
# chỉ request lỗi 4xx/5xx
awk '$9 ~ /^[45]/' /var/log/nginx/access.log | tail -20
# đếm theo IP
awk '{print $1}' /var/log/nginx/access.log | sort | uniq -c | sort -rn | head
# Xem ai đang quét file nhạy cảm 
grep -E "\.env|\.git|config\.|backup" /var/log/nginx/access.log | tail -20
# Đếm request theo IP — phát hiện IP quét/spam 
awk '{print $1}' /var/log/nginx/access.log | sort | uniq -c | sort -rn | head -10
# log ngày cũ (đã nén)
zcat /var/log/nginx/access.log.2.gz | less       
```

Format 1 dòng: `IP - - [thời gian] "METHOD /path" status bytes "referrer" "user-agent"`.
Bot quét `/.env`, `/.git/HEAD`... là bình thường trên internet — miễn là các file đó không tồn tại/không được serve.

## 5. Deploy bản mới (đã CI/CD trên main)

```bash
cd /opt/fengdeskai/backend
git pull
docker compose build
docker compose up -d
docker logs --tail 50 fengdeskai-api    # xác nhận khởi động sạch
```

## 6. Migration database — ĐỌC KỸ, đây là nguồn lỗi 500 kinh điển

App **KHÔNG tự migrate khi khởi động**. Deploy code mới có migration mà quên apply → mọi query đụng cột mới sẽ 500 (lỗi `42703: column ... does not exist` trong `docker logs`).

Quy trình khi PR có migration mới:

1. Deploy code (mục 5).
2. Apply migration vào Supabase — chạy chế độ seed của app (migrate + seeder, không bật web server):

```bash
cd /opt/fengdeskai/backend
docker compose run --rm migrate      # service "migrate" chạy "dotnet FengDeskAI.WebAPI.dll seed"
```

3. Xác nhận: `docker logs --tail 100 fengdeskai-api` không còn `42703`.

Sự cố đã gặp (07/2026): migration `RefactorReturnRmaFlow` nằm trong image nhưng chưa apply → lịch sử đơn hàng 500 nhiều ngày. Đã xử lý thủ công qua SQL. Đừng lặp lại.

## 7. Sửa nginx config

```bash
nano /etc/nginx/sites-available/fengdesk-api
nginx -t                      # LUÔN test trước
systemctl reload nginx        # reload không downtime
```

Sau khi sửa, chạy lại 2 lệnh curl ở mục 3 để chắc chắn API + SignalR còn sống.

## 8. Checklist chẩn đoán nhanh

| Triệu chứng | Kiểm tra |
|---|---|
| FE báo lỗi mạng / CORS | Thường là BE trả 500 → `docker logs` tìm exception (CORS chỉ là hệ quả) |
| API 500 | `docker logs` → có `42703` = thiếu migration (mục 6) |
| Chat "Mất kết nối" / AI không stream | `curl negotiate` (mục 3): 400/404 = nginx hỏng config `/hubs/`, 401 = nginx ổn, lỗi nằm chỗ khác |
| API chậm/timeout | `error.log` nginx có `upstream timed out` → tăng `proxy_read_timeout` cho route đó |
| Cả site chết | `docker ps` (container chết?) → `systemctl status nginx` → `docker compose up -d` |

## 9. Bảo mật — bắt buộc

- Không commit secret (connection string, API key) vào git — kể cả trong comment. Đã lỡ commit thì **xoay secret ngay** (xóa file không đủ, git history giữ vĩnh viễn).
- Không gửi mật khẩu qua chat, kể cả chat với AI.
- Không để secret trong biến `VITE_*` của FE — chúng bị nhúng công khai vào bundle JS.
- SSH bằng key, tắt password auth khi đã đủ key: `/etc/ssh/sshd_config` → `PasswordAuthentication no` → `systemctl restart sshd`.
- Đổi mật khẩu: `passwd` (VPS), Supabase dashboard → Settings → Database (DB).

## 10. Hostname dự phòng cho API

Ngày 29/09/2026 nameserver `ns1/ns2.matbao.vn` ngừng phản hồi → toàn bộ `fengdesk.io.vn` không
resolve được. API và VPS vẫn chạy bình thường, nhưng **không ai gọi tới được** — và không có cách
nào chữa từ phía server, vì lỗi nằm ở tầng DNS trước khi traffic chạm tới nginx.

Đường dự phòng đã dựng, dùng nhà cung cấp DNS khác nhưng trỏ về **chính server này**:

| | Hostname | DNS |
|---|---|---|
| Chính | `api.fengdesk.io.vn` | Mắt Bão (hoặc Cloudflare sau khi chuyển NS) |
| Dự phòng | `103-241-43-36.sslip.io` | sslip.io — tự suy IP từ tên, không cần đăng ký |

| Thành phần | Vị trí |
|---|---|
| Nginx config dự phòng | `/etc/nginx/sites-available/fengdesk-api-sslip` |
| SSL cert | `/etc/letsencrypt/live/103-241-43-36.sslip.io/` |

Cả hai proxy về cùng upstream `fengdesk_api`, cùng cấu hình `/hubs/` (WebSocket) và `/api/chat/`
(timeout 300s). Cert riêng, certbot tự gia hạn.

```bash
# Kiểm tra đường dự phòng
curl -s -o /dev/null -w "%{http_code}\n" https://103-241-43-36.sslip.io/health   # mong đợi 200
```

FE tự chọn đường lúc khởi động: dò `/health` của hostname chính, không tới được thì chuyển sang dự
phòng và ghi nhớ trong `sessionStorage` cho cả phiên. Cấu hình bằng 2 biến env trên Vercel:
`VITE_API_BASE_URL` (chính) và `VITE_API_BASE_URL_FALLBACK` (dự phòng). Bỏ trống biến thứ hai là
tắt tính năng. Chi tiết: `FengDeskAI_FE/src/config/apiBase.ts`.

**Lưu ý:** hai hostname vào cùng một VPS nên đường dự phòng **không** cứu được trường hợp VPS hoặc
container chết — nó chỉ chống sự cố DNS. Ngoài ra một số mạng doanh nghiệp/trường học chặn
`sslip.io` và `nip.io`, nên hãy thử trước trên đúng mạng sẽ demo.

### Gỡ khi domain chính đã ổn định

```bash
rm /etc/nginx/sites-enabled/fengdesk-api-sslip
certbot delete --cert-name 103-241-43-36.sslip.io
nginx -t && systemctl reload nginx
```
Rồi bỏ `VITE_API_BASE_URL_FALLBACK` trên Vercel và redeploy.
