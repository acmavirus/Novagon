Add-Type -AssemblyName System.Drawing

$srcPath = "src/OpenLaragon.UI/Assets/novagon.png"
$bmp = [System.Drawing.Bitmap]::FromFile($srcPath)
$width = $bmp.Width
$height = $bmp.Height

$outBmp = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

# Flood fill transparency from corners
for ($y = 0; $y -lt $height; $y++) {
    for ($x = 0; $x -lt $width; $x++) {
        $c = $bmp.GetPixel($x, $y)
        
        # Nếu là nền trắng / xám nhạt bên ngoài squircle
        if ($c.R -gt 240 -and $c.G -gt 240 -and $c.B -gt 240) {
            $outBmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
        }
        elseif ($c.R -gt 200 -and $c.G -gt 200 -and $c.B -gt 200) {
            # Vùng bóng mờ chuyển tiếp (anti-aliasing)
            $alpha = [int](255 - (($c.R + $c.G + $c.B) / 3 - 200) * (255 / 40))
            if ($alpha -lt 0) { $alpha = 0 }
            if ($alpha -gt 255) { $alpha = 255 }
            $outBmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, 15, 23, 42))
        }
        else {
            $outBmp.SetPixel($x, $y, $c)
        }
    }
}

$bmp.Dispose()
$outBmp.Save("src/OpenLaragon.UI/Assets/novagon_transparent.png", [System.Drawing.Imaging.ImageFormat]::Png)

# Tạo app.ico chuẩn 32x32 trong suốt từ ảnh mới
$icoBmp = New-Object System.Drawing.Bitmap 32, 32, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($icoBmp)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.DrawImage($outBmp, 0, 0, 32, 32)
$g.Dispose()

$iconHandle = $icoBmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($iconHandle)
$fs = New-Object System.IO.FileStream "src/OpenLaragon.UI/Assets/app.ico", ([System.IO.FileMode]::Create)
$icon.Save($fs)
$fs.Close()

$outBmp.Dispose()
$icoBmp.Dispose()

# Ghi đè lại novagon.png bằng bản trong suốt
Move-Item "src/OpenLaragon.UI/Assets/novagon_transparent.png" "src/OpenLaragon.UI/Assets/novagon.png" -Force

Write-Host "Đã xử lý độ trong suốt cho Icon thành công!"
