<# : batch portion
@echo off
title Git Contributor and Project Stats Tool
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-Content -Raw '%~f0' | Invoke-Expression"
if errorlevel 1 pause
exit /b
: powershell portion #>
$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Find .git in current folder, walk up to parent folders if not found
$repoDir = $null
$dir = Get-Item .
while ($dir) {
    if (Test-Path (Join-Path $dir.FullName '.git')) {
        $repoDir = $dir.FullName
        break
    }
    $dir = $dir.Parent
}
if (-not $repoDir) {
    Write-Host "[ERROR] No Git Repository found here or in any parent folder!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit
}
Set-Location $repoDir
Write-Host "[INFO] Using repo: $repoDir" -ForegroundColor DarkGray

$slnxFile = Get-ChildItem -Filter *.slnx | Select-Object -First 1
$projectName = "UNKNOWN PROJECT"
if ($slnxFile) { $projectName = $slnxFile.BaseName.ToUpper() } else { $projectName = (Get-Item .).Name.ToUpper() }

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "   GIT STATS - $projectName" -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Cyan
# ============================================================
# SECTION 1: CONTRIBUTOR STATS
# ============================================================
Write-Host ""
Write-Host ">>> CONTRIBUTOR STATS" -ForegroundColor Magenta
Write-Host "Scanning commits by author..." -ForegroundColor Yellow
$authors = git log --format='%aN' | Sort-Object -Unique
$authorReport = foreach ($name in $authors) {
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
$authorReport | Sort-Object Commits -Descending | Format-Table -AutoSize
# ============================================================
# SECTION 2: PROJECT STATS
# ============================================================
Write-Host ""
Write-Host ">>> PROJECT STATS" -ForegroundColor Magenta
Write-Host ""
Write-Host "-- Top Changed Files --" -ForegroundColor Yellow
$files = git log --pretty=format: --name-only | Where-Object { $_ -ne "" }
$files | Group-Object | Sort-Object Count -Descending | Select-Object -First 20 | ForEach-Object {
    [PSCustomObject]@{ "File" = $_.Name; "Times Changed" = $_.Count }
} | Format-Table -AutoSize
Write-Host "-- File Type Breakdown --" -ForegroundColor Yellow
$files | ForEach-Object { [System.IO.Path]::GetExtension($_) } | Where-Object { $_ -ne "" } |
    Group-Object | Sort-Object Count -Descending | ForEach-Object {
        [PSCustomObject]@{ "Extension" = $_.Name; "Changes" = $_.Count }
    } | Format-Table -AutoSize
Write-Host "-- Branches --" -ForegroundColor Yellow
git for-each-ref --sort=-committerdate refs/heads/ --format='%(refname:short)|%(committerdate:short)|%(authorname)' |
    ForEach-Object {
        $p = $_ -split '\|'
        [PSCustomObject]@{ "Branch" = $p[0]; "Last Commit" = $p[1]; "Author" = $p[2] }
    } | Format-Table -AutoSize
Write-Host "-- Merge vs Direct Commits --" -ForegroundColor Yellow
$merges = (git log --merges --oneline | Measure-Object -Line).Lines
$direct = (git log --no-merges --oneline | Measure-Object -Line).Lines
Write-Host "Merge commits : $merges"
Write-Host "Direct commits: $direct"
Write-Host ""
Write-Host "-- Commits by Weekday --" -ForegroundColor Yellow
git log --format='%ad' --date=format:'%A' |
    Group-Object | Sort-Object Count -Descending | ForEach-Object {
        [PSCustomObject]@{ "Weekday" = $_.Name; "Commits" = $_.Count }
    } | Format-Table -AutoSize
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "[SCAN COMPLETED]" -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Cyan
Read-Host "Press Enter to close this console panel"
