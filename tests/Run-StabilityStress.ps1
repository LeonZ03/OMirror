[CmdletBinding()]
param(
    [ValidateRange(0, 1)]
    [int]$DeviceIndex = 0,
    [ValidateRange(1, 60)]
    [int]$DurationMinutes = 10,
    [int]$Seed = 20260905,
    [switch]$InjectAdbFaults,
    [ValidateRange(1, 50)]
    [int]$FaultCycles = 10,
    [string]$Replay = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $projectRoot "dist\OPhoneMirror\OPhoneMirror.exe"
$artifactsRoot = Join-Path $projectRoot ".artifacts\stability"

if ($Replay) {
    $saved = Get-Content -LiteralPath $Replay -Raw | ConvertFrom-Json
    $DeviceIndex = [int]$saved.DeviceIndex
    $DurationMinutes = [int]$saved.DurationMinutes
    $Seed = [int]$saved.Seed
    $InjectAdbFaults = [bool]$saved.InjectAdbFaults
    $FaultCycles = [int]$saved.FaultCycles
}
if (-not (Test-Path -LiteralPath $exe)) {
    throw "未找到已构建程序：$exe。请先运行 build.ps1。"
}

$session = Get-Date -Format "yyyyMMdd-HHmmss"
$outDir = Join-Path $artifactsRoot $session
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$replayFile = Join-Path $outDir "replay.json"
[pscustomobject]@{
    DeviceIndex = $DeviceIndex
    DurationMinutes = $DurationMinutes
    Seed = $Seed
    InjectAdbFaults = [bool]$InjectAdbFaults
    FaultCycles = $FaultCycles
} | ConvertTo-Json | Set-Content -LiteralPath $replayFile -Encoding UTF8

# The application first runs its 100k deterministic state-machine invariants.
$model = Start-Process -FilePath $exe -ArgumentList @("--stress-model", "$Seed") -Wait -PassThru
if ($model.ExitCode -ne 0) { throw "状态机随机自检失败，退出码：$($model.ExitCode)" }

if ($InjectAdbFaults) {
    # Fault injection restarts the global ADB service. Refuse if another phone is online.
    $adb = Join-Path (Split-Path $exe -Parent) "scrcpy\adb.exe"
    $online = (& $adb devices | Select-String "`tdevice$" | Measure-Object).Count
    if ($online -ne 1) {
        throw "ADB 故障注入已中止：当前在线设备数为 $online，不是唯一的目标手机。"
    }
}

$arguments = @("--stress-live", "$DeviceIndex", "$DurationMinutes", "$Seed", "--diagnostics=debug")
$live = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru
if ($InjectAdbFaults) {
    $adb = Join-Path (Split-Path $exe -Parent) "scrcpy\adb.exe"
    for ($i = 0; $i -lt $FaultCycles -and -not $live.HasExited; $i++) {
        Start-Sleep -Seconds 8
        & $adb kill-server | Out-Null
        Start-Sleep -Milliseconds 750
        & $adb start-server | Out-Null
        Add-Content -LiteralPath (Join-Path $outDir "faults.log") -Value "$(Get-Date -Format o) adb-restart $($i + 1)"
    }
}
$live.WaitForExit()

$diagnostics = Join-Path $env:LOCALAPPDATA "OPhoneMirror\Diagnostics"
if (Test-Path -LiteralPath $diagnostics) {
    Get-ChildItem -LiteralPath $diagnostics -Filter "*.log" | Sort-Object LastWriteTime -Descending |
        Select-Object -First 5 | Copy-Item -Destination $outDir -Force
}
"ExitCode=$($live.ExitCode) Seed=$Seed DeviceIndex=$DeviceIndex" |
    Set-Content -LiteralPath (Join-Path $outDir "summary.txt") -Encoding UTF8

if ($live.ExitCode -ne 0) {
    throw "压力测试失败。重放文件：$replayFile"
}
Write-Host "压力测试通过。重放文件：$replayFile"
