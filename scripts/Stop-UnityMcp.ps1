param(
    [string]$StatePath = "F:\MimoGame\logs\unity-mcp.state.json",
    [int]$TimeoutSec = 90
)

$ErrorActionPreference = "Stop"
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Stop {
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
}
"@

function Log($msg) { Write-Output ("[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $msg) }

if (-not (Test-Path $StatePath)) { Log "No state file; nothing to stop."; exit 0 }
$state = Get-Content $StatePath -Raw | ConvertFrom-Json
$proc = Get-Process -Id $state.pid -ErrorAction SilentlyContinue
if (-not $proc) { Log "PID $($state.pid) not running."; Remove-Item $StatePath -Force; exit 0 }

Log "Closing Unity PID $($state.pid)..."
# Find the main window for this process and send WM_CLOSE.
Add-Type -AssemblyName System.Windows.Forms
$handle = $proc.MainWindowHandle
if ($handle -eq 0) {
    # fall back to taskkill without /F (graceful)
    & taskkill /PID $state.pid 2>$null | Out-Null
} else {
    [Win32Stop]::SendMessage($handle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
}
$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3
    $proc.Refresh()
    if ($proc.HasExited) { break }
}
if (-not $proc.HasExited) {
    Log "Graceful close timed out; taskkill."
    & taskkill /PID $state.pid 2>$null | Out-Null
    Start-Sleep -Seconds 15
    $proc.Refresh()
}
if (-not $proc.HasExited) { Stop-Process -Id $state.pid -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 3 }
$proc.Refresh()
Log "stopped=$($proc.HasExited)"
Remove-Item $StatePath -Force -ErrorAction SilentlyContinue
