[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $projectRoot '.artifacts\screen-policy'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$probe = Join-Path $artifacts 'ScreenPowerPolicyTests.exe'
& $compiler /nologo "/out:$probe" (Join-Path $projectRoot 'ScreenPowerPolicy.cs') (Join-Path $PSScriptRoot 'ScreenPowerPolicyTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Screen policy tests failed to compile.' }
& $probe
if ($LASTEXITCODE -ne 0) { throw 'Screen policy regression failed.' }
