param(
    [string]$EditorPath = "C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe",
    [string]$ProjectPath = "F:\MimoGame\ParvaMirabiliaDice",
    [string]$LogPath = "F:\MimoGame\logs\preflight-gui.log",
    [string]$ShotPath = "F:\MimoGame\logs\preflight-editor-window.png",
    [int]$ReadyTimeoutSec = 600,
    [int]$CloseTimeoutSec = 90
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Cap {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
"@

function Log($msg) { Write-Output ("[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $msg) }

if (Test-Path $LogPath) { Remove-Item $LogPath -Force }

# --- 3. Launch the Unity Editor GUI from the command line -------------------
Log "Launching Unity Editor GUI: $EditorPath"
Log "Project: $ProjectPath"
$proc = Start-Process -FilePath $EditorPath `
    -ArgumentList ("-projectPath `"$ProjectPath`" -logFile `"$LogPath`"") `
    -PassThru
Log "PID: $($proc.Id)"

# --- Wait for a real editor main window (not the splash/progress window) ----
$deadline = (Get-Date).AddSeconds($ReadyTimeoutSec)
$ready = $false
$title = ""
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 5
    $proc.Refresh()
    if ($proc.HasExited) { break }
    $title = $proc.MainWindowTitle
    $h = $proc.MainWindowHandle
    if ($h -ne 0 -and $title -match 'Unity 6') { $ready = $true; break }
    $refreshed = $false
    if (Test-Path $LogPath) {
        $refreshed = Select-String -Path $LogPath -Pattern "Initial Refresh End" -Quiet -ErrorAction SilentlyContinue
    }
    if ($h -ne 0 -and $refreshed) { $ready = $true; break }
}
Log ("ready=" + $ready + " title='" + $title + "' handle=" + $proc.MainWindowHandle)

if ($proc.HasExited) {
    Log "Editor exited early with code $($proc.ExitCode)"
    if (Test-Path $LogPath) { Get-Content $LogPath -Tail 40 }
    exit 1
}
if (-not $ready) {
    Log "Editor window never became ready within $ReadyTimeoutSec seconds"
    if (Test-Path $LogPath) { Get-Content $LogPath -Tail 40 }
    exit 1
}

# Let the editor finish the first import and paint.
Start-Sleep -Seconds 10
$proc.Refresh()
Log "Editor running. mode=GUI (windowed, no -batchmode) title='$($proc.MainWindowTitle)'"

# --- 6a. Screenshot of the running Editor window -----------------------------
function Save-Capture {
    param([System.Drawing.Bitmap]$bmp, [string]$path)
    $dir = Split-Path $path -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

$shotOk = $false
try {
    [Win32Cap]::ShowWindow($proc.MainWindowHandle, 9) | Out-Null   # SW_RESTORE
    [Win32Cap]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
    Start-Sleep -Seconds 3

    $r = New-Object Win32Cap+RECT
    for ($i = 0; $i -lt 6 -and -not $shotOk; $i++) {
        $proc.Refresh()
        [Win32Cap]::GetWindowRect($proc.MainWindowHandle, [ref]$r) | Out-Null
        $w = $r.Right - $r.Left
        $h = $r.Bottom - $r.Top
        if ($w -gt 50 -and $h -gt 50) {
            $bmp = New-Object System.Drawing.Bitmap($w, $h)
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $hdc = $g.GetHdc()
            $printed = [Win32Cap]::PrintWindow($proc.MainWindowHandle, $hdc, 3)
            $g.ReleaseHdc($hdc)
            $g.Dispose()
            if ($printed) {
                Save-Capture -bmp $bmp -path $ShotPath
                $shotOk = $true
                Log "Screenshot saved (PrintWindow ${w}x${h}): $ShotPath"
            } else {
                $bmp.Dispose()
                Log "PrintWindow returned false (attempt $($i+1))"
            }
        } else {
            Log "Window rect still ${w}x${h} (attempt $($i+1))"
        }
        if (-not $shotOk) { Start-Sleep -Seconds 3 }
    }

    if (-not $shotOk) {
        # Fallback: capture whatever is on screen at the window position, else the primary screen.
        $sw = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        Add-Type -AssemblyName System.Windows.Forms
        $sw = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bmp = New-Object System.Drawing.Bitmap($sw.Width, $sw.Height)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen(0, 0, 0, 0, (New-Object System.Drawing.Size($sw.Width, $sw.Height)))
        $g.Dispose()
        Save-Capture -bmp $bmp -path $ShotPath
        $shotOk = $true
        Log "Screenshot saved (full-screen fallback ${sw.Width}x${sw.Height}): $ShotPath"
    }
} catch {
    Log "Screenshot failed: $($_.Exception.Message)"
}

# --- Close the editor gracefully --------------------------------------------
Log "Closing editor (WM_CLOSE)..."
[Win32Cap]::SendMessage($proc.MainWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
$closeDeadline = (Get-Date).AddSeconds($CloseTimeoutSec)
while ((Get-Date) -lt $closeDeadline) {
    Start-Sleep -Seconds 3
    $proc.Refresh()
    if ($proc.HasExited) { break }
}
if (-not $proc.HasExited) {
    Log "Graceful close timed out; taskkill (no /F) then force."
    & taskkill /PID $proc.Id 2>$null | Out-Null
    Start-Sleep -Seconds 15
    $proc.Refresh()
}
if (-not $proc.HasExited) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
}
$proc.Refresh()
Log "Editor process exited: $($proc.HasExited) code=$(if ($proc.HasExited) { $proc.ExitCode } else { 'n/a' })"

if (Test-Path $LogPath) {
    Log "--- log markers ---"
    Select-String -Path $LogPath -Pattern "Initial Refresh End|error CS|Compilation failed|Exception|Exiting batchmode|Application quit" |
        Select-Object -Last 20 | ForEach-Object { $_.Line }
}

if ($shotOk) { exit 0 } else { exit 2 }
