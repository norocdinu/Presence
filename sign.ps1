# sign.ps1 - sign Presence.exe with a self-signed code-signing certificate.
#
# Run this on the BUILD machine after building (the one that holds the private
# key). It creates the signing cert on first run, reuses it afterward, signs
# the exe with a trusted timestamp, and exports the PUBLIC cert for trusting on
# other machines (see trust-cert.ps1).
#
# Self-signed signing does NOT give public reputation. It lets machines that
# explicitly trust this certificate (via trust-cert.ps1) run the exe without an
# "unknown publisher" warning, and lets you whitelist it by publisher.

$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $dir "Presence.exe"
if (-not (Test-Path $exe)) { throw "Presence.exe not found - build it first (build.cmd)." }

$subject = "CN=Presence Self-Signed Code Signing"
$cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $subject -and $_.HasPrivateKey } |
        Select-Object -First 1

if (-not $cert) {
    Write-Host "Creating self-signed code-signing certificate (valid 5 years)..."
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject `
        -CertStoreLocation Cert:\CurrentUser\My -KeyUsage DigitalSignature `
        -KeyExportPolicy Exportable -KeyAlgorithm RSA -KeyLength 2048 `
        -NotAfter (Get-Date).AddYears(5)
} else {
    Write-Host "Reusing existing signing certificate."
}
Write-Host "Thumbprint: $($cert.Thumbprint)"

# Timestamp so the signature stays valid after the certificate expires.
$ts = "http://timestamp.digicert.com"
$sig = Set-AuthenticodeSignature -FilePath $exe -Certificate $cert `
    -HashAlgorithm SHA256 -TimestampServer $ts
Write-Host "Signature status: $($sig.Status)   ($($sig.StatusMessage))"
Write-Host "Note: status 'UnknownError / untrusted root' is expected until the"
Write-Host "certificate is trusted on this machine (run trust-cert.ps1 as admin)."

# Export the public certificate (no private key) for distribution.
$cer = Join-Path $dir "PresenceCodeSign.cer"
Export-Certificate -Cert $cert -FilePath $cer -Type CERT | Out-Null
Write-Host "Public certificate exported -> $cer"
