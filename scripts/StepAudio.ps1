# StepFun official API helper for Parva Mirabilia Dice audio generation.
# The API key is read from the OpenCode credential store at runtime and never written to disk.
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('sfx', 'bgm-submit', 'bgm-poll', 'chat')]
    [string]$Mode,

    [string]$PromptFile = '',
    [string]$OutDir = 'D:\MimoGame\ParvaMirabiliaDice\Assets\Audio',
    [string]$Model = 'step-5-preview'
)

$ErrorActionPreference = 'Stop'

function Get-StepKey {
    $cli = 'C:\Users\sakur\AppData\Roaming\ai.opencode.desktop\cli\2.0.26\opencode-cli.exe'
    $raw = (& $cli auth export 2>&1 | Out-String)
    $j = $raw | ConvertFrom-Json
    if ($j -isnot [array]) { $j = @($j) }
    $entry = $j | Where-Object { $_.integrationID -eq 'stepfun' } | Select-Object -First 1
    if (-not $entry) { throw 'StepFun credential not found in the OpenCode credential store.' }
    return $entry.value.key
}

$Base = 'https://api.stepfun.ai/v1'

function Invoke-StepJson {
    param([string]$Path, [string]$JsonBody, [int]$TimeoutSec = 300)
    $key = Get-StepKey
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($JsonBody)
    $req = [System.Net.WebRequest]::Create($Base + $Path)
    $req.Method = 'POST'
    $req.ContentType = 'application/json; charset=utf-8'
    $req.Headers.Add('Authorization', "Bearer $key")
    # WebRequest.Timeout is expressed in milliseconds.
    $req.Timeout = [int]($TimeoutSec * 1000)
    $req.ReadWriteTimeout = [int]($TimeoutSec * 1000)
    $req.ContentLength = $bytes.Length
    $s = $req.GetRequestStream()
    $s.Write($bytes, 0, $bytes.Length)
    $s.Close()
    try {
        $resp = $req.GetResponse()
    }
    catch [System.Net.WebException] {
        $er = $_.Exception.Response
        if ($er) {
            $reader = New-Object System.IO.StreamReader($er.GetResponseStream())
            $body = $reader.ReadToEnd()
            throw ("HTTP " + $er.StatusCode.value__ + " " + $body)
        }
        throw $_.Exception.Message
    }
    $sr = New-Object System.IO.StreamReader($resp.GetResponseStream())
    $text = $sr.ReadToEnd()
    $resp.Close()
    return $text
}

function Save-StepUrl {
    param([string]$Url, [string]$Path)
    $wc = New-Object System.Net.WebClient
    $wc.DownloadFile($Url, $Path)
    $wc.Dispose()
    return (Get-Item -LiteralPath $Path).Length
}

function Save-StepBase64 {
    param([string]$Base64, [string]$Path)
    [System.IO.File]::WriteAllBytes($Path, [System.Convert]::FromBase64String($Base64))
    return (Get-Item -LiteralPath $Path).Length
}

# ---------- SFX batch ----------
function Invoke-SfxBatch {
    if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
    $items = Get-Content -LiteralPath $PromptFile -Raw | ConvertFrom-Json
    foreach ($it in $items) {
        $target = Join-Path $OutDir ($it.name + '.mp3')
        if ((Test-Path $target) -and (Get-Item -LiteralPath $target).Length -gt 4096 -and -not $it.force) {
            Write-Output ("SKIP " + $it.name + " (exists)")
            continue
        }
        $text = $it.text -replace '"', '\"'
        $body = '{"model":"stepaudio-3-gen-preview","task":"text_to_audio","scripts":[{"text":"' + $text + '"}],"response_format":"mp3","return_url":true}'
        try {
            $respText = Invoke-StepJson -Path '/audio/generate' -JsonBody $body -TimeoutSec 300
            $json = $respText | ConvertFrom-Json
            $url = $json.data.url
            if (-not $url) { Write-Output ("NORESULT " + $it.name + " " + $respText.Substring(0, [Math]::Min(200, $respText.Length))); continue }
            $len = Save-StepUrl -Url $url -Path $target
            Write-Output ("OK " + $it.name + " bytes=" + $len)
        }
        catch {
            Write-Output ("FAIL " + $it.name + " :: " + $_.Exception.Message.Substring(0, [Math]::Min(300, $_.Exception.Message.Length)))
        }
        Start-Sleep -Milliseconds 800
    }
}

