# Rasterize the bundled Rock Salt font with a fixed chalk grain. No generated illustration.
Add-Type -AssemblyName System.Drawing
$projectRoot=Split-Path -Parent $PSScriptRoot
$fontPath=Join-Path $projectRoot 'Assets/Resources/Fonts/ChalkHand.ttf'
$artPath=Join-Path $projectRoot 'Assets/Resources/Art'
$fontCollection=New-Object System.Drawing.Text.PrivateFontCollection
$fontCollection.AddFontFile($fontPath)
$font=New-Object System.Drawing.Font($fontCollection.Families[0],94,[System.Drawing.FontStyle]::Regular,[System.Drawing.GraphicsUnit]::Pixel)
$bitmap=New-Object System.Drawing.Bitmap(1920,220,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics=[System.Drawing.Graphics]::FromImage($bitmap)
$graphics.Clear([System.Drawing.Color]::Transparent)
$graphics.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$format=[System.Drawing.StringFormat]::GenericTypographic
$title='Protocol Aer'
$advances=@()
for($i=0;$i -lt $title.Length;$i++) {
    $letter=$title[$i].ToString()
    $size=$graphics.MeasureString($letter,$font,1000,$format)
    $advances += $(if($letter -eq ' '){46}else{[math]::Ceiling($size.Width)})
    $graphics.DrawString($letter,$font,[System.Drawing.Brushes]::White,[single]($i*160+20),[single]0,$format)
}
$graphics.Dispose()
$rectangle=New-Object System.Drawing.Rectangle(0,0,1920,220)
$data=$bitmap.LockBits($rectangle,[System.Drawing.Imaging.ImageLockMode]::ReadWrite,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bytes=New-Object byte[] ($data.Stride*$data.Height)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0,$bytes,0,$bytes.Length)
$random=New-Object System.Random(173)
for($i=3;$i -lt $bytes.Length;$i+=4) {
    if($bytes[$i] -eq 0){continue}
    $grain=$random.NextDouble()
    if($grain -lt .11){$bytes[$i]=0}
    elseif($grain -lt .38){$bytes[$i]=[byte]($bytes[$i]*.5)}
}
[System.Runtime.InteropServices.Marshal]::Copy($bytes,0,$data.Scan0,$bytes.Length)
$bitmap.UnlockBits($data)
$bitmap.Save((Join-Path $artPath 'TitleChalk.png'),[System.Drawing.Imaging.ImageFormat]::Png)
@{Advances=$advances}|ConvertTo-Json -Compress|Set-Content (Join-Path $artPath 'TitleChalkMetrics.json') -Encoding utf8
$bitmap.Dispose();$font.Dispose();$fontCollection.Dispose()
