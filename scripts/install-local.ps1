[CmdletBinding()]
param([switch]$Diagnostics)
$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent $PSScriptRoot
$artifactPath = Join-Path $workspacePath $(if ($Diagnostics) { 'artifacts\LiveRadio-diagnostic' } else { 'artifacts\LiveRadio' })
$destinationPath = Join-Path $env:USERPROFILE 'AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\LiveRadio'
if (Get-Process -Name Cities2 -ErrorAction SilentlyContinue) { throw 'Close Cities: Skylines II before replacing the mod DLLs.' }
if (!(Test-Path -LiteralPath (Join-Path $artifactPath 'LiveRadio.dll'))) { throw 'Run scripts/build.ps1 first.' }
$files = @('LiveRadio.dll', 'LiveRadio.pdb', 'LiveRadio.Core.dll', 'LiveRadio.Core.pdb', 'NLayer.dll', 'LiveRadio.mjs', 'LiveRadio.css', 'README.md', 'README.zh-TW.md', 'CHANGELOG.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'licenses/UNICODE-LICENSE.txt')
$files += @(Get-ChildItem -LiteralPath (Join-Path $artifactPath 'docs') -Filter '*.md' -File | ForEach-Object { 'docs/' + $_.Name })
foreach ($required in @('LiveRadio.mjs', 'LiveRadio.css')) {
    if (!(Test-Path -LiteralPath (Join-Path $artifactPath $required))) { throw "Missing UI artifact: $required. Run scripts/build.ps1." }
}
New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
$backupPath = Join-Path $workspacePath ('.work\install-backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
foreach ($filename in $files) {
    $source = Join-Path $artifactPath $filename
    if (!(Test-Path -LiteralPath $source)) { continue }
    $destination = Join-Path $destinationPath $filename
    if (Test-Path -LiteralPath $destination) {
        $backupFile = Join-Path $backupPath $filename
        New-Item -ItemType Directory -Path (Split-Path -Parent $backupFile) -Force | Out-Null
        Copy-Item -LiteralPath $destination -Destination $backupFile
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
}
Write-Output "Installed local mod: $destinationPath"
