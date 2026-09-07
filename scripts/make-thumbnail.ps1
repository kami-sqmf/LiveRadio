[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$outputDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'release-assets'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$bitmap = New-Object System.Drawing.Bitmap 1024,1024
$canvas = [System.Drawing.Graphics]::FromImage($bitmap)
$canvas.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$canvas.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$navy = [System.Drawing.ColorTranslator]::FromHtml('#102633')
$cyan = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#74E3D0'))
$white = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#F4F6F2'))
$muted = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#A7BCC5'))
$large = [System.Drawing.Font]::new('Segoe UI',112,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
$small = [System.Drawing.Font]::new('Segoe UI',30,[System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
$label = [System.Drawing.Font]::new('Segoe UI',24,[System.Drawing.FontStyle]::Bold,[System.Drawing.GraphicsUnit]::Pixel)
try {
    $canvas.Clear($navy)
    $canvas.DrawString('INTERNET RADIO FOR YOUR CITY',$label,$muted,76,76)
    $heights = @(55,110,180,115,250,350,230,150,290,400,260,170,110,65)
    for ($i=0; $i -lt $heights.Count; $i++) {
        $height = $heights[$i]
        $canvas.FillRectangle($cyan,76+$i*62,360-$height/2,24,$height)
    }
    $canvas.DrawString('LIVE',$large,$white,65,582)
    $canvas.DrawString('RADIO',$large,$white,65,705)
    $canvas.FillRectangle($cyan,76,878,72,5)
    $canvas.DrawString('CITIES: SKYLINES II',$small,$muted,76,921)
    $bitmap.Save((Join-Path $outputDirectory 'thumbnail.png'),[System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $label.Dispose(); $small.Dispose(); $large.Dispose()
    $muted.Dispose(); $white.Dispose(); $cyan.Dispose()
    $canvas.Dispose(); $bitmap.Dispose()
}
Write-Output 'Created release-assets/thumbnail.png (1024 x 1024).'
