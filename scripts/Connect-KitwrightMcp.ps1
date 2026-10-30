param(
    [string]$ProjectPath = "F:\MimoGame\ParvaMirabiliaDice",
    [int]$TimeoutMinutes = 12,
    [string]$StatePath = "F:\MimoGame\logs\unity-mcp.state.json",
    [string]$CliPath = "C:\Users\sakur\AppData\Roaming\ai.opencode.desktop\cli\2.0.26\opencode-cli.exe"
)

$ErrorActionPreference = "Stop"
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)

function Log($msg) { Write-Output ("[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $msg) }

# pin = first 8 chars of SHA256(normalized lower-case forward-slash path)
function Get-ProjectPin($path) {
    $norm = $path.Replace('\', '/').TrimEnd('/').ToLowerInvariant()
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $hash = ($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($norm)) | ForEach-Object { $_.ToString("x2") }) -join ''
    return $hash.Substring(0, 8)
}

function Get-ServerToken($pin) {
    $k = "HKCU:\Software\Unity Technologies\Unity Editor 5.x"
    if (-not (Test-Path $k)) { return $null }
    $names = (Get-Item $k).GetValueNames() | Where-Object { $_ -like "KitWright.MCP.ServerToken.*" }
    foreach ($n in $names) {
        if ($n -like "*$pin*") {
            $raw = (Get-Item $k).GetValue($n)
            if ($raw -is [byte[]]) { return [System.Text.Encoding]::ASCII.GetString(($raw | Where-Object { $_ -ne 0 })) }
            $parts = "$raw" -split '\s+' | Where-Object { $_ -match '^\d+$' }
            return ((($parts | ForEach-Object { [char][int]$_ }) -join '').Trim([char]0))
        }
    }
    return $null
}

$pin = Get-ProjectPin $ProjectPath
Log "project: $ProjectPath"
Log "pin: $pin"

# --- wait for the MCP server to answer ---------------------------------------
$port = 0
$token = $null
$url = $null
$ok = $false
$attempt = 0
while ((Get-Date) -lt $deadline) {
    $attempt++
    if ($token) {
        $url = "http://127.0.0.1:$port/p/$pin/t/$token/"
        try {
            $body = @{ jsonrpc = "2.0"; id = 1; method = "initialize"; params = @{
                protocolVersion = "2025-06-18"; capabilities = @{}
                clientInfo = @{ name = "opencode-connect"; version = "1.0.0" } } } | ConvertTo-Json -Depth 6 -Compress
            $r = Invoke-WebRequest -Uri $url -Method Post -Body $body -ContentType "application/json" `
                -Headers @{ Accept = "application/json, text/event-stream" } -TimeoutSec 15 -UseBasicParsing
            if ($r.StatusCode -eq 200) { $ok = $true; break }
        } catch { }
    }
    # resolve port from the state file written by Start-UnityMcp, else from settings
    if ($port -eq 0) {
        if (Test-Path $StatePath) {
            try { $port = [int]((Get-Content $StatePath -Raw | ConvertFrom-Json).port) } catch { }
        }
        if ($port -eq 0) {
            $s = Join-Path $ProjectPath "UserSettings\KitWrightMcpSettings.json"
            if (Test-Path $s) { try { $port = [int]((Get-Content $s -Raw | ConvertFrom-Json).port) } catch { } }
        }
        if ($port -eq 0) { $port = 9445 }
    }
    if (-not $token) { $token = Get-ServerToken $pin }
    if ($attempt -le 120 -and ($attempt % 6 -eq 0)) { Log "waiting... port=$port token=$(if ($token) { 'found' } else { 'missing' }) attempt=$attempt" }
    Start-Sleep -Seconds 5
}

if (-not $ok) {
    Log "RESULT: FAIL - MCP server did not answer (port=$port token=$(if ($token) { 'ok' } else { 'missing' }))"
    exit 1
}

Log "MCP ready at $url"

# --- rewrite the OpenCode project config -------------------------------------
$cfgPath = "F:\MimoGame\opencode.json"
$cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
if (-not $cfg.mcp) { $cfg | Add-Member -NotePropertyName mcp -NotePropertyValue ([pscustomobject]@{ servers = [pscustomobject]@{} }) }
if (-not $cfg.mcp.servers) { $cfg.mcp | Add-Member -NotePropertyName servers -NotePropertyValue ([pscustomobject]@{}) }
if ($cfg.mcp.servers.kitwright) {
    $cfg.mcp.servers.kitwright.url = $url
} else {
    $cfg.mcp.servers | Add-Member -NotePropertyName kitwright -NotePropertyValue ([pscustomobject]@{ type = "remote"; url = $url })
}
$cfg | ConvertTo-Json -Depth 10 | Set-Content $cfgPath -Encoding UTF8
Log "opencode.json updated"

# --- refresh the running OpenCode server ------------------------------------
& $CliPath reload
Start-Sleep -Seconds 3
& $CliPath mcp list

# --- persist the resolved endpoint for later scripts -------------------------
$state = [pscustomobject]@{
    pid     = if (Test-Path $StatePath) { (Get-Content $StatePath -Raw | ConvertFrom-Json).pid } else { 0 }
    port    = $port
    pin     = $pin
    token   = $token
    url     = $url
    project = $ProjectPath
}
$state | ConvertTo-Json | Set-Content $StatePath -Encoding UTF8
Log "state saved"
exit 0
