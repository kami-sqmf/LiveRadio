[CmdletBinding()]
param(
    [ValidateSet('Private', 'Public', 'Unlisted')]
    [string]$AccessLevel = 'Public',
    [string]$ModId = '158648'
)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$workspacePath = Split-Path -Parent $projectPath
$version = ([xml](Get-Content -LiteralPath (Join-Path $projectPath 'src/AirplayRadio.csproj') -Raw)).Project.PropertyGroup.Version
$destination = Join-Path $workspacePath ('artifacts/AirplayRadio-publish-' + $version)
$assets = Join-Path $destination 'assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectPath 'release-assets/thumbnail.png') -Destination (Join-Path $assets 'thumbnail.png') -Force
foreach ($language in @('en', 'zh-TW')) {
    $xml = [System.Xml.XmlDocument]::new()
    $root = $xml.CreateElement('Publish'); [void]$xml.AppendChild($root)
    function Add-Value([string]$name, [string]$value) {
        $element = $xml.CreateElement($name); $element.SetAttribute('Value', $value); [void]$root.AppendChild($element)
    }
    Add-Value 'ModId' $ModId
    Add-Value 'DisplayName' 'Airplay Radio'
    $short = if ($language -eq 'en') { 'Phone audio in the native radio panel, with artwork, supported media controls and local gain. Requires ExtendedRadio.' } else { '在原生電台面板收聽手機 AirPlay 音訊，支援來源封面、媒體遙控與本機增益。需要 ExtendedRadio。' }
    Add-Value 'ShortDescription' $short
    $description = $xml.CreateElement('LongDescription')
    $description.InnerText = Get-Content -LiteralPath (Join-Path $projectPath "docs/paradox-description.$language.md") -Raw
    [void]$root.AppendChild($description)
    Add-Value 'Thumbnail' 'assets/thumbnail.png'
    # The listing uses only the inspected crop of the native radio panel.
    foreach ($kind in @('radio-panel')) {
        $name = "$kind.$language.png"
        $imagePath = Join-Path $projectPath ('release-assets/' + $name)
        if (!(Test-Path -LiteralPath $imagePath -PathType Leaf) -and $language -ne 'en') {
            $name = "$kind.en.png"
            $imagePath = Join-Path $projectPath ('release-assets/' + $name)
        }
        if (!(Test-Path -LiteralPath $imagePath -PathType Leaf)) {
            throw "Required radio panel screenshot is missing: $imagePath"
        }
        Copy-Item -LiteralPath $imagePath -Destination (Join-Path $assets $name) -Force
        Add-Value 'Screenshot' ('assets/' + $name)
    }
    Add-Value 'ModVersion' $version
    Add-Value 'GameVersion' '1.6.*'
    $dependency = $xml.CreateElement('Dependency'); $dependency.SetAttribute('Id', '75862'); [void]$root.AppendChild($dependency)
    $sourceLink = $xml.CreateElement('ExternalLink')
    $sourceLink.SetAttribute('Type', 'github')
    $sourceLink.SetAttribute('Url', 'https://github.com/kami-sqmf/LiveRadio/tree/main/AirplayRadio')
    [void]$root.AppendChild($sourceLink)
    Add-Value 'ChangeLog' $(if ($language -eq 'en') { 'English and Traditional Chinese interface; phone controls, circular artwork and local audio gain.' } else { '中英文介面、手機媒體遙控、圓形封面及本機音量增益。' })
    Add-Value 'AccessLevel' $AccessLevel
    $xml.Save((Join-Path $destination "PublishConfiguration.$language.xml"))
}
Copy-Item -LiteralPath (Join-Path $destination 'PublishConfiguration.en.xml') -Destination (Join-Path $destination 'PublishConfiguration.xml') -Force
Write-Output "Prepared local publisher drafts: $destination"
Write-Output 'PublishConfiguration.xml uses English as the primary listing language.'
Write-Output 'No upload was performed. English screenshots are used where localized captures are absent. Public source availability still requires review.'
