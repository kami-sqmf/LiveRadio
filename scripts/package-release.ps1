[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent $PSScriptRoot
$artifactPath = Join-Path $workspacePath 'artifacts/LiveRadio'
$version = ([xml](Get-Content -LiteralPath (Join-Path $workspacePath 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $artifactPath 'LiveRadio.dll')).Version.ToString(3)
if ($assemblyVersion -ne $version) { throw 'Build artifacts do not match the source version. Run build.ps1 first.' }
$runtimeFiles = @('LiveRadio.dll','LiveRadio.Core.dll','NLayer.dll','LiveRadio.mjs','LiveRadio.css')
$docFiles = @('README.md','README.zh-TW.md','CHANGELOG.md','LICENSE','THIRD-PARTY-NOTICES.md','licenses/UNICODE-LICENSE.txt')
$docFiles += @(Get-ChildItem -LiteralPath (Join-Path $workspacePath 'docs') -Filter '*.md' -File | ForEach-Object { 'docs/' + $_.Name })
$files = $runtimeFiles + $docFiles
$stagePath = Join-Path $workspacePath ('.work/package-' + $version + '-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagePath -Force | Out-Null
$hashes = foreach ($file in $files) {
    $sourcePath = if ($file -in $runtimeFiles) { Join-Path $artifactPath $file } else { Join-Path $workspacePath $file }
    if (!(Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Missing required package file: $file" }
    $destination = Join-Path $stagePath $file
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $sourcePath -Destination $destination
    (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $file
}
$hashes | Set-Content -LiteralPath (Join-Path $stagePath 'SHA256SUMS.txt') -Encoding utf8
$zipPath = Join-Path $workspacePath ('artifacts/LiveRadio-' + $version + '.zip')
Compress-Archive -Path (Join-Path $stagePath '*') -DestinationPath $zipPath -Force
Write-Output "Prepared release package: $zipPath"
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256 | Select-Object Hash,Path
