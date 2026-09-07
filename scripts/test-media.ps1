$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent $PSScriptRoot
$ffmpegPath = 'C:/ProgramData/chocolatey/lib/ffmpeg/tools/ffmpeg/bin/ffmpeg.exe'
if (!(Test-Path -LiteralPath $ffmpegPath)) { $ffmpegPath = (Get-Command ffmpeg.exe -ErrorAction Stop).Source }
$fixturePath = Join-Path $workspacePath '.work\hls-fixtures'
New-Item -ItemType Directory -Path $fixturePath -Force | Out-Null
foreach ($format in @('media', 'fmp4', 'encrypted')) {
    $casePath = Join-Path $fixturePath $format
    New-Item -ItemType Directory -Path $casePath -Force | Out-Null
    $extra = @()
    if ($format -eq 'fmp4') { $extra = @('-hls_segment_type','fmp4') }
    if ($format -eq 'encrypted') {
        $keyPath = Join-Path $casePath 'key.bin'
        [IO.File]::WriteAllBytes($keyPath, [byte[]](1..16))
        $keyInfoPath = Join-Path $casePath 'keyinfo.txt'
        [IO.File]::WriteAllText($keyInfoPath, "key.bin`n$keyPath`n")
        $extra = @('-hls_key_info_file',$keyInfoPath)
    }
    Push-Location $casePath
    try {
        & $ffmpegPath -y -hide_banner -loglevel error -f lavfi -i 'sine=frequency=440:sample_rate=48000:duration=74' -c:a aac -b:a 96k -f hls -hls_time 10 -hls_list_size 0 -hls_flags omit_endlist @extra 'live.m3u8'
        if ($LASTEXITCODE -ne 0) { throw "Fixture generation failed: $format" }
    } finally { Pop-Location }
}
[IO.File]::WriteAllText((Join-Path $fixturePath 'master.m3u8'), "#EXTM3U`n#EXT-X-STREAM-INF:BANDWIDTH=96000,CODECS=`"mp4a.40.2`"`nmedia/live.m3u8`n")
& $ffmpegPath -y -hide_banner -loglevel error -f lavfi -i 'sine=frequency=660:duration=30' -c:a libmp3lame (Join-Path $fixturePath 'direct.mp3')
if ($LASTEXITCODE -ne 0) { throw 'MP3 fixture generation failed.' }
foreach ($mp3Case in @(
    @{ Name='intro32'; Rate=32000; Channels=1; Duration=0.75 },
    @{ Name='intro48'; Rate=48000; Channels=2; Duration=0.75 },
    @{ Name='main32'; Rate=32000; Channels=1; Duration=35 },
    @{ Name='main48'; Rate=48000; Channels=2; Duration=35 }
)) {
    $tone = "sine=frequency=733:sample_rate=$($mp3Case.Rate):duration=$($mp3Case.Duration)"
    & $ffmpegPath -y -hide_banner -loglevel error -f lavfi -i $tone -ac $mp3Case.Channels -c:a libmp3lame -b:a 128k (Join-Path $fixturePath ($mp3Case.Name + '.mp3'))
    if ($LASTEXITCODE -ne 0) { throw "MP3 format fixture generation failed: $($mp3Case.Name)" }
}
Add-Type -AssemblyName System.Drawing
$icon = New-Object System.Drawing.Bitmap 32,32
try { $icon.SetPixel(16,16,[System.Drawing.Color]::Blue); $icon.Save((Join-Path $fixturePath 'icon.png'),[System.Drawing.Imaging.ImageFormat]::Png) } finally { $icon.Dispose() }
$env:DOTNET_CLI_HOME = Join-Path $workspacePath '.tools\dotnet-home'
dotnet build (Join-Path $workspacePath 'tests\LiveRadio.Checks\LiveRadio.Checks.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Check runner failed to build.' }
& (Join-Path $workspacePath 'tests\LiveRadio.Checks\bin\Release\net48\LiveRadio.Checks.exe') --media-fixtures $fixturePath
if ($LASTEXITCODE -ne 0) { throw 'Media checks failed.' }
$runnerPath = Join-Path $workspacePath 'tests\LiveRadio.Checks\bin\Release\net48\LiveRadio.Checks.exe'
foreach ($fixture in @('direct.mp3','main48.mp3')) {
    & $runnerPath --mp3-restart-fixture (Join-Path $fixturePath $fixture)
    if ($LASTEXITCODE -ne 0) { throw "MP3 restart checks failed: $fixture" }
}
foreach ($transition in @(@('intro32.mp3','main48.mp3'), @('intro48.mp3','main32.mp3'))) {
    & $runnerPath --mp3-format-fixtures (Join-Path $fixturePath $transition[0]) (Join-Path $fixturePath $transition[1])
    if ($LASTEXITCODE -ne 0) { throw 'MP3 format transition checks failed.' }
}
