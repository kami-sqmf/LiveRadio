[CmdletBinding()]
param([string]$MsysRoot = 'C:/msys64')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $projectRoot
$env:DOTNET_CLI_HOME = Join-Path $workspaceRoot '.tools/dotnet-home'
& (Join-Path $MsysRoot 'ucrt64/bin/ctest.exe') --test-dir (Join-Path $workspaceRoot '.work/airplay-native') --output-on-failure --timeout 25
if ($LASTEXITCODE) { throw 'Native checks failed.' }
dotnet build (Join-Path $projectRoot 'tests/ManagedChecks.csproj') -c Release --nologo
if ($LASTEXITCODE) { throw 'Managed checks build failed.' }
& (Join-Path $projectRoot 'tests/bin/Release/net48/ManagedChecks.exe') (Join-Path $workspaceRoot 'artifacts/AirplayRadio/native/AirplayRadioNative.dll') (Join-Path $workspaceRoot '.work/airplay-tests/中文/receiver.key')
if ($LASTEXITCODE) { throw 'Managed/native interop checks failed.' }
