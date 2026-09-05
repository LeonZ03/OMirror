[CmdletBinding()]
param(
    [string]$ScrcpyDir = "",
    [string]$AdbDir = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$outputDir = Join-Path $projectRoot "dist\OMirror"
$manifest = Join-Path $projectRoot "app.manifest"
$appIcon = Join-Path $projectRoot "assets\OMirror.ico"
$outputExe = Join-Path $outputDir "OMirror.exe"
$deviceConfig = Join-Path $projectRoot "devices.local.txt"

$compilerCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw "未找到 .NET Framework C# 编译器 csc.exe。"
}

if (-not (Test-Path -LiteralPath $appIcon)) {
    throw "未找到应用图标：$appIcon"
}

if (-not $ScrcpyDir) {
    $scrcpyCandidates = @(
        (Join-Path $projectRoot "scrcpy"),
        (Join-Path (Split-Path $projectRoot -Parent) "tools\scrcpy")
    )
    $ScrcpyDir = $scrcpyCandidates |
        Where-Object { Test-Path -LiteralPath (Join-Path $_ "scrcpy.exe") } |
        Select-Object -First 1
}

if (-not $ScrcpyDir -or -not (Test-Path -LiteralPath (Join-Path $ScrcpyDir "scrcpy.exe"))) {
    throw "未找到 scrcpy。请用 -ScrcpyDir 指定官方 Windows 解压目录。"
}

if (Test-Path -LiteralPath $outputDir) {
    $resolvedOutput = [IO.Path]::GetFullPath($outputDir)
    $resolvedDist = [IO.Path]::GetFullPath((Join-Path $projectRoot "dist"))
    if (-not $resolvedOutput.StartsWith($resolvedDist + "\", [StringComparison]::OrdinalIgnoreCase)) {
        throw "输出目录安全检查失败。"
    }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDir | Out-Null

$sources = @(
    (Join-Path $projectRoot "OMirror.cs"),
    (Join-Path $projectRoot "AdbClient.cs"),
    (Join-Path $projectRoot "KeyboardCapture.cs"),
    (Join-Path $projectRoot "TransferForm.cs"),
    (Join-Path $projectRoot "Diagnostics.cs"),
    (Join-Path $projectRoot "StressModel.cs"),
    (Join-Path $projectRoot "StressRunner.cs")
)

& $compiler /nologo /target:winexe /optimize+ "/win32manifest:$manifest" "/win32icon:$appIcon" `
    /reference:System.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:System.Management.dll `
    "/out:$outputExe" $sources
if ($LASTEXITCODE -ne 0) {
    throw "OMirror 编译失败，退出码：$LASTEXITCODE"
}

Copy-Item -LiteralPath $ScrcpyDir -Destination (Join-Path $outputDir "scrcpy") -Recurse
if ($AdbDir) {
    $adbRuntimeFiles = @("adb.exe", "AdbWinApi.dll", "AdbWinUsbApi.dll")
    foreach ($fileName in $adbRuntimeFiles) {
        $sourceFile = Join-Path $AdbDir $fileName
        if (-not (Test-Path -LiteralPath $sourceFile)) {
            throw "ADB 运行时不完整，缺少：$sourceFile"
        }
        Copy-Item -LiteralPath $sourceFile -Destination (Join-Path $outputDir "scrcpy\$fileName") -Force
    }
}
if (Test-Path -LiteralPath $deviceConfig) {
    Copy-Item -LiteralPath $deviceConfig -Destination (Join-Path $outputDir "devices.local.txt")
}

$smokeTest = Start-Process -FilePath $outputExe -ArgumentList "--self-test" -Wait -PassThru
if ($smokeTest.ExitCode -ne 0) {
    throw "OMirror 烟雾测试失败，退出码：$($smokeTest.ExitCode)。请检查 scrcpy 和 devices.local.txt。"
}

Write-Host "构建完成：$outputExe"
