<# : batch portion
@echo off
chcp 65001 > nul
title Git Contributor Stats Tool
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-Content '%~f0' | Out-String | Invoke-Expression"
exit /b
: powershell portion #>

$OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Automatically detect .slnx file in the current directory to extract Project Name
$slnxFile = Get-ChildItem -Filter *.slnx | Select-Object -First 1
$projectName = "UNKNOWN PROJECT"

if ($slnxFile) {
    $projectName = $slnxFile.BaseName.ToUpper()
} else {
    $projectName = (Get-Item .).Name.ToUpper()
}

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host "   GIT CONTRIBUTOR STATS - $projectName            " -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path .git)) {
    Write-Host "[ERROR] This directory is not a Git Repository!" -ForegroundColor Red
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit
}

Write-Host "Scanning Git history logs, please wait..." -ForegroundColor Yellow

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
Write-Host "[SCAN COMPLETED SUCCESSFULLY]:" -ForegroundColor Green
Write-Host ""

$report | Format-Table -AutoSize

Write-Host ""
Write-Host "=========================================================" -ForegroundColor Cyan
Read-Host "Press Enter to close this console panel"