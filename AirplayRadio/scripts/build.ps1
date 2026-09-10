[CmdletBinding()]
param([string]$MsysRoot = 'C:/msys64', [switch]$SkipCodecs)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $projectRoot
$nativeBuild = Join-Path $workspaceRoot '.work/airplay-native'
$codecsRoot = Join-Path $workspaceRoot '.work/airplay-codecs'
$runtimeBin = Join-Path $MsysRoot 'ucrt64/bin'
$env:PATH = "$runtimeBin;$env:PATH"
$env:DOTNET_CLI_HOME = Join-Path $workspaceRoot '.tools/dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
if (!$SkipCodecs) {
    & (Join-Path $MsysRoot 'usr/bin/bash.exe') (Join-Path $PSScriptRoot 'build-codecs.sh')
    if ($LASTEXITCODE) { throw 'Minimal codec build failed.' }
}
& (Join-Path $runtimeBin 'cmake.exe') -S (Join-Path $projectRoot 'native') -B $nativeBuild -G Ninja -DCMAKE_BUILD_TYPE=Release "-DCODECS_ROOT=$codecsRoot"
if ($LASTEXITCODE) { throw 'CMake configure failed.' }
& (Join-Path $runtimeBin 'cmake.exe') --build $nativeBuild -j 6
if ($LASTEXITCODE) { throw 'Native build failed.' }
dotnet build (Join-Path $projectRoot 'src/AirplayRadio.csproj') -c Release --nologo
if ($LASTEXITCODE) { throw 'Managed mod build failed.' }
$package = Join-Path $workspaceRoot 'artifacts/AirplayRadio'
New-Item -ItemType Directory -Force -Path (Join-Path $package 'native') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $package 'icons') | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets/station.svg') -Destination (Join-Path $package 'icons/station.svg') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'src/bin/Release/net48/AirplayRadio.dll') -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'src/bin/Release/net48/AirplayRadio.pdb') -Destination $package -Force
$dll = Join-Path $nativeBuild 'AirplayRadioNative.dll'
Copy-Item -LiteralPath $dll -Destination (Join-Path $package 'native') -Force
# Verify imports: portable package must contain no third-party runtime DLL dependency.
$imports = & (Join-Path $runtimeBin 'objdump.exe') -p $dll
if ($LASTEXITCODE) { throw 'Cannot inspect native imports.' }
$allowed = @('kernel32.dll','msvcrt.dll','ucrtbase.dll','ws2_32.dll','iphlpapi.dll','crypt32.dll','gdi32.dll','bcrypt.dll','ole32.dll','uuid.dll','advapi32.dll','user32.dll','ntdll.dll','shell32.dll','secur32.dll')
foreach ($line in $imports) {
    if ($line -match 'DLL Name:\s*(\S+)') {
        $name = $Matches[1].ToLowerInvariant()
        if ($name -notlike 'api-ms-win-*' -and $name -notin $allowed) { throw "Unexpected runtime dependency: $name" }
    }
}
foreach ($file in @('README.md','README.en.md','README.zh-TW.md','USAGE.md','USAGE.zh-TW.md','CHANGELOG.md','CHANGELOG.zh-TW.md','VALIDATION.md','THIRD-PARTY-NOTICES.md','LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination $package -Force
}
foreach ($docDirectory in @('docs','release-assets','assets')) {
    $docDestination = Join-Path $package $docDirectory
    New-Item -ItemType Directory -Force -Path $docDestination | Out-Null
    Copy-Item -Path (Join-Path $projectRoot "$docDirectory/*.md") -Destination $docDestination -Force
}
New-Item -ItemType Directory -Force -Path (Join-Path $package 'licenses') | Out-Null
Copy-Item -Path (Join-Path $projectRoot 'licenses/*') -Destination (Join-Path $package 'licenses') -Force
Write-Output "Built Airplay Radio prototype (no helper EXE): $package"
