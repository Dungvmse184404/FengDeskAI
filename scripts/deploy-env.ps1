# =====================================================================
# deploy-env.ps1 — đẩy config NHẠY CẢM (KHÔNG commit lên git) lên VPS rồi rebuild API.
# Chạy TỪ MÁY LOCAL (Windows PowerShell), tại thư mục gốc repo BE.
#
#   powershell -ExecutionPolicy Bypass -File scripts\deploy-env.ps1
#
# ĐẨY 2 FILE (cả hai đều bị .gitignore → git KHÔNG mang lên VPS):
#   - .env.vps                                -> VPS:/opt/fengdeskai/backend/.env  (secret runtime, env_file)
#   - src\FengDeskAI.WebAPI\appsettings.json  -> VPS:.../src/FengDeskAI.WebAPI/appsettings.json
#                                                (appsettings COPY vào image lúc build → phải rebuild)
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

Write-Host "→ Copy .env.vps          -> $VpsUser@${VpsHost}:$RemoteDir/.env"
scp $envFile "${VpsUser}@${VpsHost}:$RemoteDir/.env"

Write-Host "→ Copy appsettings.json  -> $VpsUser@${VpsHost}:$RemoteDir/$AppsettingsRel"
scp $appFile "${VpsUser}@${VpsHost}:$RemoteDir/$AppsettingsRel"

Write-Host "→ Rebuild + restart API tren VPS (appsettings moi can build lai image)"
ssh "${VpsUser}@${VpsHost}" "cd $RemoteDir && docker compose up -d --build"

Write-Host "✓ Xong — .env + appsettings da cap nhat, image rebuild + API restart."
Write-Host "  Kiem tra: curl https://api.fengdesk.io.vn/api/workspace/speech-config"
