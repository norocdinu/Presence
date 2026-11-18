# trust-cert.ps1 - trust the Presence self-signed certificate on THIS machine.
#
# Run this ONCE per machine that should run the signed Presence.exe without an
# "unknown publisher" prompt. Requires an elevated (Administrator) PowerShell.
#
# What it does: imports PresenceCodeSign.cer into the LocalMachine
# Trusted Root and Trusted Publishers stores. After this, Windows treats
# Presence.exe as signed by a trusted publisher.
#
# SECURITY NOTE: trusting a root certificate means this machine will trust
# ANYTHING signed by that certificate's private key. Only the person who
# created the cert holds that key. Only run this for a certificate you control
# and on machines you own. To undo, see the bottom of this file.

#Requires -RunAsAdministrator
$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$cer = Join-Path $dir "PresenceCodeSign.cer"
if (-not (Test-Path $cer)) { throw "PresenceCodeSign.cer not found next to this script." }

Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null
Write-Host "Trusted. Presence.exe should now run without an unknown-publisher prompt."
Write-Host ""
Write-Host "To UNDO later, remove the cert from both stores, e.g.:"
Write-Host '  Get-ChildItem Cert:\LocalMachine\Root, Cert:\LocalMachine\TrustedPublisher |'
Write-Host '    Where-Object Subject -eq "CN=Presence Self-Signed Code Signing" | Remove-Item'
