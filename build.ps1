# Build OpenCodeUsage.exe with the in-box .NET Framework compiler (no SDK needed).
param(
    [switch]$ForceIcon,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $root 'src'
$distDir = Join-Path $root 'dist'
$icon = Join-Path $root 'app.ico'
$manifest = Join-Path $root 'app.manifest'
$exe = Join-Path $distDir 'OpenCodeUsage.exe'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) { throw "csc.exe (.NET Framework 4.x) tidak ditemukan." }

if ($ForceIcon -or -not (Test-Path $icon)) {
    & (Join-Path $root 'tools\make-icon.ps1') -Output $icon
}

if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Force -Path $distDir | Out-Null }
if ($Clean) {
    Get-ChildItem $distDir -Filter *.exe -ErrorAction SilentlyContinue | Remove-Item -Force
}

$sources = @(Get-ChildItem $srcDir -Filter *.cs | Sort-Object Name | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw "Tidak ada sumber .cs di $srcDir" }

$compilerArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/langversion:5',
    '/warn:4',
    ('/out:' + $exe),
    ('/win32icon:' + $icon),
    ('/win32manifest:' + $manifest),
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.Net.Http.dll',
    '/r:System.Security.dll'
) + $sources

Write-Host ("kompilasi {0} file -> {1}" -f $sources.Count, $exe)
& $csc $compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Kompilasi gagal (exit code $LASTEXITCODE)." }

$info = Get-Item $exe
Write-Host ("OK  {0}  ({1:N0} bytes)  {2}" -f $info.Name, $info.Length, $info.LastWriteTime)
