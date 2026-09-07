<#
.SYNOPSIS
    Lists (and optionally removes) regenerable build output and uncited scratch files.
.DESCRIPTION
    Dry run by default: nothing is deleted unless -Execute is passed.

    Tier Safe   - reproducible by build.ps1 / test-media.ps1 / npm ci / dotnet restore.
    Tier Review - objective but judgement-worthy: install backups beyond the newest few,
                  release zips older than the current version, and .work entries that no
                  document, script or test cites.

    Nothing is hardcoded to a version. The current version comes from Directory.Build.props,
    and the "cited" set is derived by scanning docs, READMEs, scripts and tests for each
    .work entry name, so the guard keeps working as the project moves on.
.EXAMPLE
    scripts\clean-workspace.ps1
    scripts\clean-workspace.ps1 -Tier All
    scripts\clean-workspace.ps1 -Tier Safe -Execute
#>
[CmdletBinding()]
param(
    [switch]$Execute,
    [ValidateSet('Safe', 'Review', 'All')][string]$Tier = 'Safe',
    [int]$KeepInstallBackups = 3,
    [int]$MinimumAgeMinutes = 30
)
$ErrorActionPreference = 'Stop'
$workspacePath = (Resolve-Path (Split-Path -Parent $PSScriptRoot)).Path
$workPath = Join-Path $workspacePath '.work'
$cutoff = (Get-Date).AddMinutes(-$MinimumAgeMinutes)
$version = ([xml](Get-Content -LiteralPath (Join-Path $workspacePath 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version

# Every .work name mentioned by a document, script or test is evidence someone still points at it.
$citingText = ''
foreach ($pattern in @('docs\*.md', 'README*.md', 'scripts\*.ps1', 'tests\*.cs', 'tests\*\*.cs', 'tests\*\*.cjs', 'tests\*\*.py')) {
    foreach ($file in Get-ChildItem -LiteralPath $workspacePath -Filter (Split-Path -Leaf $pattern) -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notlike '*\node_modules\*' -and $_.FullName -notlike '*\.work\*' -and $_.FullName -notlike '*\bin\*' -and $_.FullName -notlike '*\obj\*' }) {
        $citingText += (Get-Content -LiteralPath $file.FullName -Raw) + "`n"
    }
}
$citedNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in Get-ChildItem -LiteralPath $workPath -Force -ErrorAction SilentlyContinue) {
    if ($citingText.Contains($entry.Name)) { [void]$citedNames.Add($entry.Name) }
}

# Structural keeps that no document has to spell out.
# generate-country-labels.cjs is the generator behind src/LiveRadio.UI/src/countries.ts;
# nothing imports it, but regenerating the region names needs it.
$structuralKeeps = @('before-0.4.0', 'install-backups', 'hls-fixtures', 'pcm-lock-baseline',
                     'generate-country-labels.cjs')

function Test-Protected([string]$name) {
    if ($citedNames.Contains($name)) { return $true }
    if ($structuralKeeps -contains $name) { return $true }
    if ($name -like 'validation-*') { return $true }
    if ($name.Contains($version)) { return $true }   # anything named after the version in flight
    return $false
}

$candidates = [Collections.Generic.List[object]]::new()
function Get-PathSize([string]$fullPath) {
    if (Test-Path -LiteralPath $fullPath -PathType Leaf) { return [long](Get-Item -LiteralPath $fullPath).Length }
    $sum = (Get-ChildItem -LiteralPath $fullPath -Recurse -File -Force -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum).Sum
    if ($null -eq $sum) { return [long]0 }
    return [long]$sum
}
function Add-Candidate([string]$itemTier, [string]$group, [string]$fullPath, [string]$restore) {
    if (!(Test-Path -LiteralPath $fullPath)) { return }
    $resolved = (Resolve-Path -LiteralPath $fullPath).Path
    if (!$resolved.StartsWith($workspacePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing a path outside the workspace: $resolved"
    }
    if ((Get-Item -LiteralPath $resolved).LastWriteTime -gt $cutoff) { return }   # still warm; a run may be in flight
    $candidates.Add([pscustomobject]@{
        Tier     = $itemTier
        Group    = $group
        Relative = $resolved.Substring($workspacePath.Length + 1)
        FullPath = $resolved
        Bytes    = Get-PathSize $resolved
        Restore  = $restore
    })
}

# ---------- Tier: Safe ----------
$targetFrameworks = (Get-ChildItem -LiteralPath $workspacePath -Filter '*.csproj' -Recurse -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
foreach ($refPack in Get-ChildItem -LiteralPath (Join-Path $workspacePath '.tools\packages') -Directory -Filter 'microsoft.netframework.referenceassemblies.net*' -ErrorAction SilentlyContinue) {
    $moniker = $refPack.Name.Split('.')[-1]
    if ($targetFrameworks -notmatch [regex]::Escape($moniker)) {
        Add-Candidate 'Safe' 'Stale NuGet restore' $refPack.FullName "dotnet restore - no csproj targets $moniker"
    }
}
Add-Candidate 'Safe' 'Snapshot node_modules' (Join-Path $workPath 'before-0.4.0\src\LiveRadio.UI\node_modules') 'npm ci inside that snapshot, if it is ever needed'
Add-Candidate 'Safe' 'HLS/MP3 fixtures' (Join-Path $workPath 'hls-fixtures') 'scripts\test-media.ps1 (requires ffmpeg)'
foreach ($stage in Get-ChildItem -LiteralPath $workPath -Directory -Filter 'package-*' -ErrorAction SilentlyContinue) {
    Add-Candidate 'Safe' 'Packaging staging leftovers' $stage.FullName 'scripts\package-release.ps1 - leftover staging; the zip in artifacts is the real output'
}
foreach ($project in @('src\LiveRadio.Core', 'src\LiveRadio.Mod', 'tests\LiveRadio.Checks')) {
    foreach ($dir in @('bin', 'obj')) {
        Add-Candidate 'Safe' 'Build output' (Join-Path $workspacePath (Join-Path $project $dir)) 'scripts\build.ps1'
    }
}
$snapshotPath = Join-Path $workPath 'before-0.4.0'
if (Test-Path -LiteralPath $snapshotPath) {
    $snapshotOutputs = Get-ChildItem -LiteralPath $snapshotPath -Recurse -Directory -Force -ErrorAction SilentlyContinue |
        Where-Object { ($_.Name -eq 'bin' -or $_.Name -eq 'obj') -and $_.FullName -notlike '*node_modules*' }
    foreach ($dir in $snapshotOutputs) {
        Add-Candidate 'Safe' 'Snapshot build output' $dir.FullName 'not needed - the snapshot is a source-only reference'
    }
}

# ---------- Tier: Review ----------
$backupRoot = Join-Path $workPath 'install-backups'
if (Test-Path -LiteralPath $backupRoot) {
    $staleBackups = Get-ChildItem -LiteralPath $backupRoot -Directory | Sort-Object Name -Descending | Select-Object -Skip $KeepInstallBackups
    foreach ($backup in $staleBackups) {
        Add-Candidate 'Review' "Install backups beyond the newest $KeepInstallBackups" $backup.FullName 'none - copies of previously installed mod DLLs'
    }
}
foreach ($zip in Get-ChildItem -LiteralPath (Join-Path $workspacePath 'artifacts') -File -Filter 'LiveRadio-*.zip' -ErrorAction SilentlyContinue) {
    if ($zip.Name -notlike "*$version*") {
        Add-Candidate 'Review' "Release zips older than $version" $zip.FullName 'scripts\package-release.ps1 at that source version'
    }
}
foreach ($entry in Get-ChildItem -LiteralPath $workPath -File -Force -ErrorAction SilentlyContinue) {
    if (Test-Protected $entry.Name) { continue }
    Add-Candidate 'Review' 'Uncited .work scratch (verify each)' $entry.FullName 'none - no doc, script or test references this name'
}

# ---------- Report ----------
$selected = @($candidates | Where-Object { $Tier -eq 'All' -or $_.Tier -eq $Tier })
$mode = if ($Execute) { 'EXECUTE' } else { 'DRY RUN' }
Write-Output ''
Write-Output ('Live Radio workspace cleanup - {0} - tier: {1}' -f $mode, $Tier)
Write-Output ('Workspace: {0}   version in Directory.Build.props: {1}' -f $workspacePath, $version)
Write-Output ('Skipping anything modified in the last {0} minute(s).' -f $MinimumAgeMinutes)
Write-Output ''
foreach ($group in $selected | Group-Object Tier, Group | Sort-Object Name) {
    $groupBytes = ($group.Group | Measure-Object -Property Bytes -Sum).Sum
    Write-Output ('[{0}] {1}  ({2:N1} MB, {3} path(s))' -f $group.Group[0].Tier, $group.Group[0].Group, ($groupBytes / 1MB), $group.Count)
    Write-Output ('       restore: ' + $group.Group[0].Restore)
    foreach ($item in $group.Group | Sort-Object -Property Bytes -Descending) {
        Write-Output ('       {0,10:N2} MB  {1}   (modified {2:yyyy-MM-dd HH:mm})' -f ($item.Bytes / 1MB), $item.Relative, (Get-Item -LiteralPath $item.FullPath).LastWriteTime)
    }
    Write-Output ''
}
$totalBytes = ($selected | Measure-Object -Property Bytes -Sum).Sum
Write-Output ('TOTAL: {0:N1} MB across {1} path(s)' -f ($totalBytes / 1MB), $selected.Count)
Write-Output ('Kept as cited by docs/scripts/tests: {0} .work entr(ies).' -f $citedNames.Count)

if (!$Execute) {
    Write-Output ''
    Write-Output 'Nothing was deleted. Re-run with -Execute to remove the listed paths.'
    Write-Output 'This workspace is not a git repository - deletions cannot be undone.'
    return
}

Write-Output ''
foreach ($item in $selected) {
    Remove-Item -LiteralPath $item.FullPath -Recurse -Force
    Write-Output ('removed  ' + $item.Relative)
}
Write-Output ('Reclaimed {0:N1} MB.' -f ($totalBytes / 1MB))
