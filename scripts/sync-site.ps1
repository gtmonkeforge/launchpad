param([string]$OpenForge = (Join-Path $PSScriptRoot "..\..\openforge"))

$source = Join-Path $OpenForge "src\wwwroot"
$target = Join-Path $PSScriptRoot "..\src\Launchpad\wwwroot\site"

if (-not (Test-Path (Join-Path $source "index.html"))) { throw "openforge's wwwroot wasn't found at $source" }

if (Test-Path $target) { Remove-Item -Recurse -Force $target }
New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item (Join-Path $source "index.html") $target
Copy-Item -Recurse (Join-Path $source "static") (Join-Path $target "static")

Remove-Item -Recurse -Force (Join-Path $target "static\js\admin"), (Join-Path $target "static\css\admin.css"), (Join-Path $target "static\js\setup.js"), (Join-Path $target "static\world-map.json"), (Join-Path $target "static\world-map.LICENSE.txt") -ErrorAction SilentlyContinue

$commit = git -C $OpenForge rev-parse --short HEAD 2>$null
Set-Content -Path (Join-Path $target "SOURCE.txt") -Value "Copied from openforge $commit by scripts/sync-site.ps1. Don't edit; change openforge and sync again." -Encoding utf8
Write-Host "Synced site from openforge $commit"
