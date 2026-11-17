$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$project = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source = Join-Path $project 'Docs/ArtBackups/OfficeFurniture-2026-10-29'
$target = Join-Path $project 'Assets/Sprites/Office Furniture'
$manifest = Get-Content (Join-Path $PSScriptRoot 'generation.json') -Raw | ConvertFrom-Json
Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Private.Windows.Core,System.Private.Windows.GdiPlus -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
public static class FurnitureArt {
 public static Rectangle Bounds(Bitmap b) {
  int x0=b.Width,y0=b.Height,x1=-1,y1=-1;
  for(int y=0;y<b.Height;y++) for(int x=0;x<b.Width;x++) if(b.GetPixel(x,y).A>8) {x0=Math.Min(x0,x);y0=Math.Min(y0,y);x1=Math.Max(x1,x);y1=Math.Max(y1,y);}
  if(x1<0) throw new Exception("Empty alpha");
  return new Rectangle(x0,y0,x1-x0+1,y1-y0+1);
 }
 public static void Convert(string original,string generated,string output,int scale) {
  using(var old=new Bitmap(original)) using(var input=new Bitmap(generated)) {
   if(!Image.IsAlphaPixelFormat(input.PixelFormat)) throw new Exception("Missing alpha: "+generated);
   var ob=Bounds(old); var ib=Bounds(input);
   if(ib.Width==input.Width && ib.Height==input.Height && input.GetPixel(0,0).A>0) throw new Exception("Opaque background: "+generated);
   using(var result=new Bitmap(old.Width*scale,old.Height*scale,PixelFormat.Format32bppArgb)) using(var g=Graphics.FromImage(result)) {
    g.Clear(Color.Transparent); g.InterpolationMode=InterpolationMode.NearestNeighbor; g.PixelOffsetMode=PixelOffsetMode.Half;
    double ratio=Math.Min((double)(ob.Width*scale)/ib.Width,(double)(ob.Height*scale)/ib.Height);
    int w=(int)Math.Round(ib.Width*ratio),h=(int)Math.Round(ib.Height*ratio);
    var dest=new Rectangle(ob.X*scale+(ob.Width*scale-w)/2,ob.Y*scale+(ob.Height*scale-h)/2,w,h);
    g.DrawImage(input,dest,ib,GraphicsUnit.Pixel); result.Save(output,ImageFormat.Png);
   }
  }
 }
 public static Rectangle Find(Bitmap atlas,Bitmap item) {
  var b=Bounds(item);
  for(int y=0;y<=atlas.Height-b.Height;y++) for(int x=0;x<=atlas.Width-b.Width;x++) {
   bool same=true;
   for(int iy=0;iy<b.Height&&same;iy++) for(int ix=0;ix<b.Width;ix++) {
    var p=item.GetPixel(b.X+ix,b.Y+iy);var q=atlas.GetPixel(x+ix,y+iy);
    if(p.A!=q.A || (p.A>0 && p.ToArgb()!=q.ToArgb())) {same=false;break;}
   }
   if(same) return new Rectangle(x,y,b.Width,b.Height);
  }
  throw new Exception("Atlas match missing");
 }
}
'@
$scale = 16
foreach ($item in $manifest.assets) {
 $name = $item.name
 $out = Join-Path $target "$name.png"
 [FurnitureArt]::Convert((Join-Path $source "$name.png"),$item.generated,$out,$scale)
 $meta = Get-Content (Join-Path $source "$name.png.meta") -Raw
 $meta = [regex]::Replace($meta,'spritePixelsToUnits: ([\d.]+)',{param($m) 'spritePixelsToUnits: ' + ([double]$m.Groups[1].Value * $scale)})
 Set-Content -LiteralPath "$out.meta" -Value $meta -NoNewline -Encoding utf8
}
# Rebuild the source atlas using exact matches against the backed-up original.
$atlasOld = [System.Drawing.Bitmap]::new((Join-Path $source '0-Tileset.png'))
$atlasNew = [System.Drawing.Bitmap]::new($atlasOld.Width*$scale,$atlasOld.Height*$scale)
$graphics = [System.Drawing.Graphics]::FromImage($atlasNew)
$graphics.Clear([System.Drawing.Color]::Transparent)
foreach ($item in $manifest.assets) {
 $old = [System.Drawing.Bitmap]::new((Join-Path $source ($item.name+'.png')))
 $new = [System.Drawing.Bitmap]::new((Join-Path $target ($item.name+'.png')))
 try {
  $position = [FurnitureArt]::Find($atlasOld,$old)
  $bounds = [FurnitureArt]::Bounds($old)
  $dest = [System.Drawing.Rectangle]::new($position.X*$scale,$position.Y*$scale,$position.Width*$scale,$position.Height*$scale)
  $src = [System.Drawing.Rectangle]::new($bounds.X*$scale,$bounds.Y*$scale,$bounds.Width*$scale,$bounds.Height*$scale)
  $graphics.DrawImage($new,$dest,$src,[System.Drawing.GraphicsUnit]::Pixel)
 } finally { $old.Dispose(); $new.Dispose() }
}
$graphics.Dispose()
$atlasNew.Save((Join-Path $target '0-Tileset.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$atlasOld.Dispose(); $atlasNew.Dispose()
$meta = Get-Content (Join-Path $source '0-Tileset.png.meta') -Raw
$meta = [regex]::Replace($meta,'spritePixelsToUnits: ([\d.]+)',{param($m) 'spritePixelsToUnits: ' + ([double]$m.Groups[1].Value * $scale)})
$meta = $meta -replace 'maxTextureSize: 2048','maxTextureSize: 4096'
$meta = [regex]::Replace($meta,'(?m)^(        (?:x|y|width|height): )([\d.]+)',{param($m) $m.Groups[1].Value + ([double]$m.Groups[2].Value*$scale)})
Set-Content -LiteralPath (Join-Path $target '0-Tileset.png.meta') -Value $meta -NoNewline -Encoding utf8
Write-Output ('Imported '+$manifest.assets.Count+' sprites and rebuilt atlas; original world dimensions retained.')
