<# : batch portion
@echo off
chcp 65001 > nul
title Git & GitHub Stats Tool
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-Content '%~f0' | Out-String | Invoke-Expression"
exit /b
: powershell portion #>

$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Tu dong xoa cac file rac do CMD tao nham tu cac lan chay truoc
if (Test-Path "`$null") { Remove-Item "`$null" -Force -ErrorAction SilentlyContinue }
if (Test-Path "`$null)") { Remove-Item "`$null)" -Force -ErrorAction SilentlyContinue }

$projectName = (Get-Item .).Name.ToUpper()

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "   GIT & GITHUB STATS - $projectName             " -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path .git)) {
    Write-Host "[ERROR] Thu muc nay khong phai la Git Repository!" -ForegroundColor Red
    Write-Host "Vui long dat file nay o thu muc goc (ngang hang voi thu muc .git)" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Nhan Enter de thoat"
    exit
}

# --- PHAN 1: LOCAL GIT DATA ---
Write-Host "[1/2] Dang quet du lieu Local Git, vui long doi..." -ForegroundColor Yellow

$authors = git log --format='%aN' | Sort-Object -Unique

$report = foreach ($name in $authors) {
    if (-not $name) { continue }
    $escapedName = [regex]::Escape($name)
    
    $commits = (git rev-list --count --author=$escapedName HEAD 2>$null)
    if (-not $commits) { $commits = 0 }

    $add = 0; $sub = 0
    git log --author=$escapedName --pretty=tformat: --numstat 2>$null | ForEach-Object {
        if ($_ -match '^(\d+)\s+(\d+)') {
            $add += [int]$Matches[1]
            $sub += [int]$Matches[2]
        }
    }
    
    [PSCustomObject]@{
        "Contributor"   = $name
        "Commits"       = $commits
        "Lines Added"   = "+$add"
        "Lines Removed" = "-$sub"
    }
}

Write-Host ""
Write-Host "[QUET LOCAL GIT THANH CONG]:" -ForegroundColor Green
Write-Host ""
$report | Format-Table -AutoSize

# --- PHAN 2: GITHUB DATA (Forks & Stars) ---
Write-Host ""
Write-Host "[2/2] Dang ket noi lay du lieu GitHub (Forks, Stars)..." -ForegroundColor Yellow
try {
    $ghCheck = Get-Command gh -ErrorAction SilentlyContinue
    if ($ghCheck) {
        $repoStats = gh repo view --json forkCount,stargazerCount | ConvertFrom-Json
        Write-Host ""
        Write-Host "[THONG KE TU GITHUB]:" -ForegroundColor Green
        Write-Host " - So luot Forks : $($repoStats.forkCount)" -ForegroundColor Cyan
        Write-Host " - So luot Stars : $($repoStats.stargazerCount)" -ForegroundColor Cyan
    } else {
        Write-Host "[CANH BAO] Khong tim thay GitHub CLI (gh). Bo qua buoc nay." -ForegroundColor DarkYellow
    }
} catch {
    Write-Host "[LOI GITHUB] Khong the lay du lieu truc tuyen. Co the repo chua duoc push, hoac chua dang nhap 'gh auth login'." -ForegroundColor Red
}

Write-Host ""
Write-Host "=========================================================" -ForegroundColor Cyan
Read-Host "Nhan Enter de dong cua so nay"