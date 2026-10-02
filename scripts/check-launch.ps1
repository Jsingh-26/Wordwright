# Launches the Debug build and reports whether it is running (docs/PLAN.md P12.15).
# It uses your real %AppData%\Wordwright (the app ignores the APPDATA variable),
# and with startWithWindows on, a launch points the Run entry at this build.
$exe = Get-ChildItem "$PSScriptRoot\..\src\Wordwright.App\bin\Debug\net10.0-windows*\Wordwright.App.exe" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $exe) { Write-Output "FAIL: build the app first (dotnet build)"; exit 1 }

$p = Start-Process $exe -PassThru
Start-Sleep -Seconds 4
if ($p.HasExited) {
    Write-Output ("EXITED code=" + $p.ExitCode)
} else {
    $p.Refresh()
    if ($p.MainWindowTitle) {
        Write-Output ("RUNNING window=[" + $p.MainWindowTitle + "]")
    } else {
        Write-Output "RUNNING but no main window title"
    }
    Stop-Process -Id $p.Id -Force
    Write-Output "killed"
}