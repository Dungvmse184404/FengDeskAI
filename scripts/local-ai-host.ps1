# =====================================================================
# local-ai-host.ps1 — giữ máy local luôn sẵn sàng làm AI host cho VPS (Ollama + ngrok tunnel).
# File PHẢI lưu UTF-8 CÓ BOM (PowerShell 5.1 đọc file không BOM theo ANSI → vỡ tiếng Việt).
#
#   powershell -ExecutionPolicy Bypass -File scripts\local-ai-host.ps1             # chạy tay (Ctrl+C để dừng)
#   powershell -ExecutionPolicy Bypass -File scripts\local-ai-host.ps1 -Install    # tự chạy ẩn mỗi lần đăng nhập
#   powershell -ExecutionPolicy Bypass -File scripts\local-ai-host.ps1 -Uninstall
#
# Làm gì:
#   - Chặn Windows sleep khi script còn chạy (SetThreadExecutionState — tự hết hiệu lực khi script thoát).
#   - Ollama chết → bật lại. ngrok chết (đóng terminal, crash) → bật lại với static domain.
#   - VRAM trống (Ollama vừa khởi động / bị stop) → nạp sẵn model chính với keep_alive=-1.
#
# KHÔNG ping định kỳ: tunnel ngrok không đóng vì idle (agent tự heartbeat), còn model bị gỡ là do
# keep_alive → đặt -1 là xong. Lệnh nạp model không sinh token nên không tranh GPU với request thật.
# Nếu VRAM đang có model khác (vd qwen3-vl vừa xử lý ảnh) → để yên, không giành lại VRAM.
# =====================================================================
param(
    [string]$Model = 'qwen3.5:latest',
    # PHẢI khớp Ai__Relay__Providers__0__NumCtx ở .env.vps — lệch num_ctx là Ollama reload model, preload thành vô ích.
    [int]$NumCtx = 16384,
    [string]$NgrokUrl = 'https://insuppressible-damien-historiographical.ngrok-free.dev',
    [int]$OllamaPort = 11434,
    [int]$CheckIntervalSeconds = 20,
    [switch]$Install,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
$TaskName = 'FengDeskAI Local AI Host'
$OllamaBase = "http://127.0.0.1:$OllamaPort"
$LogDir = Join-Path $env:LOCALAPPDATA 'FengDeskAI'
$LogFile = Join-Path $LogDir 'local-ai-host.log'
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Write-Log([string]$Message) {
    $line = '[{0:yyyy-MM-dd HH:mm:ss}] {1}' -f (Get-Date), $Message
    Write-Host $line
    Add-Content -Path $LogFile -Value $line -Encoding UTF8
}

if ($Install) {
    $argument = '-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "{0}" -Model "{1}" -NumCtx {2} -NgrokUrl "{3}" -OllamaPort {4}' -f `
        $PSCommandPath, $Model, $NumCtx, $NgrokUrl, $OllamaPort
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $argument
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
    # ExecutionTimeLimit=0 → không bị Task Scheduler kill sau 72h mặc định; script crash → tự chạy lại sau 1 phút.
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) `
        -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) `
        -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null
    Start-ScheduledTask -TaskName $TaskName
    Write-Host "Đã đăng ký + chạy task '$TaskName'. Log: $LogFile"
    exit 0
}

if ($Uninstall) {
    Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "Đã gỡ task '$TaskName' (Ollama/ngrok đang chạy vẫn giữ nguyên)."
    exit 0
}

# Chỉ 1 instance: chạy tay trong lúc task nền đang chạy → 2 watchdog cùng bật ngrok sẽ đụng static domain.
$mutex = New-Object System.Threading.Mutex($false, 'Local\FengDeskAI-LocalAiHost')
try { $acquired = $mutex.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $acquired = $true }
if (-not $acquired) { Write-Host 'Đã có 1 instance local-ai-host đang chạy — thoát.'; exit 0 }

Add-Type -Namespace Win32 -Name Power -MemberDefinition @'
[DllImport("kernel32.dll")]
public static extern uint SetThreadExecutionState(uint esFlags);
'@
# ES_CONTINUOUS (0x80000000) | ES_SYSTEM_REQUIRED (0x1): chặn sleep, vẫn cho tắt màn hình.
[void][Win32.Power]::SetThreadExecutionState([uint32]2147483649)

function Test-OllamaAlive {
    try { Invoke-RestMethod "$OllamaBase/api/version" -TimeoutSec 5 | Out-Null; return $true } catch { return $false }
}

function Start-OllamaIfDown {
    if (Test-OllamaAlive) { return $true }
    Write-Log 'Ollama không phản hồi — khởi động "ollama serve".'
    Start-Process -FilePath 'ollama' -ArgumentList 'serve' -WindowStyle Hidden
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 1
        if (Test-OllamaAlive) { Write-Log 'Ollama đã lên.'; return $true }
    }
    Write-Log 'Ollama vẫn chưa lên sau 30s — thử lại ở vòng sau.'
    return $false
}

function Start-NgrokIfDown {
    # Chỉ xét process: ngrok còn sống thì tự reconnect khi rớt mạng, không cần can thiệp.
    if (Get-Process -Name 'ngrok' -ErrorAction SilentlyContinue) { return }
    Write-Log "ngrok không chạy — khởi động tunnel $NgrokUrl -> :$OllamaPort."
    Start-Process -FilePath 'ngrok' -WindowStyle Hidden -ArgumentList @(
        'http', "$OllamaPort",
        "--host-header=localhost:$OllamaPort",
        "--url=$NgrokUrl",
        '--log-level=warn', "--log=$(Join-Path $LogDir 'ngrok.log')"
    )
}

function Initialize-ModelIfVramEmpty {
    $loaded = @((Invoke-RestMethod "$OllamaBase/api/ps" -TimeoutSec 5).models)
    if ($loaded.Count -gt 0) { return }
    Write-Log "VRAM trống — nạp sẵn $Model (num_ctx=$NumCtx, keep_alive=-1)."
    # Không có prompt → Ollama chỉ load model rồi trả về, không sinh token.
    $body = @{ model = $Model; keep_alive = -1; options = @{ num_ctx = $NumCtx } } | ConvertTo-Json
    Invoke-RestMethod -Method Post -Uri "$OllamaBase/api/generate" -Body $body -ContentType 'application/json' -TimeoutSec 300 | Out-Null
    Write-Log "Đã nạp $Model."
}

Write-Log "local-ai-host bắt đầu (model=$Model, port=$OllamaPort, chu kỳ ${CheckIntervalSeconds}s)."
try {
    while ($true) {
        try {
            if (Start-OllamaIfDown) { Initialize-ModelIfVramEmpty }
            Start-NgrokIfDown
        } catch {
            Write-Log "Lỗi vòng kiểm tra: $($_.Exception.Message)"
        }
        Start-Sleep -Seconds $CheckIntervalSeconds
    }
} finally {
    [void][Win32.Power]::SetThreadExecutionState([uint32]2147483648)
    $mutex.ReleaseMutex()
    Write-Log 'local-ai-host dừng.'
}
