# =====================================================================
# deploy-env.ps1 — đẩy .env.vps (secret, KHÔNG commit) lên VPS thành /opt/fengdeskai/backend/.env
# rồi restart API. Chạy TỪ MÁY LOCAL (Windows PowerShell), tại thư mục gốc repo BE.
#
#   powershell -ExecutionPolicy Bypass -File scripts\deploy-env.ps1
#
# LƯU Ý phân công:
#   - appsettings.json  -> nằm trong git, `git push main` tự deploy (deploy.yml rebuild). KHÔNG dùng script này.
#   - .env (secret)     -> KHÔNG trong git -> script này scp .env.vps lên VPS.
# =====================================================================

$ErrorActionPreference = "Stop"

$VpsUser   = if ($env:VPS_USER) { $env:VPS_USER } else { "dungvu" }
$VpsHost   = if ($env:VPS_HOST) { $env:VPS_HOST } else { "103.241.43.36" }
$RemoteDir = "/opt/fengdeskai/backend"

$envFile = Join-Path $PSScriptRoot "..\.env.vps"
if (-not (Test-Path $envFile)) { throw "Khong tim thay .env.vps tai $envFile" }

Write-Host "→ Copy .env.vps -> $VpsUser@${VpsHost}:$RemoteDir/.env"
scp $envFile "${VpsUser}@${VpsHost}:$RemoteDir/.env"

Write-Host "→ Restart API tren VPS"
ssh "${VpsUser}@${VpsHost}" "cd $RemoteDir && docker compose up -d"

Write-Host "✓ Xong — .env da cap nhat + API restart. Kiem tra: curl https://api.fengdesk.io.vn/api/workspace/speech-config"
