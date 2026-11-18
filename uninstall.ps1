# uninstall.ps1 - remove Presence (run via uninstall.cmd). No admin required.

$ErrorActionPreference = "SilentlyContinue"

# Stop it if running.
Get-Process Presence -ErrorAction SilentlyContinue | Stop-Process -Force

# Remove shortcuts.
Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) "Presence.lnk") -Force
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) "Presence.lnk") -Force

# Remove the startup Run entry if the app created one.
try {
    $rk = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    if (Get-ItemProperty -Path $rk -Name "Presence" -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $rk -Name "Presence"
    }
} catch {}

# Remove the installed folder.
Remove-Item (Join-Path $env:LOCALAPPDATA "Presence") -Recurse -Force

# Leave %APPDATA%\Presence (config) alone unless you want a full wipe:
# Remove-Item (Join-Path $env:APPDATA "Presence") -Recurse -Force

Write-Host "Presence removed."
