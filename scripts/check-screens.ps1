# Captures every page of the running Wordwright window for a hand check.
# Run the app first (a second launch just shows the existing window). Output:
# scripts/ui-check-<page>.png (ignored by git). Also counts interactive
# controls on the last page that have no UI Automation name, which a screen
# reader would read as "button".
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L,T,Rt,B; }
}
public static class M {
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
}
"@
# Physical pixels everywhere (window rect, screen copy, clicks), whatever the display scaling.
[W]::SetProcessDPIAware() | Out-Null
$p = Get-Process Wordwright.App -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Write-Output "Wordwright.App is not running"; exit 1 }
$hwnd = $p.MainWindowHandle
if ($hwnd -eq 0) { Write-Output "no main window; open it from the tray first"; exit 1 }
[W]::ShowWindow($hwnd, 9) | Out-Null; [W]::SetForegroundWindow($hwnd) | Out-Null; Start-Sleep -Milliseconds 800

function Snap($name) {
  $r = New-Object W+R; [W]::GetWindowRect($hwnd, [ref]$r) | Out-Null
  $w = $r.Rt - $r.L; $h = $r.B - $r.T
  $bmp = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size); $g.Dispose()
  $path = Join-Path $PSScriptRoot "ui-check-$name.png"; $bmp.Save($path); $bmp.Dispose()
  Write-Output "saved $path ($w x $h)"
}

foreach ($n in @("Snippets","Settings","About")) {
  # Re-read the tree each time: WPF-UI rebuilds parts of it on navigation.
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $n)
  $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
  if ($el -eq $null) { Write-Output "nav item '$n' not found"; continue }
  # WPF-UI's NavigationViewItem exposes neither SelectionItem nor Invoke, so
  # fall back to a real click on the item's clickable point.
  $done = $false
  try { $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); $done = $true } catch {}
  if (-not $done) { try { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); $done = $true } catch {} }
  if (-not $done) {
    $pt = $el.GetClickablePoint()
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point([int]$pt.X, [int]$pt.Y)
    [M]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); [M]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
  }
  Start-Sleep -Milliseconds 700
  Snap ($n -replace ' ','-')

  # Count interactive controls on this page that a screen reader would read as
  # "button" (docs/PLAN.md P3.4b: Settings and About must report zero).
  $all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  $unnamed = @(); $total = 0
  foreach ($e in $all) {
    $c = $e.Current
    if ($c.ControlType.ProgrammaticName -match 'Button|Edit|ListItem|CheckBox|ComboBox|Hyperlink') {
      $total++; if (-not $c.Name) { $unnamed += $c.ControlType.ProgrammaticName + " (" + $c.AutomationId + ")" }
    }
  }
  Write-Output "$n`: $total interactive controls; without an automation name: $($unnamed.Count)"
  $unnamed | ForEach-Object { Write-Output "  unnamed: $_" }
}
