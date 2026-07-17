@echo off
REM =====================================================================
REM deploy-env.bat - 1-CLICK: day config NHAY CAM (khong commit git) len VPS + rebuild API.
REM Double-click de chay (hoac: scripts\deploy-env.bat). Can SSH key da cau hinh (khong hoi pass).
REM
REM DAY 2 FILE (ca hai deu bi .gitignore -> git KHONG mang len VPS):
REM   - .env.vps                                -> VPS:/opt/fengdeskai/backend/.env (secret runtime)
REM   - src\FengDeskAI.WebAPI\appsettings.json  -> VPS:.../src/FengDeskAI.WebAPI/appsettings.json
REM                                                (appsettings COPY vao image luc build -> phai rebuild)
REM
REM LUU Y: code/app khac deploy qua `git push main` (deploy.yml tu pull + rebuild).
REM        Rieng .env va appsettings.json bi gitignore nen DUNG file nay de dong bo.
REM =====================================================================
setlocal
if "%VPS_USER%"=="" set VPS_USER=dungvu
if "%VPS_HOST%"=="" set VPS_HOST=103.241.43.36
set REMOTE_DIR=/opt/fengdeskai/backend
set APPSETTINGS_REL=src/FengDeskAI.WebAPI/appsettings.json

REM ve thu muc goc repo (file .bat nam trong scripts\)
cd /d "%~dp0.."

if not exist ".env.vps" (
  echo [LOI] Khong tim thay .env.vps tai %CD%
  pause
  exit /b 1
)
if not exist "src\FengDeskAI.WebAPI\appsettings.json" (
  echo [LOI] Khong tim thay src\FengDeskAI.WebAPI\appsettings.json tai %CD%
  pause
  exit /b 1
)

echo.
echo === Copy .env.vps -^> %VPS_USER%@%VPS_HOST%:%REMOTE_DIR%/.env ===
scp .env.vps %VPS_USER%@%VPS_HOST%:%REMOTE_DIR%/.env
if errorlevel 1 goto fail

echo.
echo === Copy appsettings.json -^> %VPS_USER%@%VPS_HOST%:%REMOTE_DIR%/%APPSETTINGS_REL% ===
scp "src\FengDeskAI.WebAPI\appsettings.json" %VPS_USER%@%VPS_HOST%:%REMOTE_DIR%/%APPSETTINGS_REL%
if errorlevel 1 goto fail

echo.
echo === Rebuild + restart API tren VPS ===
ssh %VPS_USER%@%VPS_HOST% "cd %REMOTE_DIR% && docker compose up -d --build"
if errorlevel 1 goto fail

echo.
echo [OK] Da cap nhat .env + appsettings, rebuild + restart API.
echo Kiem tra: curl https://api.fengdesk.io.vn/api/workspace/speech-config
pause
exit /b 0

:fail
echo.
echo [LOI] Deploy that bai. Kiem tra: SSH key da cau hinh chua? VPS_HOST/VPS_USER dung chua?
pause
exit /b 1
