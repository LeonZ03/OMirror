[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $projectRoot 'dist\OMirror'
$artifacts = Join-Path $projectRoot '.artifacts\screen-shortcut'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$probe = Join-Path $artifacts 'ScreenShortcutProbe.exe'
& $compiler /nologo "/out:$probe" (Join-Path $PSScriptRoot 'ScreenShortcutProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Shortcut probe compilation failed.' }
$previousPath = $env:PATH
try {
    $env:PATH = (Join-Path $runtime 'scrcpy') + ';' + $previousPath
    & $probe (Join-Path $runtime 'OMirror.exe')
    if ($LASTEXITCODE -ne 0) { throw 'SDL shortcut regression failed.' }
} finally {
    $env:PATH = $previousPath
}
