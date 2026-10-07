# Builds the Microsoft Store package (docs/PLAN.md P3.6).
#
#   scripts/pack-msix.ps1                          unsigned .msix and .msixbundle
#   scripts/pack-msix.ps1 -Certificate dev.pfx -CertificatePassword pass
#
# makeappx.exe comes with the Windows SDK (or the Microsoft.Windows.SDK.BuildTools
# NuGet package); set MAKEAPPX or pass -MakeAppx if it lives somewhere else.
# The Store signs what you upload, so signing is only needed to install the
# package on this machine for a hand check.
param(
    [string]$Version = "1.0.1",
    [string]$MakeAppx,
    [string]$Certificate,
    [string]$CertificatePassword
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$stage = Join-Path $root "packaging/stage"
$publish = Join-Path $root "publish"
$output = Join-Path $root "Releases"
$packageName = "Wordwright-$Version-x64.msix"

function Find-MakeAppx {
    if ($MakeAppx) { return $MakeAppx }
    if ($env:MAKEAPPX) { return $env:MAKEAPPX }

    $candidates = @()
    $candidates += Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue
    # The BuildTools package drops the same tool into the NuGet cache.
    $candidates += Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools\*\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue

    $found = $candidates | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $found) {
        throw "makeappx.exe was not found. Install the Windows SDK's packaging tools, " +
              "add the Microsoft.Windows.SDK.BuildTools package, or pass -MakeAppx <path>."
    }

    return $found.FullName
}

$makeappx = Find-MakeAppx

# The tiles come from the brand geometry, so they are regenerated rather than
# kept in the repository (docs/AGENTS.md rule 7).
dotnet run --project (Join-Path $root "scripts/IconGen") -- --msix (Join-Path $root "packaging/Assets")
if ($LASTEXITCODE -ne 0) { throw "rendering the tiles failed" }

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

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item "$publish/*" $stage -Recurse

# The Identity Version is what the Store checks for uniqueness, so it has to
# follow -Version or every later upload is refused as a duplicate. The file in
# the repository stays a template, and only the staged copy is stamped.
# MSIX identity versions have four parts; -Version is usually given as three.
$identityVersion = (@($Version.Split('.') + '0', '0', '0', '0') | Select-Object -First 4) -join '.'
$manifestPath = Join-Path $stage "AppxManifest.xml"
Copy-Item (Join-Path $root "packaging/AppxManifest.xml") $manifestPath -Force

$manifest = [System.IO.File]::ReadAllText($manifestPath)
$manifest = [regex]::Replace(
    $manifest,
    '(<Identity\b[^>]*?Version=")[^"]*(")',
    '${1}' + $identityVersion + '${2}')

if ($manifest -notmatch ('Version="' + [regex]::Escape($identityVersion) + '"')) {
    throw "could not stamp version $identityVersion into the manifest"
}

# Written without a byte-order mark: makeappx accepts it, and it keeps the
# staged file's first bytes predictable.
[System.IO.File]::WriteAllText($manifestPath, $manifest, (New-Object System.Text.UTF8Encoding($false)))

Write-Output "Packaging version $Version (manifest identity $identityVersion)"

Copy-Item (Join-Path $root "packaging/Assets") (Join-Path $stage "Assets") -Recurse
New-Item -ItemType Directory -Path $output -Force | Out-Null

# Windows picks scale-NNN and targetsize-NN_altform-unplated assets through the
# package's resources.pri; without one it only ever uses the unqualified file,
# so the taskbar would show the tile on a plate instead of the mark.
$makepri = Join-Path (Split-Path -Parent $makeappx) "makepri.exe"
$priConfig = Join-Path $root "packaging/stage-priconfig.xml"
& $makepri createconfig /cf $priConfig /dq en-US /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makepri createconfig failed" }
# The default config splits each scale into its own resources.scale-NNN.pri for
# separate resource packs; this package is a single one, so keep one index.
[xml]$config = Get-Content $priConfig
$packagingNode = $config.SelectSingleNode("//packaging")
if ($packagingNode) { [void]$packagingNode.ParentNode.RemoveChild($packagingNode) }
$config.Save($priConfig)
& $makepri new /pr $stage /cf $priConfig /mn $manifestPath /of (Join-Path $stage "resources.pri") /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makepri new failed" }
Remove-Item $priConfig


& $makeappx pack /o /d $stage /p (Join-Path $output $packageName)
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed" }

# A bundle is what the Store expects when more than one architecture is offered;
# with x64 alone it still uploads fine and keeps the door open for arm64 later.
# makeappx bundles a directory of packages, so the .msix gets one of its own —
# Releases/ also holds the Velopack installer's files.
$bundleStage = Join-Path $root "packaging/stage-bundle"
if (Test-Path $bundleStage) { Remove-Item $bundleStage -Recurse -Force }
New-Item -ItemType Directory -Path $bundleStage | Out-Null
Copy-Item (Join-Path $output $packageName) $bundleStage

& $makeappx bundle /o /d $bundleStage /p (Join-Path $output "Wordwright-$Version.msixbundle")
if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed" }

if ($Certificate) {
    $signtool = Join-Path (Split-Path -Parent $makeappx) "signtool.exe"
    & $signtool sign /fd SHA256 /f $Certificate /p $CertificatePassword (Join-Path $output $packageName)
    if ($LASTEXITCODE -ne 0) { throw "signtool failed" }
}

Get-ChildItem $output -Filter "*.msix*" |
    Select-Object Name, @{n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } } |
    Format-Table -AutoSize
