param([string]$TitlePattern = 'ParvaMirabiliaDice*')

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class UnityForeground {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
}
"@

$p = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like $TitlePattern } | Select-Object -First 1
if (-not $p) {
    Write-Output 'no-window'
    return
}

# Alt key dance: Windows rejects SetForegroundWindow unless the caller owns the input queue.
[UnityForeground]::keybd_event(0x12, 0, 0, 0)
[UnityForeground]::keybd_event(0x12, 0, 2, 0)
[UnityForeground]::BringWindowToTop($p.MainWindowHandle) | Out-Null
[UnityForeground]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 400
Write-Output ('foregrounded ' + $p.MainWindowTitle)
