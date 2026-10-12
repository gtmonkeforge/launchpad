param([ValidateSet('all', 'win64', 'tux64')][string]$Target = 'all')
$ErrorActionPreference = 'Stop'
$launchpadBunCommand = Get-Command bun -ErrorAction SilentlyContinue
$launchpadBun = if ($launchpadBunCommand) { $launchpadBunCommand.Source } else { Join-Path $env:USERPROFILE '.bun/bin/bun.exe' }
if (-not (Test-Path -LiteralPath $launchpadBun)) { throw 'Install Bun 1.4.3 or newer before building Launchpad.' }
Push-Location -LiteralPath $PSScriptRoot
try {
    if ($Target -eq 'all') { & $launchpadBun run build } else { & $launchpadBun run "build:$Target" }
    if ($LASTEXITCODE -ne 0) { throw "Launchpad build failed with exit code $LASTEXITCODE." }
} finally { Pop-Location }
