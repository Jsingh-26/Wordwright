$p = Start-Process "$PSScriptRoot\..\src\Wordwright.App\bin\Debug\net10.0-windows\Wordwright.App.exe" -PassThru
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