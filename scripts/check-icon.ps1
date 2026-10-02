Add-Type -AssemblyName System.Drawing
$exe = Get-ChildItem "$PSScriptRoot\..\src\Wordwright.App\bin\Debug\net10.0-windows*\Wordwright.App.exe" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $exe) { Write-Output "FAIL: build the app first (dotnet build)"; exit 1 }
$ico = "$PSScriptRoot\..\brand\icon.ico"

$embedded = [System.Drawing.Icon]::ExtractAssociatedIcon($exe)
Write-Output ("exe embedded icon: " + $embedded.Size)

$big = New-Object System.Drawing.Icon($ico, 256, 256)
Write-Output ("ico reads at 256x256: " + $big.Size)

$small = New-Object System.Drawing.Icon($ico, 16, 16)
Write-Output ("ico reads at 16x16: " + $small.Size)