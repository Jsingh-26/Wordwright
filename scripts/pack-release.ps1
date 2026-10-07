# Builds the Velopack installer for a release (docs/PLAN.md P3.5, P10.3).
# Produces Releases/v<version>/: Setup.exe, the portable zip, the full package
# and the feed files, and nothing else.
#
# Each version is packed into its own empty folder with no delta package
# (docs/PLAN.md P12.16): the app never checks for updates, so a delta helps no
# one, and an empty folder keeps unpublished local builds out of the feed.
#
#   scripts/pack-release.ps1                 (1.0.1)
#   scripts/pack-release.ps1 -Version 1.0.1
#
# Publishing is a separate, deliberate step:
#   vpk upload github --repoUrl https://github.com/Jsingh-26/Wordwright `
#       --token <token> --publish --releaseName "v0.1.0" --tag v0.1.0
param(
    [string]$Version = "1.0.1"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root "publish"
$releases = Join-Path $root "Releases/v$Version"

if (Test-Path $releases) { Remove-Item $releases -Recurse -Force }
New-Item -ItemType Directory -Path $releases | Out-Null

# The version the app reports on its About page comes from the assembly, so the
# build and the package are given the same one.
# Start from an empty folder: dotnet publish never deletes, so files from older
# builds (the parked AI feature's 300 MB of native libraries) would ship too.
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish (Join-Path $root "src/Wordwright.App") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# The installer splash is the hero illustration (docs/PLAN.md P10.0), rendered
# from the app's own Resources/Hero.xaml.
$splash = Join-Path ([System.IO.Path]::GetTempPath()) "wordwright-splash.png"
dotnet run --project (Join-Path $root "scripts/IconGen") -- --hero $splash
if ($LASTEXITCODE -ne 0) { throw "rendering the splash failed" }

vpk pack `
    --packId Wordwright `
    --packVersion $Version `
    --packDir $publish `
    --mainExe Wordwright.App.exe `
    --runtime win-x64 `
    --packTitle "Wordwright" `
    --packAuthors "Unbound Kite" `
    --icon (Join-Path $root "brand/icon.ico") `
    --splashImage $splash `
    --delta None `
    --outputDir $releases
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed" }

Get-ChildItem $releases | Select-Object Name, @{n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
