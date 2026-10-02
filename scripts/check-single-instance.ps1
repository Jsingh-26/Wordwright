# Checks single-instance behaviour against the Debug build (docs/PLAN.md P12.15).
# It uses your real %AppData%\Wordwright (the app ignores the APPDATA variable),
# and with startWithWindows on, a launch points the Run entry at this build.
$exe = Get-ChildItem "$PSScriptRoot\..\src\Wordwright.App\bin\Debug\net10.0-windows*\Wordwright.App.exe" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $exe) { Write-Output "FAIL: build the app first (dotnet build)"; exit 1 }

# First launch: should start hidden (in tray), no window title.
$first = Start-Process $exe -PassThru
Start-Sleep -Seconds 3
$first.Refresh()
if ($first.HasExited) { Write-Output "FAIL: first instance exited"; exit 1 }
if ($first.MainWindowTitle) {
    Write-Output ("FAIL: first launch showed a window: [" + $first.MainWindowTitle + "]")
    Stop-Process -Id $first.Id -Force
    exit 1
}
Write-Output "OK: first launch runs hidden (starts to tray)"

# Second launch: should exit by itself and signal the first instance to show its window.
$second = Start-Process $exe -PassThru
$second.WaitForExit(10000) | Out-Null
if (-not $second.HasExited) { Write-Output "FAIL: second instance did not exit"; Stop-Process -Id $second.Id -Force; Stop-Process -Id $first.Id -Force; exit 1 }
Write-Output "OK: second launch exited by itself"

Start-Sleep -Seconds 2
$first.Refresh()
if ($first.MainWindowTitle -eq "Wordwright") {
    Write-Output "OK: first instance opened its window"
} else {
    Write-Output ("FAIL: first instance window is [" + $first.MainWindowTitle + "]")
    Stop-Process -Id $first.Id -Force
    exit 1
}

Stop-Process -Id $first.Id -Force
Write-Output "PASS"