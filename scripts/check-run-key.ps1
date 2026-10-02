# Checks the installer build's Start with Windows switch (docs/PLAN.md P3.5,
# P12.12, P12.15) against the Debug build:
#   1. first launch of a build that Setup did not install registers nothing,
#      and saves startWithWindows: false
#   2. startWithWindows: true registers HKCU\...\Run\Wordwright
#   3. startWithWindows: false removes it again
#
# Wordwright finds %AppData% through the shell, not the APPDATA variable, so
# this cannot be pointed at a scratch folder: it uses YOUR real
# %AppData%\Wordwright\settings.json and YOUR real Run entry. It therefore
# refuses to run without -UseMyRealData, and it backs both up first and puts
# them back afterwards, whatever happens. Quit Wordwright before running it.
param(
    [switch]$UseMyRealData
)

if (-not $UseMyRealData) {
    Write-Output "Refusing to run: this check uses your real %AppData%\Wordwright\settings.json and"
    Write-Output "your real Run entry (it backs both up and restores them). Quit Wordwright, then run:"
    Write-Output "  scripts\check-run-key.ps1 -UseMyRealData"
    exit 2
}

if (Get-Process Wordwright.App -ErrorAction SilentlyContinue) {
    Write-Output "Refusing to run: Wordwright is running. Quit it from the tray first."
    exit 2
}

$exe = Get-ChildItem "$PSScriptRoot\..\src\Wordwright.App\bin\Debug\net10.0-windows*\Wordwright.App.exe" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $exe) { Write-Output "FAIL: build the app first (dotnet build)"; exit 1 }

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$appData = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Wordwright'
$settings = Join-Path $appData 'settings.json'

function Get-RunValue {
    (Get-ItemProperty $runKey -ErrorAction SilentlyContinue).Wordwright
}

function Start-AndStop {
    $app = Start-Process $exe -PassThru
    Start-Sleep -Seconds 3
    $app.Refresh()
    if ($app.HasExited) { throw "app exited on launch (code $($app.ExitCode))" }
    $value = Get-RunValue
    Stop-Process -Id $app.Id -Force
    Start-Sleep -Seconds 1
    return $value
}

# Back up the real state.
$savedSettings = if (Test-Path $settings) { [IO.File]::ReadAllBytes($settings) } else { $null }
$savedRun = Get-RunValue

try {
    # 1. No settings yet, a build Setup did not install: nothing registered.
    if (Test-Path $settings) { Remove-Item $settings -Force }
    if (Get-RunValue) { Remove-ItemProperty $runKey -Name Wordwright }
    $value = Start-AndStop
    if ($value) { Write-Output "FAIL: a dev build's first launch registered [$value]"; exit 1 }
    if ((Get-Content $settings -Raw) -notmatch '"startWithWindows":\s*false') {
        Write-Output "FAIL: first launch did not save startWithWindows: false"; exit 1
    }
    Write-Output "OK: a dev build's first launch registers nothing"

    # 2. startWithWindows true: launching registers the Run entry.
    Set-Content $settings '{"schemaVersion":1,"startWithWindows":true}'
    $value = Start-AndStop
    if ($value -ne "`"$exe`"") { Write-Output "FAIL: Run entry after launch is [$value]"; exit 1 }
    Write-Output "OK: startWithWindows=true registered the Run entry"

    # 3. startWithWindows false: launching removes it.
    Set-Content $settings '{"schemaVersion":1,"startWithWindows":false}'
    $value = Start-AndStop
    if ($value) { Write-Output "FAIL: Run entry still present after disabled launch: [$value]"; exit 1 }
    Write-Output "OK: startWithWindows=false removed the Run entry"

    Write-Output "PASS"
}
catch {
    Write-Output "FAIL: $_"
    exit 1
}
finally {
    # Put the real state back exactly as it was.
    if ($null -ne $savedSettings) {
        [IO.File]::WriteAllBytes($settings, $savedSettings)
    } elseif (Test-Path $settings) {
        Remove-Item $settings -Force
    }

    if ($savedRun) {
        Set-ItemProperty $runKey -Name Wordwright -Value $savedRun
    } elseif (Get-RunValue) {
        Remove-ItemProperty $runKey -Name Wordwright
    }
}
