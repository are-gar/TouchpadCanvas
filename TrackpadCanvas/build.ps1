# Builds bin\TrackpadCanvas.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
# Pass 1 builds a temporary exe that renders the icon to obj\app.ico; pass 2 embeds it in the final exe.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc" }
$bin = Join-Path $root 'bin'
$obj = Join-Path $root 'obj'
New-Item -ItemType Directory -Force $bin, $obj | Out-Null
$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$common = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+',
    '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Core.dll')

& $csc @common /out:"$obj\IconWriter.exe" @sources
if ($LASTEXITCODE -ne 0) { throw "Build failed (pass 1)" }
$p = Start-Process "$obj\IconWriter.exe" -ArgumentList '--write-icon', "`"$obj\app.ico`"" -Wait -PassThru
if ($p.ExitCode -ne 0 -or -not (Test-Path "$obj\app.ico")) { throw "Icon generation failed" }

& $csc @common /win32icon:"$obj\app.ico" /out:"$bin\TrackpadCanvas.exe" @sources
if ($LASTEXITCODE -ne 0) { throw "Build failed (pass 2)" }
Write-Host "Built $bin\TrackpadCanvas.exe"