# ---------- BGM (async submit / poll / download) ----------
function Submit-Bgm {
    $items = Get-Content -LiteralPath $PromptFile -Raw | ConvertFrom-Json
    $tasks = @()
    foreach ($it in $items) {
        $caption = $it.caption -replace '"', '\"'
        $body = '{"task":"text_to_music","model_id":"stepaudio-3-music-preview","instrumental":true,"caption":"' + $caption + '","response_format":"mp3"}'
        try {
            $respText = Invoke-StepJson -Path '/audio/music/submit' -JsonBody $body -TimeoutSec 120
            $json = $respText | ConvertFrom-Json
            $tasks += [pscustomobject]@{ name = $it.name; task_id = $json.task_id }
            Write-Output ("SUBMITTED " + $it.name + " task=" + $json.task_id)
        }
        catch {
            Write-Output ("SUBMIT_FAIL " + $it.name + " :: " + $_.Exception.Message.Substring(0, [Math]::Min(300, $_.Exception.Message.Length)))
        }
    }
    $tasks | ConvertTo-Json | Out-File -FilePath 'D:\MimoGame\logs\bgm-tasks.json' -Encoding utf8
}

function Poll-Bgm {
    if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
    $tasks = Get-Content -LiteralPath 'D:\MimoGame\logs\bgm-tasks.json' -Raw | ConvertFrom-Json
    if ($tasks -isnot [array]) { $tasks = @($tasks) }
    $deadline = (Get-Date).AddMinutes(12)
    $pending = @{}
    foreach ($t in $tasks) { $pending[$t.name] = $t.task_id }
    while ($pending.Count -gt 0 -and (Get-Date) -lt $deadline) {
        foreach ($name in @($pending.Keys)) {
            $body = '{"task_id":"' + $pending[$name] + '"}'
            try {
                $respText = Invoke-StepJson -Path '/audio/music/query' -JsonBody $body -TimeoutSec 60
                $json = $respText | ConvertFrom-Json
                if ($json.status -eq 'SUCCESS') {
                    $target = Join-Path $OutDir ($name + '.mp3')
                    $len = Save-StepBase64 -Base64 $json.audio -Path $target
                    Write-Output ("DONE " + $name + " bytes=" + $len)
                    $pending.Remove($name)
                }
                elseif ($json.status -eq 'FAILED') {
                    Write-Output ("FAILED " + $name + " stage=" + $json.error.stage + " msg=" + $json.error.message)
                    $pending.Remove($name)
                }
                else {
                    Write-Output ("WAIT " + $name + " " + $json.status)
                }
            }
            catch {
                Write-Output ("QUERY_ERR " + $name + " " + $_.Exception.Message.Substring(0, [Math]::Min(200, $_.Exception.Message.Length)))
            }
        }
        if ($pending.Count -gt 0) { Start-Sleep -Seconds 6 }
    }
    foreach ($k in $pending.Keys) { Write-Output ("TIMEOUT " + $k) }
}

function Invoke-Chat {
    # Strip the UTF-8 BOM: a leading BOM makes the API answer "invalid request format".
    $body = (Get-Content -LiteralPath $PromptFile -Raw -Encoding UTF8).TrimStart([char]0xFEFF)
    $respText = Invoke-StepJson -Path '/chat/completions' -JsonBody $body -TimeoutSec 600
    $json = $respText | ConvertFrom-Json
    Write-Output ("MODEL " + $json.model + " finish=" + $json.choices[0].finish_reason + " usage=" + $json.usage.prompt_tokens + "/" + $json.usage.completion_tokens)
    Write-Output '-----'
    Write-Output $json.choices[0].message.content
}

switch ($Mode) {
    'sfx' { Invoke-SfxBatch }
    'bgm-submit' { Submit-Bgm }
    'bgm-poll' { Poll-Bgm }
    'chat' { Invoke-Chat }
}
