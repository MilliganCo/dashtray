param(
    [switch]$RemoveSettings
)

$ErrorActionPreference = "Stop"

Get-Process DashTray -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-ItemProperty `
    -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" `
    -Name "DashTray" `
    -ErrorAction SilentlyContinue

$installDirectory = Join-Path $env:LOCALAPPDATA "Programs\DashTray"
Remove-Item $installDirectory -Recurse -Force -ErrorAction SilentlyContinue

if ($RemoveSettings) {
    Remove-Item (Join-Path $env:LOCALAPPDATA "DashTray") -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "DashTray удалён."
