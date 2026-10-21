# Menandatangani OpenCodeUsage.exe dengan sertifikat code-signing.
#
# Dua mode:
#   1. Self-signed (gratis, untuk PC sendiri):  .\tools\sign-app.ps1 -Trust
#   2. Sertifikat berbayar (PFX):               .\tools\sign-app.ps1 -PfxPath .\cert.pfx -PfxPassword ***
#
# SmartScreen tetap punya reputasi sendiri; yang dijamin script ini adalah
# tanda tangan Authenticode yang valid + nama publisher yang muncul di Windows.
param(
    [string]$Exe = (Join-Path (Split-Path -Parent $PSScriptRoot) 'dist\OpenCodeUsage.exe'),
    [string]$Subject = 'CN=Hadi Permana',
    [string]$PfxPath,
    [string]$PfxPassword,
    [switch]$Trust,
    [string]$TimestampServer = ''
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Exe)) { throw "File tidak ditemukan: $Exe" }
$Exe = (Resolve-Path $Exe).Path

Write-Host ("target : {0}" -f $Exe)
Write-Host ("sha256 sebelum: {0}" -f (Get-FileHash $Exe -Algorithm SHA256).Hash)

$certificate = $null

if ($PfxPath) {
    if (-not (Test-Path $PfxPath)) { throw "PFX tidak ditemukan: $PfxPath" }
    $secure = ConvertTo-SecureString -String $PfxPassword -AsPlainText -Force
    $certificate = Get-PfxCertificate -FilePath $PfxPath -Password $secure
    if (-not $certificate) { throw "PFX tidak bisa dibaca." }
    Write-Host ("sertifikat: PFX {0}" -f $certificate.Subject)
}
else {
    $codeSigningOid = '1.3.6.1.5.5.7.3.3'
    $certificate = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -eq $Subject -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if ($certificate) {
        Write-Host ("sertifikat: pakai yang sudah ada ({0})" -f $certificate.Thumbprint)
    }
    else {
        Write-Host ("sertifikat: membuat self-signed baru untuk '{0}'" -f $Subject)
        $certificate = New-SelfSignedCertificate `
            -Type CodeSigningCert `
            -Subject $Subject `
            -KeyUsage DigitalSignature `
            -KeyAlgorithm RSA `
            -KeyLength 3072 `
            -HashAlgorithm SHA256 `
            -CertStoreLocation 'Cert:\CurrentUser\My' `
            -NotAfter (Get-Date).AddYears(3) `
            -FriendlyName 'OpenCode Usage (self-signed)' `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', "2.5.29.19={text}")
        Write-Host ("sertifikat: {0}" -f $certificate.Thumbprint)
    }

    if ($Trust) {
        foreach ($storeName in 'Root', 'TrustedPublisher') {
            $store = New-Object System.Security.Cryptography.X509Certificates.X509Store($storeName, 'CurrentUser')
            $store.Open('ReadWrite')
            $already = $store.Certificates | Where-Object { $_.Thumbprint -eq $certificate.Thumbprint }
            if (-not $already) {
                $store.Add($certificate)
                Write-Host ("trust  : ditambahkan ke CurrentUser\{0}" -f $storeName)
            }
            else {
                Write-Host ("trust  : sudah ada di CurrentUser\{0}" -f $storeName)
            }
            $store.Close()
        }
    }
    else {
        Write-Host "trust  : dilewati (pakai -Trust agar Windows mempercayai sertifikat ini di akun kamu)"
    }
}

$signArgs = @{
    FilePath    = $Exe
    Certificate = $certificate
    HashAlgorithm = 'SHA256'
}
if ($TimestampServer) { $signArgs['TimestampServer'] = $TimestampServer }

Set-AuthenticodeSignature @signArgs | Out-Null

$sig = Get-AuthenticodeSignature $Exe
Write-Host ""
Write-Host ("status  : {0}" -f $sig.Status)
Write-Host ("publisher: {0}" -f $(if ($sig.SignerCertificate) { $sig.SignerCertificate.GetNameInfo('SimpleName', $false) } else { '(tidak ada)' }))
Write-Host ("subject : {0}" -f $(if ($sig.SignerCertificate) { $sig.SignerCertificate.Subject } else { '-' }))
Write-Host ("sha256 sesudah: {0}" -f (Get-FileHash $Exe -Algorithm SHA256).Hash)
Write-Host ""
Write-Host "Catatan: kalau status masih 'UnknownError'/'NotTrusted', jalankan lagi dengan -Trust"
Write-Host "         atau impor sertifikatnya ke Trusted Root + Trusted Publisher di akun ini."
