[CmdletBinding()]
param([switch]$Live)
$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $workspacePath '.tools\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet build (Join-Path $workspacePath 'tests\LiveRadio.Checks\LiveRadio.Checks.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Check runner failed to build.' }
$runner = Join-Path $workspacePath 'tests\LiveRadio.Checks\bin\Release\net48\LiveRadio.Checks.exe'
if ($Live) { & $runner --live } else { & $runner }
if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
