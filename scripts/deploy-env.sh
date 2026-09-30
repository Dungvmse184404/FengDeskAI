#!/usr/bin/env bash
# =====================================================================
# deploy-env.sh — đẩy config NHẠY CẢM (KHÔNG commit lên git) lên VPS rồi rebuild API.
# Chạy TỪ MÁY LOCAL (git-bash / WSL / Linux / macOS), tại thư mục gốc repo BE.
#
#   bash scripts/deploy-env.sh
#
# ĐẨY 2 FILE (cả hai đều bị .gitignore → git KHÔNG mang lên VPS):
#   - .env.vps                          -> VPS:/opt/fengdeskai/backend/.env   (secret runtime, env_file)
#   - src/FengDeskAI.WebAPI/appsettings.json -> VPS:.../src/FengDeskAI.WebAPI/appsettings.json
#                                          (mount vào container qua volume → chỉ cần recreate, không rebuild)
#
# LƯU Ý: code/app khác deploy qua `git push main` (deploy.yml tự pull + rebuild).
#        Riêng .env và appsettings.json bị gitignore nên DÙNG script này để đồng bộ.
# =====================================================================
set -euo pipefail

VPS_USER="${VPS_USER:-dungvu}"
VPS_HOST="${VPS_HOST:-103.241.43.36}"
REMOTE_DIR="/opt/fengdeskai/backend"
APPSETTINGS_REL="src/FengDeskAI.WebAPI/appsettings.json"

cd "$(dirname "$0")/.."
[ -f .env.vps ] || { echo "Không tìm thấy .env.vps"; exit 1; }
[ -f "$APPSETTINGS_REL" ] || { echo "Không tìm thấy $APPSETTINGS_REL"; exit 1; }

# Sao lưu bản đang chạy trên VPS trước khi ghi đè — đẩy nhầm thì chép lại là xong.
STAMP="$(date +%Y%m%d-%H%M%S)"
echo "→ Sao lưu trên VPS: .env.bak-$STAMP, appsettings.json.bak-$STAMP"
ssh "$VPS_USER@$VPS_HOST" "cd $REMOTE_DIR && cp -p .env .env.bak-$STAMP && cp -p $APPSETTINGS_REL $APPSETTINGS_REL.bak-$STAMP"

echo "→ Copy .env.vps          -> $VPS_USER@$VPS_HOST:$REMOTE_DIR/.env"
scp .env.vps "$VPS_USER@$VPS_HOST:$REMOTE_DIR/.env"

echo "→ Copy appsettings.json  -> $VPS_USER@$VPS_HOST:$REMOTE_DIR/$APPSETTINGS_REL"
scp "$APPSETTINGS_REL" "$VPS_USER@$VPS_HOST:$REMOTE_DIR/$APPSETTINGS_REL"

echo "→ Recreate API trên VPS (appsettings mount volume + .env đọc lúc tạo container → KHÔNG cần rebuild)"
ssh "$VPS_USER@$VPS_HOST" "cd $REMOTE_DIR && docker compose up -d --force-recreate api"

echo "✓ Xong — .env + appsettings đã cập nhật, API restart (không rebuild)."
echo "  Kiểm tra: curl https://api.fengdesk.io.vn/api/products?pageSize=1   (mong đợi 200)"
