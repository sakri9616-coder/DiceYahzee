param(
    [int]$Port = 9445,
    [string]$Url = "",
    [int]$TimeoutMinutes = 8,
    [string]$StatePath = "F:\MimoGame\logs\unity-mcp.state.json"
)

$ErrorActionPreference = "Continue"
if ([string]::IsNullOrEmpty($Url)) { $Url = "http://127.0.0.1:$Port/" }
$url = $Url
if (-not $url.EndsWith("/")) { $url += "/" }
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)

function Parse-McpResponse($resp) {
    $ct = ""
    try { $ct = $resp.Headers['Content-Type'] } catch {}
    $content = $resp.Content
    if ($content -is [byte[]]) { $content = [System.Text.Encoding]::UTF8.GetString($content) }
    if ($ct -match 'event-stream' -or $content -match '^\s*event:') {
        $dataLines = @()
        foreach ($line in ($content -split "`n")) {
            if ($line -like 'data:*') { $dataLines += ($line.Substring(5).Trim()) }
        }
        $content = ($dataLines -join '')
    }
    return $content
}

function Invoke-Mcp($method, $params, $sessionId, $id) {
    $payload = @{ jsonrpc = "2.0"; method = $method } | ConvertTo-Json -Depth 10 -Compress
    if ($null -ne $params) {
        $o = @{ jsonrpc = "2.0"; id = $id; method = $method; params = $params }
        $payload = $o | ConvertTo-Json -Depth 10 -Compress
    } elseif ($null -ne $id) {
        $payload = @{ jsonrpc = "2.0"; id = $id; method = $method } | ConvertTo-Json -Depth 10 -Compress
    }
    $h = @{ Accept = "application/json, text/event-stream" }
    if ($sessionId) { $h['Mcp-Session-Id'] = $sessionId }
    return Invoke-WebRequest -Uri $url -Method Post -Body $payload -ContentType "application/json" `
        -Headers $h -TimeoutSec 30 -UseBasicParsing
}

# --- wait for the server ------------------------------------------------------
$init = $null
$attempt = 0
while ((Get-Date) -lt $deadline) {
    $attempt++
    try {
        $init = Invoke-Mcp "initialize" @{
            protocolVersion = "2025-06-18"
            capabilities     = @{}
            clientInfo       = @{ name = "opencode-preflight"; version = "1.0.0" }
        } $null 1
        break
    } catch {
        Write-Output ("[{0}] attempt {1}: {2}" -f (Get-Date -Format "HH:mm:ss"), $attempt, $_.Exception.Message)
        Start-Sleep -Seconds 10
    }
}

if (-not $init) { Write-Output "RESULT: FAIL (server never answered on $url)"; exit 1 }

$sid = $null
try { $sid = $init.Headers['Mcp-Session-Id'] } catch {}
Write-Output "=== initialize response (session=$sid)"
Write-Output (Parse-McpResponse $init)

# --- initialized notification -------------------------------------------------
try {
    $n = @{ jsonrpc = "2.0"; method = "notifications/initialized" } | ConvertTo-Json -Compress
    $h = @{ Accept = "application/json, text/event-stream" }
    if ($sid) { $h['Mcp-Session-Id'] = $sid }
    Invoke-WebRequest -Uri $url -Method Post -Body $n -ContentType "application/json" -Headers $h -TimeoutSec 30 -UseBasicParsing | Out-Null
    Write-Output "initialized notification: ok"
} catch { Write-Output "initialized notification: $($_.Exception.Message)" }

# --- tools/list ----------------------------------------------------------------
try {
    $tl = Invoke-Mcp "tools/list" @{} $sid 2
    $raw = Parse-McpResponse $tl
    $obj = $raw | ConvertFrom-Json
    $tools = $obj.result.tools
    Write-Output "=== tools/list: $($tools.Count) tools"
    ($tools | Select-Object -First 60 | ForEach-Object { "  - $($_.name)" }) -join "`n"
} catch {
    Write-Output "tools/list FAILED: $($_.Exception.Message)"
    $tools = @()
}

# --- one real tool call --------------------------------------------------------
if ($tools.Count -gt 0) {
    $candidate = @("get_scene_info", "get_scene_hierarchy", "get_project_context") | Where-Object {
        ($tools | Where-Object { $_.name -eq $_ }) -ne $null
    } | Select-Object -First 1
    if ($candidate) {
        try {
            $tc = Invoke-Mcp "tools/call" @{ name = $candidate; arguments = @{} } $sid 3
            Write-Output "=== tools/call $candidate"
            Write-Output (Parse-McpResponse $tc)
        } catch { Write-Output "tools/call $candidate FAILED: $($_.Exception.Message)" }
    }
}

if ($tools.Count -gt 0) { Write-Output "RESULT: PASS"; exit 0 } else { Write-Output "RESULT: FAIL"; exit 1 }
