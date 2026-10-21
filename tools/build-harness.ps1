# Builds dist\UiHarness.exe (dev-only screenshot harness for the app windows).
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$srcDir = Join-Path $root 'src'
$distDir = Join-Path $root 'dist'
$exe = Join-Path $distDir 'UiHarness.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Force -Path $distDir | Out-Null }

$sources = @(Get-ChildItem $srcDir -Filter *.cs | ForEach-Object { $_.FullName })
$sources += (Join-Path $PSScriptRoot 'ui-harness.cs')

$compilerArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/langversion:5',
    '/main:OpenCodeUsage.UiHarness',
    ('/out:' + $exe),
    ('/win32manifest:' + (Join-Path $root 'app.manifest')),
    ('/win32icon:' + (Join-Path $root 'app.ico')),
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.Net.Http.dll',
    '/r:System.Security.dll'
) + $sources

& $csc $compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Kompilasi harness gagal." }
Write-Host ("OK  {0}" -f $exe)
