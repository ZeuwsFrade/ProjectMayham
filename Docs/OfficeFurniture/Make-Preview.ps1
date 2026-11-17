$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$items = (Get-Content (Join-Path $PSScriptRoot 'generation.json') -Raw | ConvertFrom-Json).assets
$sheet = [System.Drawing.Bitmap]::new(1792,1536)
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(39,43,54))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$font = [System.Drawing.Font]::new('Segoe UI',11)
$brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(224,229,238))
$i = 0
foreach ($item in $items) {
 $x = ($i % 7)*256; $y = [math]::Floor($i/7)*256
 $img = [System.Drawing.Bitmap]::new((Join-Path $project ('Assets/Sprites/Office Furniture/'+$item.name+'.png')))
 try {
  $ratio = [math]::Min(220.0/$img.Width,220.0/$img.Height)
  $w = [int]($img.Width*$ratio); $h = [int]($img.Height*$ratio)
  $g.DrawImage($img,[System.Drawing.Rectangle]::new($x+(256-$w)/2,$y+(226-$h)/2,$w,$h))
  $g.DrawString($item.name,$font,$brush,$x+9,$y+232)
 } finally { $img.Dispose() }
 $i++
}
$sheet.Save((Join-Path $PSScriptRoot 'OfficeFurniture-overview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$brush.Dispose(); $font.Dispose(); $g.Dispose(); $sheet.Dispose()
