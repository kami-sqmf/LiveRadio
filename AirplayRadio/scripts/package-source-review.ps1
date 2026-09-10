[CmdletBinding()]
param([string]$MsysRoot = 'C:/msys64')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $projectRoot
$version = ([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'src/AirplayRadio.csproj') -Raw)).Project.PropertyGroup.Version
$ffmpegRoot = Join-Path $workspaceRoot '.work/airplay-ffmpeg'
$ffmpegRevision = '894da5ca7d742e4429ffb2af534fcda0103ef593'
$actualRoot = (& git -C $ffmpegRoot rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -or [IO.Path]::GetFullPath($actualRoot) -ne [IO.Path]::GetFullPath($ffmpegRoot)) { throw 'FFmpeg must resolve to its own repository.' }
$actualRevision = (& git -C $ffmpegRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -or $actualRevision -ne $ffmpegRevision) { throw 'Unexpected FFmpeg revision.' }
$sourceChanges = @(& git -C $ffmpegRoot status --porcelain)
if ($LASTEXITCODE -or $sourceChanges.Count) { throw 'FFmpeg checkout must be clean.' }
$stage = Join-Path $workspaceRoot ('.work/airplay-source-review-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
$reviewFiles = @(Get-ChildItem -LiteralPath $projectRoot -File -Filter '*.md')
$reviewFiles += Get-Item -LiteralPath (Join-Path $projectRoot 'LICENSE')
foreach ($directory in @('src','native','scripts','tests','docs','licenses','assets')) {
    $reviewFiles += @(Get-ChildItem -LiteralPath (Join-Path $projectRoot $directory) -File -Recurse | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj|\.git|node_modules)[\\/]' -and
        $_.Extension -notin @('.dll','.exe','.pdb','.key','.log','.a','.o')
    })
}
$reviewFiles += Get-Item -LiteralPath (Join-Path $projectRoot 'release-assets/README.md')
foreach ($file in $reviewFiles) {
    $relative = $file.FullName.Substring($projectRoot.Length + 1)
    $destination = Join-Path $stage ('AirplayRadio/' + $relative)
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}
Copy-Item -LiteralPath (Join-Path $workspaceRoot 'Directory.Build.props') -Destination $stage
@'
<Project>
  <PropertyGroup>
    <GameManagedPath>C:/Path/To/Cities Skylines II/Cities2_Data/Managed</GameManagedPath>
    <ExtendedRadioPath>C:/Path/To/ExtendedRadio</ExtendedRadioPath>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $stage 'Local.props.example') -Encoding utf8
$dependencyRoot = Join-Path $stage 'dependencies'
New-Item -ItemType Directory -Path $dependencyRoot | Out-Null
$ffmpegArchive = Join-Path $dependencyRoot ('ffmpeg-' + $ffmpegRevision + '.tar')
& git -C $ffmpegRoot archive --format=tar --output=$ffmpegArchive $ffmpegRevision
if ($LASTEXITCODE) { throw 'FFmpeg source archive failed.' }
$installedPackages = @(Get-ChildItem -LiteralPath (Join-Path $MsysRoot 'var/lib/pacman/local') -Directory | Where-Object {
    $_.Name -match '^mingw-w64-ucrt-x86_64-(openssl|libplist|gcc|gcc-libs|winpthreads)-'
} | Select-Object -ExpandProperty Name)
$installedPackages | Set-Content -LiteralPath (Join-Path $dependencyRoot 'msys2-package-versions.txt') -Encoding utf8
$binaryHashes = foreach ($relative in @('AirplayRadio.dll','native/AirplayRadioNative.dll')) {
    $binary = Join-Path $workspaceRoot ('artifacts/AirplayRadio/' + $relative)
    (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $relative
}
$binaryHashes | Set-Content -LiteralPath (Join-Path $stage 'REVIEWED-BINARY-SHA256SUMS.txt') -Encoding utf8
@'
# Airplay Radio local source review snapshot

This snapshot is not cleared for public binary distribution and is not a complete corresponding-source release. See AirplayRadio/docs/publication-review.md for the unresolved license issue and missing dependency source materials.

Included: current mod sources, vendored receiver, tests, documentation, build scripts, approved icon sources, a pinned FFmpeg source archive, installed MSYS2 package versions and hashes of the reviewed binaries. Runtime DLLs themselves are not included. SHA256SUMS.txt covers every other snapshot file.

To prepare a source workspace, extract this ZIP and copy Local.props.example to Local.props with your own paths. Extract the FFmpeg tar contents into .work/airplay-ffmpeg. The current codec build script expects a Git checkout at the pinned revision, so restore and verify that checkout before building. See AirplayRadio/README.md for the toolchain and commands. A clean rebuild from this snapshot has not been verified.

Game/ExtendedRadio assemblies, private Local.props, Git metadata, receiver identity/keys, logs, savegames, runtime artwork caches and store screenshot PNGs are excluded.
'@ | Set-Content -LiteralPath (Join-Path $stage 'REVIEW-README.md') -Encoding utf8
$manifest = foreach ($file in Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName) {
    $relative = $file.FullName.Substring($stage.Length + 1).Replace('\','/')
    (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $relative
}
$manifest | Set-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Encoding utf8
$archivePath = Join-Path $workspaceRoot ('artifacts/AirplayRadio-' + $version + '-source-review.zip')
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archivePath -Force
Write-Output "Local source review snapshot: $archivePath"
Write-Output "Staging directory: $stage"
Write-Output 'No upload performed. Not a complete corresponding-source release.'
