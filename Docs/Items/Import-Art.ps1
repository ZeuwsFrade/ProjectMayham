$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$project=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$backup=Join-Path $project 'Docs/ArtBackups/Items-2026-10-30'
$target=Join-Path $project 'Assets/Sprites/Items'
$oldScript=Get-Content (Join-Path $project 'Docs/OfficeFurniture/Import-Art.ps1') -Raw
$classText=[regex]::Match($oldScript,"(?s)@'\r?\n(.*?)\r?\n'@").Groups[1].Value
Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Private.Windows.Core,System.Private.Windows.GdiPlus -TypeDefinition $classText
$manifest=Get-Content (Join-Path $PSScriptRoot 'generation.json') -Raw | ConvertFrom-Json
foreach($item in $manifest.assets) {
 $old=[System.Drawing.Bitmap]::new((Join-Path $backup ($item.name+'.png')))
 $input=[System.Drawing.Bitmap]::new($item.generated)
 try {
  $factor=if([math]::Max($old.Width,$old.Height) -le 64){16}else{1}
  $w=$old.Width*$factor; $h=$old.Height*$factor
  $bounds=[FurnitureArt]::Bounds($input)
  $ratio=[math]::Min(($w*0.9)/$bounds.Width,($h*0.9)/$bounds.Height)
  $dw=[int][math]::Round($bounds.Width*$ratio); $dh=[int][math]::Round($bounds.Height*$ratio)
  $result=[System.Drawing.Bitmap]::new($w,$h,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g=[System.Drawing.Graphics]::FromImage($result)
  try {
   $g.Clear([System.Drawing.Color]::Transparent)
   $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
   $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
   $g.DrawImage($input,[System.Drawing.Rectangle]::new(($w-$dw)/2,($h-$dh)/2,$dw,$dh),$bounds,[System.Drawing.GraphicsUnit]::Pixel)
   $result.Save((Join-Path $target ($item.name+'.png')),[System.Drawing.Imaging.ImageFormat]::Png)
  } finally {$g.Dispose();$result.Dispose()}
  $meta=Get-Content (Join-Path $backup ($item.name+'.png.meta')) -Raw
  $meta=[regex]::Replace($meta,'spritePixelsToUnits: ([\d.]+)',{param($m) 'spritePixelsToUnits: '+([double]$m.Groups[1].Value*$factor)})
  $meta=$meta -replace 'spriteMeshType: 1','spriteMeshType: 0'
  $meta=$meta -replace 'spriteGenerateFallbackPhysicsShape: 1','spriteGenerateFallbackPhysicsShape: 0'
  $meta=[regex]::Replace($meta,'(?m)^(        (?:x|y|width|height): )([\d.]+)',{param($m) $m.Groups[1].Value+([double]$m.Groups[2].Value*$factor)})
  Set-Content -LiteralPath (Join-Path $target ($item.name+'.png.meta')) -Value $meta -NoNewline -Encoding utf8
 }finally {$old.Dispose();$input.Dispose()}
}
$sheet=[System.Drawing.Bitmap]::new(1536,900)
$g=[System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(35,39,49))
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
$font=[System.Drawing.Font]::new('Segoe UI',12)
$brush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(225,230,240))
$i=0
foreach($item in $manifest.assets) {
 $x=($i%6)*256; $y=[math]::Floor($i/6)*300
 $b=[System.Drawing.Bitmap]::new((Join-Path $target ($item.name+'.png')))
 try {
  $r=[math]::Min(208.0/$b.Width,208.0/$b.Height);$w=[int]($b.Width*$r);$h=[int]($b.Height*$r)
  $g.DrawImage($b,[System.Drawing.Rectangle]::new($x+(256-$w)/2,$y+(218-$h)/2,$w,$h))
  $g.DrawString($item.name,$font,$brush,$x+12,$y+220)
  $r=[math]::Min(48.0/$b.Width,48.0/$b.Height);$w=[int]($b.Width*$r);$h=[int]($b.Height*$r)
  $g.DrawImage($b,[System.Drawing.Rectangle]::new($x+12,$y+247,$w,$h))
 }finally{$b.Dispose()}
 $i++
}
$sheet.Save((Join-Path $PSScriptRoot 'Items-overview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$sheet.Dispose();$brush.Dispose();$font.Dispose()
Write-Output ('Imported '+$manifest.assets.Count+' inventory icons.')

