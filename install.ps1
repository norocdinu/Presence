# install.ps1 - one-click install for Presence (run via install.cmd).
# Unblocks the exe (clears the "downloaded from internet" mark that triggers
# SmartScreen), copies it to your user folder, makes Desktop + Start Menu
# shortcuts, and launches it. No admin required. Per-user only.

$ErrorActionPreference = "Stop"
$here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }

$srcExe = Join-Path $here "Presence.exe"
if (-not (Test-Path $srcExe)) { throw "Presence.exe not found next to this script." }

# 1) Clear mark-of-the-web so Windows stops warning about a downloaded file.
Get-ChildItem $here -Filter *.exe | ForEach-Object { Unblock-File $_.FullName }

# 2) Install into the user's local app data (no admin needed).
$dest = Join-Path $env:LOCALAPPDATA "Presence"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item $srcExe (Join-Path $dest "Presence.exe") -Force
$destExe = Join-Path $dest "Presence.exe"
Unblock-File $destExe

# 3) Shortcuts (Desktop + Start Menu).
$ws = New-Object -ComObject WScript.Shell
function New-Shortcut($path) {
    $s = $ws.CreateShortcut($path)
    $s.TargetPath = $destExe
    $s.WorkingDirectory = $dest
    $s.Description = "Keep PC awake / active"
    $s.Save()
}
New-Shortcut (Join-Path ([Environment]::GetFolderPath('Desktop')) "Presence.lnk")
$startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) "Presence.lnk"
New-Shortcut $startMenu

# 4) Launch it.
Start-Process $destExe

Write-Host ""
Write-Host "Presence is installed and running (look for the tray icon near the clock)."
Write-Host "Installed to: $dest"
Write-Host "Shortcuts added to Desktop and Start Menu."
Write-Host "Tip: right-click the tray icon to set 'Launch at startup' and an auto-stop time."
Write-Host "To remove it later, run uninstall.cmd."
