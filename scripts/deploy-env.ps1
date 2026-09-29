# =====================================================================
# deploy-env.ps1 — đẩy config NHẠY CẢM (KHÔNG commit lên git) lên VPS rồi rebuild API.
# Chạy TỪ MÁY LOCAL (Windows PowerShell), tại thư mục gốc repo BE.
# File PHẢI lưu UTF-8 CÓ BOM: PowerShell 5.1 đọc file không BOM theo bảng mã ANSI của máy, các ký tự → ✓ —
# bị đọc thành dấu ngoặc kép cong và làm hỏng cú pháp cả file.
#
#   powershell -ExecutionPolicy Bypass -File scripts\deploy-env.ps1
#
# ĐẨY 2 FILE (cả hai đều bị .gitignore → git KHÔNG mang lên VPS):
#   - .env.vps                                -> VPS:/opt/fengdeskai/backend/.env  (secret runtime, env_file)
#   - src\FengDeskAI.WebAPI\appsettings.json  -> VPS:.../src/FengDeskAI.WebAPI/appsettings.json
#                                                (mount vào container qua volume → chỉ cần recreate, không rebuild)
#
# LƯU Ý: code/app khác deploy qua `git push main` (deploy.yml tự pull + rebuild).
#        Riêng .env và appsettings.json bị gitignore nên DÙNG script này để đồng bộ.
# =====================================================================

$ErrorActionPreference = "Stop"

$VpsUser   = if ($env:VPS_USER) { $env:VPS_USER } else { "dungvu" }
$VpsHost   = if ($env:VPS_HOST) { $env:VPS_HOST } else { "103.241.43.36" }
$RemoteDir = "/opt/fengdeskai/backend"
$AppsettingsRel = "src/FengDeskAI.WebAPI/appsettings.json"

$repoRoot   = Join-Path $PSScriptRoot ".."
$envFile    = Join-Path $repoRoot ".env.vps"
$appFile    = Join-Path $repoRoot "src\FengDeskAI.WebAPI\appsettings.json"
if (-not (Test-Path $envFile)) { throw "Khong tim thay .env.vps tai $envFile" }
if (-not (Test-Path $appFile)) { throw "Khong tim thay appsettings.json tai $appFile" }

# Sao lưu bản đang chạy trên VPS trước khi ghi đè — đẩy nhầm thì chép lại là xong.
$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
Write-Host "→ Sao lưu trên VPS: .env.bak-$Stamp, appsettings.json.bak-$Stamp"
ssh "${VpsUser}@${VpsHost}" "cd $RemoteDir && cp -p .env .env.bak-$Stamp && cp -p $AppsettingsRel $AppsettingsRel.bak-$Stamp"
if ($LASTEXITCODE -ne 0) { throw "Sao luu tren VPS that bai — dung lai, chua ghi de gi." }

Write-Host "→ Copy .env.vps          -> $VpsUser@${VpsHost}:$RemoteDir/.env"
scp $envFile "${VpsUser}@${VpsHost}:$RemoteDir/.env"
if ($LASTEXITCODE -ne 0) { throw "Lenh that bai: scp" }

Write-Host "→ Copy appsettings.json  -> $VpsUser@${VpsHost}:$RemoteDir/$AppsettingsRel"
scp $appFile "${VpsUser}@${VpsHost}:$RemoteDir/$AppsettingsRel"
if ($LASTEXITCODE -ne 0) { throw "Lenh that bai: scp" }

Write-Host "→ Recreate API tren VPS (khong can rebuild — appsettings mount volume)"
ssh "${VpsUser}@${VpsHost}" "cd $RemoteDir && docker compose up -d --force-recreate api"
if ($LASTEXITCODE -ne 0) { throw "Lenh that bai: ssh" }

Write-Host "✓ Xong — .env + appsettings da cap nhat, API restart (khong rebuild)."
Write-Host "  Kiem tra: curl https://api.fengdesk.io.vn/api/products?pageSize=1   (mong doi 200)"
