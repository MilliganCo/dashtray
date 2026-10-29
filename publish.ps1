$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "DashTray.csproj"
$output = Join-Path $PSScriptRoot "publish"

New-Item -ItemType Directory -Path $output -Force | Out-Null

Write-Host "Publishing DashTray as a self-contained Windows x64 executable..."
dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false `
    -o $output

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Write-Host ""
Write-Host "Ready: $output\DashTray.exe"
