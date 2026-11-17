$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$project=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$manifest=Get-Content (Join-Path $PSScriptRoot 'generation.json') -Raw | ConvertFrom-Json
$order=@('PlayerHero','Tyler','Intern','Intern-Carried','Ram','Bed','Pipe','Sledgehammer','Box','Crate','Door','Exit','Car-Blue','Car-Gray','Car-Red','ParkingSpot','WallMap')
$sheet=[System.Drawing.Bitmap]::new(1536,900)
$g=[System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(39,43,54))
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
$font=[System.Drawing.Font]::new('Segoe UI',12)
$brush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(225,230,240))
for($i=0;$i -lt $order.Count;$i++){
 $item=$manifest.assets | Where-Object {$_.key -eq $order[$i]}
 $b=[System.Drawing.Bitmap]::new((Join-Path $project $item.path))
 try {
  $x=($i%6)*256; $y=[math]::Floor($i/6)*300
  $r=[math]::Min(224.0/$b.Width,224.0/$b.Height);$w=[int]($b.Width*$r);$h=[int]($b.Height*$r)
  $g.DrawImage($b,[System.Drawing.Rectangle]::new($x+(256-$w)/2,$y+(244-$h)/2,$w,$h))
  $g.DrawString($item.key,$font,$brush,$x+12,$y+262)
 }finally{$b.Dispose()}
}
$sheet.Save((Join-Path $PSScriptRoot 'Sprites-overview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$sheet.Dispose();$font.Dispose();$brush.Dispose()
