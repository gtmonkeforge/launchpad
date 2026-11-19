param(
    [ValidateSet("all", "windows", "linux")][string]$Target = "all",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "src\Launchpad\Launchpad.csproj"
$staging = Join-Path $root "obj\package"
$dist = Join-Path $root "dist"
$version = ([xml](Get-Content (Join-Path $root "Directory.Build.props"))).Project.PropertyGroup.Version

function Invoke-Checked([string]$File, [string[]]$Arguments) {
    & $File @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$File $($Arguments -join ' ') failed with exit code $LASTEXITCODE" }
}

function Publish-Launchpad([string]$Runtime) {
    $output = Join-Path $staging $Runtime
    if (Test-Path $output) { Remove-Item -Recurse -Force $output }
    Invoke-Checked dotnet @("publish", $project, "-c", $Configuration, "-r", $Runtime, "--self-contained", "-o", $output, "-p:DebugType=None", "-p:DebugSymbols=false", "-nologo")
    return $output
}

function ConvertTo-Rtf([string]$Markdown) {
    $body = foreach ($line in ($Markdown -split "\r?\n")) {
        $text = $line.Replace("\", "\\").Replace("{", "\{").Replace("}", "\}")
        $text = [regex]::Replace($text, "[^\x00-\x7F]", { param($m) "\u$([int][char]$m.Value)?" })
        $text = [regex]::Replace($text, "\*\*(.+?)\*\*", "{\b `$1}")
        if ($text -match "^#+\s*(.*)$") { "{\b\fs20 $($Matches[1])}\par" }
        elseif ($text -match "^\s*[-*]\s+(.*)$") { "\bullet  $($Matches[1])\par" }
        elseif ($text.Trim() -eq "") { "\par" }
        else { "$text\par" }
    }
    return "{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\f0\fs18`n$($body -join "`n")`n}"
}

New-Item -ItemType Directory -Force $staging, $dist | Out-Null

if ($Target -in "all", "windows") {
    $publish = Publish-Launchpad "win-x64"
    $rtf = Join-Path $staging "License.rtf"
    Set-Content -Path $rtf -Value (ConvertTo-Rtf (Get-Content -Raw (Join-Path $root "LICENSE.md"))) -Encoding ascii
    Invoke-Checked dotnet @("build", (Join-Path $root "packaging\windows\Launchpad.Installer.wixproj"), "-c", $Configuration, "-p:PublishDir=$publish\", "-p:LicenseRtf=$rtf", "-o", $dist, "-nologo")
    Get-ChildItem $dist -Filter "*.wixpdb" | Remove-Item -Force
    Write-Host "Built $(Join-Path $dist "launchpad-$version-windows-x64.msi")"
}

if ($Target -in "all", "linux") {
    $publish = Publish-Launchpad "linux-x64"
    Copy-Item (Join-Path $root "packaging\linux\install.sh"), (Join-Path $root "packaging\linux\uninstall.sh"), (Join-Path $root "packaging\linux\README.txt"), (Join-Path $root "LICENSE.md") $publish
    Copy-Item (Join-Path $root "src\Launchpad\wwwroot\site\static\logo.svg") (Join-Path $publish "monkeforge-launchpad.svg")
    $archive = Join-Path $dist "launchpad-$version-linux-x64.tar.gz"
    Invoke-Checked dotnet @("run", (Join-Path $root "scripts\tarball.cs"), "--", $publish, $archive, "monkeforge-launchpad-$version")
}
