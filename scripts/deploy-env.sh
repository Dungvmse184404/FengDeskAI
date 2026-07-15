#!/usr/bin/env bash
# =====================================================================
# deploy-env.sh — đẩy .env.vps (secret, KHÔNG commit) lên VPS thành /opt/fengdeskai/backend/.env
# rồi restart API. Chạy TỪ MÁY LOCAL (git-bash / WSL / Linux / macOS), tại thư mục gốc repo BE.
#
#   bash scripts/deploy-env.sh
#
# Phân công:
#   - appsettings.json  -> trong git, `git push main` tự deploy (deploy.yml rebuild). KHÔNG dùng script này.
#   - .env (secret)     -> KHÔNG trong git -> script này scp .env.vps lên VPS.
# =====================================================================
set -euo pipefail

VPS_USER="${VPS_USER:-dungvu}"
VPS_HOST="${VPS_HOST:-103.241.43.36}"
REMOTE_DIR="/opt/fengdeskai/backend"

cd "$(dirname "$0")/.."
[ -f .env.vps ] || { echo "Không tìm thấy .env.vps"; exit 1; }

echo "→ Copy .env.vps -> $VPS_USER@$VPS_HOST:$REMOTE_DIR/.env"
scp .env.vps "$VPS_USER@$VPS_HOST:$REMOTE_DIR/.env"

echo "→ Restart API trên VPS"
ssh "$VPS_USER@$VPS_HOST" "cd $REMOTE_DIR && docker compose up -d"

echo "✓ Xong — .env đã cập nhật + API restart. Kiểm tra: curl https://api.fengdesk.io.vn/api/workspace/speech-config"
