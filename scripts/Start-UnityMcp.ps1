param(
    [string]$EditorPath = "C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe",
    [string]$ProjectPath = "F:\MimoGame\ParvaMirabiliaDice",
    [string]$LogPath = "F:\MimoGame\logs\unity-mcp.log",
    [string]$ShotPath = "F:\MimoGame\logs\unity-mcp-window.png",
    [string]$StatePath = "F:\MimoGame\logs\unity-mcp.state.json",
    [int]$ReadyTimeoutSec = 600,
    [int]$McpTimeoutSec = 300,
    [switch]$NoScreenshot,
    [switch]$KeepRunning,
    [switch]$Wait
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Mcp {
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

Log "Launching Unity Editor GUI (MCP server expected to auto-start)"
$proc = Start-Process -FilePath $EditorPath `
    -ArgumentList ("-projectPath `"$ProjectPath`" -logFile `"$LogPath`"") `
    -PassThru
Log "PID: $($proc.Id)"

# --- wait for editor window --------------------------------------------------
$deadline = (Get-Date).AddSeconds($ReadyTimeoutSec)
$ready = $false
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 5
    $proc.Refresh()
    if ($proc.HasExited) { break }
    if ($proc.MainWindowHandle -ne 0 -and $proc.MainWindowTitle -match 'Unity 6') { $ready = $true; break }
    if ($proc.MainWindowHandle -ne 0 -and (Test-Path $LogPath) -and
        (Select-String -Path $LogPath -Pattern "Initial Refresh End" -Quiet -ErrorAction SilentlyContinue)) {
        $ready = $true; break
    }
}
Log ("editorReady=" + $ready + " title='" + $proc.MainWindowTitle + "'")
if (-not $ready) {
    Log "Editor failed to become ready"
    if (Test-Path $LogPath) { Get-Content $LogPath -Tail 50 }
    exit 1
}

# --- wait for the KitWright MCP server --------------------------------------
$port = 0
$mcpDeadline = (Get-Date).AddSeconds($McpTimeoutSec)
$pattern = 'MCP Server (?:started on|attached to existing listener on) http://127\.0\.0\.1:(\d+)'
while ((Get-Date) -lt $mcpDeadline) {
    Start-Sleep -Seconds 5
    if (Test-Path $LogPath) {
        $m = Select-String -Path $LogPath -Pattern $pattern -ErrorAction SilentlyContinue | Select-Object -Last 1
        if ($m) { $port = [int]$m.Matches[0].Groups[1].Value; break }
        $fail = Select-String -Path $LogPath -Pattern "KitWright MCP Server" -ErrorAction SilentlyContinue |
                Where-Object { $_.Line -match "Failed|Skipping server start|error" } | Select-Object -First 1
        if ($fail) { Log "MCP error in log: $($fail.Line)"; break }
    }
    $proc.Refresh()
    if ($proc.HasExited) { break }
}
Log "mcpPort=$port"

# --- fallback: read the persisted settings ----------------------------------
if ($port -eq 0) {
    $settings = Join-Path $ProjectPath "UserSettings\KitWrightMcpSettings.json"
    if (Test-Path $settings) {
        Log "settings: $((Get-Content $settings -Raw))"
        try { $port = [int]((Get-Content $settings -Raw | ConvertFrom-Json).port) } catch {}
    }
}

# --- screenshot ---------------------------------------------------------------
if (-not $NoScreenshot) {
    try {
        $proc.Refresh()
        [Win32Mcp]::ShowWindow($proc.MainWindowHandle, 9) | Out-Null
        [Win32Mcp]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
        Start-Sleep -Seconds 3
        $r = New-Object Win32Mcp+RECT
        [Win32Mcp]::GetWindowRect($proc.MainWindowHandle, [ref]$r) | Out-Null
        $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
        if ($w -gt 50 -and $h -gt 50) {
            $bmp = New-Object System.Drawing.Bitmap($w, $h)
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $hdc = $g.GetHdc()
            $printed = [Win32Mcp]::PrintWindow($proc.MainWindowHandle, $hdc, 3)
            $g.ReleaseHdc($hdc); $g.Dispose()
            if ($printed) {
                $bmp.Save($ShotPath, [System.Drawing.Imaging.ImageFormat]::Png)
                Log "screenshot: $ShotPath"
            }
            $bmp.Dispose()
        }
    } catch { Log "screenshot failed: $($_.Exception.Message)" }
}

# --- state file ---------------------------------------------------------------
$state = @{
    pid = $proc.Id
    port = $port
    url = "http://127.0.0.1:$port/"
    project = $ProjectPath
    log = $LogPath
    startedAt = (Get-Date).ToString("o")
} | ConvertTo-Json
Set-Content -Path $StatePath -Value $state -Encoding UTF8
Log "state: $state"

if ($Wait) {
    Log "Waiting for the editor to exit (keeps the MCP server alive)..."
    $proc.WaitForExit()
    Log "Editor exited with code $($proc.ExitCode)"
    Remove-Item $StatePath -Force -ErrorAction SilentlyContinue
    exit $(if ($proc.ExitCode -eq 0) { 0 } else { 4 })
}

if ($KeepRunning) {
    Log "Editor left running for MCP use. Stop with scripts/Stop-UnityMcp.ps1"
    exit $(if ($port -gt 0) { 0 } else { 3 })
}

# --- otherwise close it --------------------------------------------------------
Log "Closing editor..."
[Win32Mcp]::SendMessage($proc.MainWindowHandle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
$closeDeadline = (Get-Date).AddSeconds(90)
while ((Get-Date) -lt $closeDeadline) { Start-Sleep -Seconds 3; $proc.Refresh(); if ($proc.HasExited) { break } }
if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 3 }
exit $(if ($port -gt 0) { 0 } else { 3 })
