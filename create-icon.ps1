Add-Type -AssemblyName System.Drawing

$bmp = New-Object System.Drawing.Bitmap(32, 32)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

$blue = [System.Drawing.Color]::FromArgb(255, 99, 179, 237)
$brush = New-Object System.Drawing.SolidBrush($blue)
$pen = New-Object System.Drawing.Pen($blue, 2.5)
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

# Mic body
$g.FillEllipse($brush, 10, 2, 12, 12)
$g.FillRectangle($brush, 10, 8, 12, 10)
$g.FillEllipse($brush, 10, 12, 12, 12)

# Stand arc
$g.DrawArc($pen, 6, 12, 20, 14, 0, 180)

# Stem
$g.DrawLine($pen, 16, 26, 16, 29)

# Base
$g.DrawLine($pen, 10, 29, 22, 29)

$g.Dispose()

# Save as PNG to memory
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$pngData = $ms.ToArray()
$ms.Dispose()
$bmp.Dispose()

# Write ICO file with PNG data
$icoPath = 'C:\devops\WinAIrecorder\VoiceType\Resources\mic-icon.ico'
$icoStream = New-Object System.IO.FileStream($icoPath, [System.IO.FileMode]::Create)
$writer = New-Object System.IO.BinaryWriter($icoStream)

# ICO header: reserved=0, type=1, count=1
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]1)

# Directory entry
$writer.Write([byte]32)
$writer.Write([byte]32)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([uint16]1)
$writer.Write([uint16]32)
$writer.Write([uint32]$pngData.Length)
$writer.Write([uint32]22)

# PNG data
$writer.Write($pngData)
$writer.Close()
$icoStream.Dispose()

Write-Host "Created mic-icon.ico ($($pngData.Length + 22) bytes)"
