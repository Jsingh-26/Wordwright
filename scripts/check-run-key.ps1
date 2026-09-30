$exe = (Resolve-Path "$PSScriptRoot\..\src\Wordwright.App\bin\Debug\net10.0-windows\Wordwright.App.exe").Path
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$appData = Join-Path $env:APPDATA 'Wordwright'
$settings = Join-Path $appData 'settings.json'

function Get-RunValue {
    (Get-ItemProperty $runKey -ErrorAction SilentlyContinue).Wordwright
}

function Remove-State {
    if (Test-Path $settings) { Remove-Item $settings -Force }
    $value = Get-RunValue
    if ($value) { Remove-ItemProperty $runKey -Name Wordwright }
}

try {
    Remove-State

    # 1. Default setting (startWithWindows true): launching registers the Run entry.
    $app = Start-Process $exe -PassThru
    Start-Sleep -Seconds 3
    $app.Refresh()
    if ($app.HasExited) { Write-Output "FAIL: app exited on first launch"; exit 1 }
    $value = Get-RunValue
    if ($value -ne "`"$exe`"") {
        Write-Output "FAIL: Run entry after launch is [$value]"
        Stop-Process -Id $app.Id -Force
        exit 1
    }
    Write-Output "OK: default setting registered the Run entry [$value]"
    Stop-Process -Id $app.Id -Force
    Start-Sleep -Seconds 1

    # 2. startWithWindows false: launching removes the Run entry.
    New-Item -ItemType Directory -Path $appData -Force | Out-Null
    Set-Content $settings '{"schemaVersion":1,"startWithWindows":false}'
    $app = Start-Process $exe -PassThru
    Start-Sleep -Seconds 3
    $app.Refresh()
    if ($app.HasExited) { Write-Output "FAIL: app exited on second launch"; exit 1 }
    $value = Get-RunValue
    if ($value) {
        Write-Output "FAIL: Run entry still present after disabled launch: [$value]"
        Stop-Process -Id $app.Id -Force
        exit 1
    }
    Write-Output "OK: startWithWindows=false removed the Run entry"
    Stop-Process -Id $app.Id -Force
    Write-Output "PASS"
}
finally {
    Remove-State
}