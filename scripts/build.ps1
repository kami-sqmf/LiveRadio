[CmdletBinding()]
param([string]$GameManagedPath, [string]$ExtendedRadioPath, [switch]$Diagnostics)
$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent $PSScriptRoot
$uiPath = Join-Path $workspacePath 'src\LiveRadio.UI'
Push-Location $uiPath
try {
    if (!(Test-Path -LiteralPath 'node_modules\esbuild')) {
        npm ci --ignore-scripts --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'UI dependencies failed to restore.' }
    }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'UI build failed.' }
} finally { Pop-Location }
$userDataPath = Join-Path $env:USERPROFILE 'AppData\LocalLow\Colossal Order\Cities Skylines II'
if (!$GameManagedPath) {
    $logPath = Join-Path $userDataPath 'Player.log'
    if (Test-Path -LiteralPath $logPath) {
        $match = Select-String -LiteralPath $logPath -Pattern "Mono path\[0\] = '(.*)'" | Select-Object -First 1
        if ($match) { $GameManagedPath = $match.Matches[0].Groups[1].Value }
    }
}
if (!$ExtendedRadioPath) {
    $modCache = Join-Path $userDataPath '.cache\Mods'
    $candidates = @(Get-ChildItem -LiteralPath $modCache -Filter 'ExtendedRadio.dll' -File -Recurse -ErrorAction SilentlyContinue)
    $latest = $candidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest) { $ExtendedRadioPath = $latest.DirectoryName }
}
if (!$GameManagedPath -or !(Test-Path -LiteralPath (Join-Path $GameManagedPath 'Game.dll'))) { throw 'Game.dll not found. Pass -GameManagedPath with the Cities2_Data/Managed folder.' }
if (!$ExtendedRadioPath -or !(Test-Path -LiteralPath (Join-Path $ExtendedRadioPath 'ExtendedRadio.dll'))) { throw 'ExtendedRadio.dll not found. Subscribe to ExtendedRadio or pass -ExtendedRadioPath.' }
$escapedGame = [System.Security.SecurityElement]::Escape($GameManagedPath)
$escapedRadio = [System.Security.SecurityElement]::Escape($ExtendedRadioPath)
@"
<Project>
  <PropertyGroup>
    <GameManagedPath>$escapedGame</GameManagedPath>
    <ExtendedRadioPath>$escapedRadio</ExtendedRadioPath>
  </PropertyGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $workspacePath 'Local.props') -Encoding utf8
$env:DOTNET_CLI_HOME = Join-Path $workspacePath '.tools\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet build (Join-Path $workspacePath 'src\LiveRadio.Mod\LiveRadio.Mod.csproj') -c Release --nologo "-p:LiveRadioDiagnostics=$($Diagnostics.IsPresent.ToString().ToLowerInvariant())"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$artifactPath = Join-Path $workspacePath $(if ($Diagnostics) { 'artifacts\LiveRadio-diagnostic' } else { 'artifacts\LiveRadio' })
New-Item -ItemType Directory -Path $artifactPath -Force | Out-Null
$outputPath = Join-Path $workspacePath 'src\LiveRadio.Mod\bin\Release\net48'
foreach ($filename in @('LiveRadio.dll', 'LiveRadio.pdb', 'LiveRadio.Core.dll', 'LiveRadio.Core.pdb', 'NLayer.dll')) {
    Copy-Item -LiteralPath (Join-Path $outputPath $filename) -Destination $artifactPath -Force
}
foreach ($filename in @('README.md', 'README.zh-TW.md', 'CHANGELOG.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) {
    $source = Join-Path $workspacePath $filename
    if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $artifactPath -Force }
}
New-Item -ItemType Directory -Path (Join-Path $artifactPath 'licenses') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $artifactPath 'docs') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $workspacePath 'docs') -Filter '*.md' -File | Copy-Item -Destination (Join-Path $artifactPath 'docs') -Force
Copy-Item -LiteralPath (Join-Path $workspacePath 'licenses/UNICODE-LICENSE.txt') -Destination (Join-Path $artifactPath 'licenses/UNICODE-LICENSE.txt') -Force
foreach ($filename in @('LiveRadio.mjs', 'LiveRadio.css')) {
    Copy-Item -LiteralPath (Join-Path $uiPath ('dist\' + $filename)) -Destination $artifactPath -Force
}
Write-Output "Built local mod: $artifactPath"
