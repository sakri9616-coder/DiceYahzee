# Generic Step 5 chat caller. ASCII-only on purpose: PowerShell 5.1 reads a .ps1 without a BOM
# as the local ANSI code page, so any CJK literal in the script body breaks parsing.
# Prompts (which are Japanese) are supplied as UTF-8 files instead.
# The API key is read from the OpenCode credential store at runtime, never written to disk.
param(
    [Parameter(Mandatory = $true)]
    [string]$UserFile,

    [Parameter(Mandatory = $true)]
    [string]$OutFile,

    [string]$SystemFile = '',

    [string]$SystemText = 'You are an independent game design critic. Reply in Japanese.',

    [string]$Model = 'step-5-preview',

    [int]$MaxTokens = 20000,

    [double]$Temperature = 0.5
)

$ErrorActionPreference = 'Stop'

function Get-StepKey {
    $cli = 'C:\Users\sakur\AppData\Roaming\ai.opencode.desktop\cli\2.0.26\opencode-cli.exe'
    $raw = (& $cli auth export 2>&1 | Out-String)
    $j = $raw | ConvertFrom-Json
    if ($j -isnot [array]) { $j = @($j) }
    $entry = $j | Where-Object { $_.integrationID -eq 'stepfun' } | Select-Object -First 1
    if (-not $entry) { throw 'StepFun credential not found.' }
    return $entry.value.key
}

function Read-Utf8NoBom {
    param([string]$Path)
    return [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8).TrimStart([char]0xFEFF)
}

$user = Read-Utf8NoBom -Path $UserFile
if ($SystemFile -ne '') { $SystemText = Read-Utf8NoBom -Path $SystemFile }

$payload = @{
    model       = $Model
    max_tokens  = $MaxTokens
    temperature = $Temperature
    messages    = @(
        @{ role = 'system'; content = $SystemText },
        @{ role = 'user';   content = $user }
    )
}

$body = $payload | ConvertTo-Json -Depth 6
$key = Get-StepKey
$bytes = [System.Text.Encoding]::UTF8.GetBytes($body)

$req = [System.Net.WebRequest]::Create('https://api.stepfun.ai/v1/chat/completions')
$req.Method = 'POST'
$req.ContentType = 'application/json; charset=utf-8'
$req.Headers.Add('Authorization', "Bearer $key")
$req.Timeout = 1800000
$req.ReadWriteTimeout = 1800000
$req.ContentLength = $bytes.Length
$ws = $req.GetRequestStream()
$ws.Write($bytes, 0, $bytes.Length)
$ws.Close()

try {
    $resp = $req.GetResponse()
}
catch [System.Net.WebException] {
    $er = $_.Exception.Response
    if ($er) {
        $reader = New-Object System.IO.StreamReader($er.GetResponseStream())
        $errBody = $reader.ReadToEnd()
        throw ("HTTP " + $er.StatusCode.value__ + " " + $errBody)
    }
    throw $_.Exception.Message
}

$sr = New-Object System.IO.StreamReader($resp.GetResponseStream())
$respText = $sr.ReadToEnd()
$resp.Close()

$json = $respText | ConvertFrom-Json
$finish = $json.choices[0].finish_reason
$content = $json.choices[0].message.content

$header = "MODEL " + $json.model + " finish=" + $finish +
          " usage=" + $json.usage.prompt_tokens + "/" + $json.usage.completion_tokens +
          "`r`n-----`r`n"

[System.IO.File]::WriteAllText($OutFile, $header + $content, (New-Object System.Text.UTF8Encoding($false)))

Write-Output ("finish=" + $finish + " completion=" + $json.usage.completion_tokens +
               " chars=" + $content.Length + " out=" + $OutFile)
