[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$artifactPath = Join-Path $workspacePath 'artifacts/AirplayRadio'
$version = ([xml](Get-Content -LiteralPath (Join-Path $projectPath 'src/AirplayRadio.csproj') -Raw)).Project.PropertyGroup.Version
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $artifactPath 'AirplayRadio.dll')).Version.ToString(3)
if ($assemblyVersion -ne $version) { throw 'Build artifacts do not match the source version.' }
$runtimeFiles = @('AirplayRadio.dll', 'native/AirplayRadioNative.dll', 'icons/station.svg')
$docFiles = @('README.md','README.en.md','README.zh-TW.md','USAGE.md','USAGE.zh-TW.md','CHANGELOG.md','CHANGELOG.zh-TW.md','VALIDATION.md','LICENSE','THIRD-PARTY-NOTICES.md')
$docFiles += @('release-assets/README.md', 'assets/GENERATION.md')
$docFiles += @(Get-ChildItem -LiteralPath (Join-Path $projectPath 'docs') -File -Filter '*.md' | ForEach-Object { 'docs/' + $_.Name })
$docFiles += @(Get-ChildItem -LiteralPath (Join-Path $projectPath 'licenses') -File | ForEach-Object { 'licenses/' + $_.Name })
$stagePath = Join-Path $workspacePath ('.work/airplay-release-' + $version + '-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagePath | Out-Null
$hashes = foreach ($file in ($runtimeFiles + $docFiles)) {
    $sourcePath = if ($file -in $runtimeFiles) { Join-Path $artifactPath $file } else { Join-Path $projectPath $file }
    if (!(Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Missing required package file: $file" }
    $destination = Join-Path $stagePath $file
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $sourcePath -Destination $destination
    (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $file
}
$hashes | Set-Content -LiteralPath (Join-Path $stagePath 'SHA256SUMS.txt') -Encoding utf8
$zipPath = Join-Path $workspacePath ('artifacts/AirplayRadio-' + $version + '.zip')
Compress-Archive -Path (Join-Path $stagePath '*') -DestinationPath $zipPath -Force
Write-Output "Prepared release package: $zipPath"
Write-Output "Publisher content directory: $stagePath"
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256 | Select-Object Hash,Path
