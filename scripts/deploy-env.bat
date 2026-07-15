@echo off
REM =====================================================================
REM deploy-env.bat — 1-CLICK: day .env.vps len VPS thanh /opt/fengdeskai/backend/.env + restart API.
REM Double-click de chay (hoac: scripts\deploy-env.bat). Can SSH key da cau hinh (khong hoi pass).
REM
REM Phan cong:
REM   - appsettings.json / code  -> git push main (deploy.yml tu rebuild). KHONG dung file nay.
REM   - .env (secret)            -> file nay scp .env.vps len VPS.
REM =====================================================================
setlocal
if "%VPS_USER%"=="" set VPS_USER=dungvu
if "%VPS_HOST%"=="" set VPS_HOST=103.241.43.36
set REMOTE_DIR=/opt/fengdeskai/backend

REM ve thu muc goc repo (file .bat nam trong scripts\)
cd /d "%~dp0.."

if not exist ".env.vps" (
  echo [LOI] Khong tim thay .env.vps tai %CD%
  pause
  exit /b 1
)

echo.
echo === Copy .env.vps -^> %VPS_USER%@%VPS_HOST%:%REMOTE_DIR%/.env ===
scp .env.vps %VPS_USER%@%VPS_HOST%:%REMOTE_DIR%/.env
if errorlevel 1 goto fail

echo.
echo === Restart API tren VPS ===
ssh %VPS_USER%@%VPS_HOST% "cd %REMOTE_DIR% && docker compose up -d"
if errorlevel 1 goto fail

echo.
echo [OK] Da cap nhat .env + restart API.
echo Kiem tra: curl https://api.fengdesk.io.vn/api/workspace/speech-config
pause
exit /b 0

:fail
echo.
echo [LOI] Deploy that bai. Kiem tra: SSH key da cau hinh chua? VPS_HOST/VPS_USER dung chua?
pause
exit /b 1
