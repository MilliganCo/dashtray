$ErrorActionPreference = "Stop"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 8 SDK не найден. Установите .NET 8 SDK и повторите запуск install.ps1."
}

& (Join-Path $PSScriptRoot "publish.ps1")

$sourceExe = Join-Path $PSScriptRoot "publish\DashTray.exe"
$installDirectory = Join-Path $env:LOCALAPPDATA "Programs\DashTray"
$targetExe = Join-Path $installDirectory "DashTray.exe"

Get-Process DashTray -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
Copy-Item $sourceExe $targetExe -Force
Start-Process $targetExe

Write-Host "DashTray установлен: $targetExe"
