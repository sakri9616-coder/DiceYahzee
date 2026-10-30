param(
    [string]$Url = "http://127.0.0.1:9445/p/daf7e34c/t/abd7e501f52a350f31297acd9ee831de/",
    [string]$OutDir = "F:\MimoGame\logs"
)
$ErrorActionPreference = "Stop"
if (-not $Url.EndsWith("/")) { $Url += "/" }

function Parse-McpResponse($resp) {
    $ct = ""
    try { $ct = $resp.Headers['Content-Type'] } catch {}
    $content = $resp.Content
    if ($content -is [byte[]]) { $content = [System.Text.Encoding]::UTF8.GetString($content) }
    if ($ct -match 'event-stream' -or $content -match '^\s*event:') {
        $dl = @()
        foreach ($line in ($content -split "`n")) { if ($line -like 'data:*') { $dl += $line.Substring(5).Trim() } }
        $content = ($dl -join '')
    }
    return $content
}

function Post-Mcp($bodyObj, $sessionId) {
    $payload = $bodyObj | ConvertTo-Json -Depth 12 -Compress
    $h = @{ Accept = "application/json, text/event-stream" }
    if ($sessionId) { $h['Mcp-Session-Id'] = $sessionId }
    return Invoke-WebRequest -Uri $Url -Method Post -Body $payload -ContentType "application/json" `
        -Headers $h -TimeoutSec 60 -UseBasicParsing
}

# initialize
$init = Post-Mcp @{ jsonrpc="2.0"; id=1; method="initialize"; params=@{
    protocolVersion="2025-06-18"; capabilities=@{}; clientInfo=@{name="opencode-preflight";version="1.0.0"} } } $null
$sid = $init.Headers['Mcp-Session-Id']
Write-Output "session: $sid"
Post-Mcp @{ jsonrpc="2.0"; method="notifications/initialized" } $sid | Out-Null

# tools/list -> find schemas
$tl = Parse-McpResponse (Post-Mcp @{ jsonrpc="2.0"; id=2; method="tools/list"; params=@{} } $sid)
$tools = ($tl | ConvertFrom-Json).result.tools
Write-Output "tools: $($tools.Count)"

function Call-Tool($name, $args) {
    $body = @{ jsonrpc="2.0"; id=(Get-Random -Maximum 99999); method="tools/call";
               params=@{ name=$name; arguments=$args } }
    $raw = Parse-McpResponse (Post-Mcp $body $script:sid)
    return ($raw | ConvertFrom-Json)
}

# 1) scene info
$r = Call-Tool "get_scene_info" @{}
Write-Output "=== get_scene_info"
Write-Output (($r | ConvertTo-Json -Depth 12))

# 2) screenshot through MCP
$capTool = $tools | Where-Object { $_.name -eq "capture_scene_view" }
if ($capTool) {
    Write-Output "=== capture_scene_view schema"
    Write-Output (($capTool.inputSchema | ConvertTo-Json -Depth 12))
    $args = @{}
    foreach ($p in ($capTool.inputSchema.properties.PSObject.Properties)) {
        if ($p.Name -eq "path" -or $p.Name -eq "savePath" -or $p.Name -eq "filePath") { $args[$p.Name] = (Join-Path $OutDir "mcp-scene-view.png") }
        if ($p.Name -eq "width") { $args[$p.Name] = 1024 }
        if ($p.Name -eq "height") { $args[$p.Name] = 576 }
    }
    Write-Output ("args: " + ($args | ConvertTo-Json -Compress))
    try {
        $c = Call-Tool "capture_scene_view" $args
        Write-Output "=== capture_scene_view"
        Write-Output (($c | ConvertTo-Json -Depth 12))
    } catch { Write-Output "capture failed: $($_.Exception.Message)" }
}

Get-ChildItem $OutDir -Filter "mcp-*.png" -ErrorAction SilentlyContinue | Select-Object FullName, Length
