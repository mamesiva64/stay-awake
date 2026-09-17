# StayAwake.exe をビルドする。追加ツール不要（Windows 同梱の .NET Framework 4 の csc.exe を使用）
# 実行: powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw "C# compiler not found: $csc" }

# --- exe に埋め込むアイコン (.ico) を生成 -----------------------------------
$icoPath = Join-Path $here 'StayAwake.ico'
$bmp = New-Object System.Drawing.Bitmap 64, 64
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)
$bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 46, 160, 67))
$g.FillEllipse($bg, 0, 0, 63, 63)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 4.5
$g.DrawRectangle($pen, 16, 26, 24, 22)
$g.DrawArc($pen, 37, 30, 16, 14, -90, 180)
$g.DrawLine($pen, 24, 20, 24, 12)
$g.DrawLine($pen, 33, 20, 33, 12)
$pen.Dispose(); $bg.Dispose(); $g.Dispose()

$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()
$ms.Dispose(); $bmp.Dispose()

# ICONDIR + ICONDIRENTRY + PNG (Vista 以降は PNG 埋め込み ico が使える)
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)          # reserved, type=icon, count=1
$bw.Write([byte]64); $bw.Write([byte]64); $bw.Write([byte]0); $bw.Write([byte]0)
$bw.Write([uint16]1); $bw.Write([uint16]32)                               # planes, bit count
$bw.Write([uint32]$png.Length); $bw.Write([uint32]22)                     # size, offset
$bw.Write($png)
$bw.Flush(); $bw.Close(); $fs.Dispose()
Write-Host "icon  : $icoPath"

# --- コンパイル -------------------------------------------------------------
$exe = Join-Path $here 'StayAwake.exe'
$src = Join-Path $here 'StayAwake.cs'
& $csc /nologo /target:winexe /optimize+ /codepage:65001 `
       /win32icon:"$icoPath" /out:"$exe" `
       /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
       "$src" "$(Join-Path $here 'LidSettings.cs')"
if ($LASTEXITCODE -ne 0) { throw "コンパイルに失敗しました (exit $LASTEXITCODE)" }
Write-Host "built : $exe"
