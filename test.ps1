$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$output = Join-Path $here 'Tests.exe'
& $csc /nologo /target:exe /main:Tests /codepage:65001 /out:$output /win32icon:"$(Join-Path $here 'StayAwake.ico')" `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    (Join-Path $here 'StayAwake.cs') (Join-Path $here 'LidSettings.cs') (Join-Path $here 'Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
