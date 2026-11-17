$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$project=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$backup=Join-Path $project 'Docs/ArtBackups/ArtRefresh-2026-10-30'
$classSource=Get-Content (Join-Path $project 'Docs/OfficeFurniture/Import-Art.ps1') -Raw
$classText=[regex]::Match($classSource,"(?s)@'\r?\n(.*?)\r?\n'@").Groups[1].Value
Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Private.Windows.Core,System.Private.Windows.GdiPlus -TypeDefinition $classText
$manifest=Get-Content (Join-Path $PSScriptRoot 'generation.json') -Raw | ConvertFrom-Json
$rows=@()
foreach($item in $manifest.assets){
 $template=Join-Path $backup $item.template
 $old=[System.Drawing.Bitmap]::new($template)
 $input=[System.Drawing.Bitmap]::new($item.generated)
 try {
  $factor=if([math]::Max($old.Width,$old.Height) -le 96){16}else{1}
  $w=$old.Width*$factor; $h=$old.Height*$factor
  $ob=[FurnitureArt]::Bounds($old);$ib=[FurnitureArt]::Bounds($input)
  if($item.key -eq 'Tyler'){
   # Use the same crop transform as the player so head and torso retain their size and location.
   $reference=[System.Drawing.Bitmap]::new($item.alignReference)
   try {
    if($reference.Width -ne $input.Width -or $reference.Height -ne $input.Height){throw 'Tyler and player reference canvases must match.'}
    $ib=[FurnitureArt]::Bounds($reference)
   } finally {$reference.Dispose()}
  }
  $ratio=[math]::Min(($ob.Width*$factor)/$ib.Width,($ob.Height*$factor)/$ib.Height)
  $dw=[int][math]::Round($ib.Width*$ratio);$dh=[int][math]::Round($ib.Height*$ratio)
  $dest=[System.Drawing.Rectangle]::new($ob.X*$factor+($ob.Width*$factor-$dw)/2,$ob.Y*$factor+($ob.Height*$factor-$dh)/2,$dw,$dh)
  $result=[System.Drawing.Bitmap]::new($w,$h,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g=[System.Drawing.Graphics]::FromImage($result)
  $target=Join-Path $project $item.path
  try {
   $g.Clear([System.Drawing.Color]::Transparent)
   $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
   $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
   $g.DrawImage($input,$dest,$ib,[System.Drawing.GraphicsUnit]::Pixel)
   $result.Save($target,[System.Drawing.Imaging.ImageFormat]::Png)
  } finally {$g.Dispose();$result.Dispose()}
  $meta=Get-Content ($template+'.meta') -Raw
  $originalPpu=[double][regex]::Match($meta,'spritePixelsToUnits: ([\d.]+)').Groups[1].Value
  $meta=[regex]::Replace($meta,'spritePixelsToUnits: ([\d.]+)',{param($m) 'spritePixelsToUnits: '+([double]$m.Groups[1].Value*$factor)})
  $meta=$meta -replace 'spriteMeshType: 1','spriteMeshType: 0'
  $meta=$meta -replace 'spriteGenerateFallbackPhysicsShape: 1','spriteGenerateFallbackPhysicsShape: 0'
  $meta=$meta -replace 'enableMipMap: 1','enableMipMap: 0'
  $meta=[regex]::Replace($meta,'(?m)^    filterMode: \d+','    filterMode: 0')
  $meta=[regex]::Replace($meta,'(?m)^(        (?:x|y|width|height): )([\d.]+)',{param($m) $m.Groups[1].Value+([double]$m.Groups[2].Value*$factor)})
  if($item.key -eq 'Tyler'){
   $guid=if(Test-Path -LiteralPath ($target+'.meta')){[regex]::Match((Get-Content ($target+'.meta') -Raw),'(?m)^guid: (\w+)').Groups[1].Value}else{[guid]::NewGuid().ToString('N')}
   $meta=[regex]::Replace($meta,'(?m)^guid: \w+',('guid: '+$guid))
   $meta=$meta.Replace('PlayerHero','Tyler')
  }
  Set-Content -LiteralPath ($target+'.meta') -Value $meta -NoNewline -Encoding utf8
  $rows+=[pscustomobject]@{key=$item.key;path=$item.path;width=$w;height=$h;ppu=$originalPpu*$factor;worldWidth=$old.Width/$originalPpu;worldHeight=$old.Height/$originalPpu}
 } finally {$old.Dispose();$input.Dispose()}
}
$rows | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot 'imported.json') -Encoding utf8
Write-Output ('Imported '+$rows.Count+' sprites with original world canvases and pivots.')
