$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$project=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$culture=[System.Globalization.CultureInfo]::InvariantCulture
$rows=Get-Content (Join-Path $PSScriptRoot 'barricade-draw.tsv')
$groups=@($rows | Where-Object { $_.StartsWith('B|') })
$sheet=[System.Drawing.Bitmap]::new(1024,960)
$g=[System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(39,43,54))
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
$font=[System.Drawing.Font]::new('Segoe UI',17)
$brush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(225,230,240))
$resolution=280.0
for($i=0;$i -lt $groups.Count;$i++){
 $header=$groups[$i].Split('|');$name=$header[1]
 $cx=512.0;$cy=$i*320.0+190.0
 $g.DrawString($name,$font,$brush,30,$i*320+16)
 $parts=@($rows | Where-Object {$_.StartsWith('S|'+$name+'|')} | ForEach-Object {,$_.Split('|')} | Sort-Object {[int]$_[12]})
 foreach($p in $parts){
  $b=[System.Drawing.Bitmap]::new((Join-Path $project $p[2]))
  $state=$g.Save()
  try{
   $v=@();for($j=3;$j -le 11;$j++){$v+=[double]::Parse($p[$j],$culture)}
   $m00=$v[0];$m01=$v[1];$m10=$v[2];$m11=$v[3];$px=$v[4];$py=$v[5];$ppu=$v[6];$ox=$v[7];$oy=$v[8]
   $a=$resolution*$m00/$ppu;$bb=-$resolution*$m10/$ppu
   $c=-$resolution*$m01/$ppu;$d=$resolution*$m11/$ppu
   $tx=$cx+$resolution*($px-$m00*$ox/$ppu+$m01*($b.Height-$oy)/$ppu)
   $ty=$cy-$resolution*($py-$m10*$ox/$ppu+$m11*($b.Height-$oy)/$ppu)
   $matrix=[System.Drawing.Drawing2D.Matrix]::new([single]$a,[single]$bb,[single]$c,[single]$d,[single]$tx,[single]$ty)
   try{
    $g.Transform=$matrix
    $g.DrawImage($b,[System.Drawing.Rectangle]::new(0,0,$b.Width,$b.Height),0,0,$b.Width,$b.Height,[System.Drawing.GraphicsUnit]::Pixel)
   }finally{$matrix.Dispose()}
  }finally{$g.Restore($state);$b.Dispose()}
 }
}
$sheet.Save((Join-Path $PSScriptRoot 'Barricades-overview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$sheet.Dispose();$font.Dispose();$brush.Dispose()
