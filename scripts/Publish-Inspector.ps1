param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root "JabInspector.App\JabInspector.App.csproj"
$artifactsRoot = Join-Path $root "artifacts"
$publishRoot = Join-Path $artifactsRoot "publish"
$packageRoot = Join-Path $artifactsRoot "JavaAccessBridgeInspector-$Runtime"

if (Test-Path $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

if (Test-Path $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

Write-Host "Publishing $appProject for $Runtime ($Configuration)..."

dotnet publish $appProject `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:PublishTrimmed=false `
    -o $publishRoot

New-Item -ItemType Directory -Path $packageRoot | Out-Null
Copy-Item -Path (Join-Path $publishRoot "*") -Destination $packageRoot -Recurse
Copy-Item -LiteralPath (Join-Path $root "README.md") -Destination (Join-Path $packageRoot "README.md")
Copy-Item -LiteralPath (Join-Path $root "DISTRIBUTION.md") -Destination (Join-Path $packageRoot "DISTRIBUTION.md")

$launcherPath = Join-Path $packageRoot "Launch Inspector.cmd"
$launcherContent = @"
@echo off
setlocal
cd /d "%~dp0"
start "" "JavaAccessBridgeInspector.exe"
"@
Set-Content -LiteralPath $launcherPath -Value $launcherContent -Encoding ASCII

Write-Host ""
Write-Host "Package created:"
Write-Host "  $packageRoot"
Write-Host ""
Write-Host "Distribute that folder to end users."
