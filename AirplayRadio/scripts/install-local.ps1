[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$package = Join-Path $workspaceRoot 'artifacts/AirplayRadio'
$destination = Join-Path $env:USERPROFILE 'AppData/LocalLow/Colossal Order/Cities Skylines II/Mods/AirplayRadio'
if (Get-Process -Name Cities2 -ErrorAction SilentlyContinue) { throw 'Close Cities: Skylines II before installing Airplay Radio.' }
foreach ($file in @('AirplayRadio.dll', 'native/AirplayRadioNative.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $package $file))) { throw 'Build Airplay Radio first.' }
}
$backup = Join-Path $workspaceRoot ('.work/airplay-install-backups/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
foreach ($source in Get-ChildItem -LiteralPath $package -File -Recurse) {
    $relative = $source.FullName.Substring($package.Length + 1)
    $target = Join-Path $destination $relative
    if (Test-Path -LiteralPath $target) {
        $copy = Join-Path $backup $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $copy) | Out-Null
        Copy-Item -LiteralPath $target -Destination $copy
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $source.FullName -Destination $target -Force
}
Write-Output "Installed Airplay Radio: $destination"
