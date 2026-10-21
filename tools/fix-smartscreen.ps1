# Membersihkan penanda unduhan (Mark of the Web) dan/atau menandatangani
# OpenCodeUsage.exe supaya tidak lagi muncul "Windows protected your PC".
#
# Contoh:
#   .\tools\fix-smartscreen.ps1
#   .\tools\fix-smartscreen.ps1 -Sign
#   .\tools\fix-smartscreen.ps1 -Path 'D:\app\OpenCodeUsage.exe' -Sign
param(
    [string[]]$Path = @(),
    [switch]$Sign,
    [string]$Subject = 'CN=Hadi Permana'
)

$ErrorActionPreference = 'Stop'

function Find-Targets {
    $targets = @()

    if ($Path.Count -gt 0) {
        foreach ($item in $Path) {
            if (Test-Path -LiteralPath $item) {
                $resolved = (Resolve-Path -LiteralPath $item).Path
                if (Test-Path -LiteralPath $resolved -PathType Container) {
                    $targets += Get-ChildItem -LiteralPath $resolved -Recurse -Filter 'OpenCodeUsage*.exe' -ErrorAction SilentlyContinue |
                        Select-Object -ExpandProperty FullName
                }
                else {
                    $targets += $resolved
                }
            }
            else {
                Write-Warning "Tidak ditemukan: $item"
            }
        }
    }
    else {
        $dirs = @(
            (Join-Path $env:USERPROFILE 'Downloads'),
            (Join-Path $env:USERPROFILE 'Desktop'),
            (Join-Path $env:USERPROFILE 'Documents'),
            $env:TEMP,
            (Join-Path (Split-Path -Parent $PSScriptRoot) 'dist')
        )
        foreach ($dir in $dirs) {
            if (-not (Test-Path -LiteralPath $dir)) { continue }
            $targets += Get-ChildItem -LiteralPath $dir -Recurse -Depth 2 -Filter 'OpenCodeUsage*.exe' -ErrorAction SilentlyContinue |
                Select-Object -ExpandProperty FullName
        }
    }

    return @($targets | Sort-Object -Unique)
}

$targets = Find-Targets
if ($targets.Count -eq 0) {
    Write-Host "Tidak ada OpenCodeUsage*.exe yang ditemukan di folder umum (Downloads/Desktop/Documents/Temp/dist)."
    Write-Host "Jalankan ulang dengan lokasi file, misalnya:"
    Write-Host "  .\tools\fix-smartscreen.ps1 -Path 'C:\folder\OpenCodeUsage.exe' -Sign"
    exit 1
}

foreach ($file in $targets) {
    Write-Host ""
    Write-Host ("== {0}" -f $file)

    $mark = Get-Item -LiteralPath $file -Stream * -ErrorAction SilentlyContinue |
        Where-Object { $_.Stream -eq 'Zone.Identifier' }
    if ($mark) {
        Unblock-File -LiteralPath $file
        $left = Get-Item -LiteralPath $file -Stream * -ErrorAction SilentlyContinue |
            Where-Object { $_.Stream -eq 'Zone.Identifier' }
        Write-Host ("   penanda unduhan: {0}" -f $(if ($left) { 'GAGAL dihapus' } else { 'dihapus' }))
    }
    else {
        Write-Host "   penanda unduhan: tidak ada (file ini seharusnya tidak memicu SmartScreen)"
    }

    if ($Sign) {
        $existing = Get-AuthenticodeSignature -LiteralPath $file
        if ($existing.Status -eq 'Valid' -and $existing.SignerCertificate) {
            Write-Host ("   tanda tangan   : sudah valid ({0})" -f $existing.SignerCertificate.GetNameInfo('SimpleName', $false))
        }
        else {
            & (Join-Path $PSScriptRoot 'sign-app.ps1') -Exe $file -Subject $Subject -Trust
        }
    }
    else {
        $sig = Get-AuthenticodeSignature -LiteralPath $file
        Write-Host ("   tanda tangan   : {0}" -f $sig.Status)
    }
}

Write-Host ""
Write-Host "Selesai. Jalankan aplikasinya lewat shortcut/file yang sudah diperbaiki."
Write-Host "Kalau masih muncul peringatan: klik 'More info' -> 'Run anyway', atau jalankan ulang dengan -Sign."
