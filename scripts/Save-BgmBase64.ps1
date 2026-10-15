# The music API returns the finished loop as a base64 MP3 inside the query response.
# This script queries the task, decodes it and swaps it into the project (old clip kept in logs/bgm_candidates).
$ErrorActionPreference = 'Stop'

$cli = 'C:\Users\sakur\AppData\Roaming\ai.opencode.desktop\cli\2.0.26\opencode-cli.exe'
$raw = (& $cli auth export 2>&1 | Out-String)
$j = $raw | ConvertFrom-Json
if ($j -isnot [array]) { $j = @($j) }
$key = ($j | Where-Object { $_.integrationID -eq 'stepfun' } | Select-Object -First 1).value.key

$tasks = Get-Content -LiteralPath 'D:\MimoGame\logs\bgm-tasks.json' -Raw | ConvertFrom-Json
$task = @($tasks) | Where-Object { $_.name -eq 'bgm_duel' } | Select-Object -First 1
if (-not $task) { $task = @($tasks)[0] }

$body = '{"task_id":"' + $task.task_id + '"}'
$bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
$req = [System.Net.WebRequest]::Create('https://api.stepfun.ai/v1/audio/music/query')
$req.Method = 'POST'
$req.ContentType = 'application/json; charset=utf-8'
$req.Headers.Add('Authorization', "Bearer $key")
$req.Timeout = 90000
$req.ContentLength = $bytes.Length
$s = $req.GetRequestStream()
$s.Write($bytes, 0, $bytes.Length)
$s.Close()
$resp = $req.GetResponse()
$sr = New-Object System.IO.StreamReader($resp.GetResponseStream())
$txt = $sr.ReadToEnd()
$resp.Close()

$o = $txt | ConvertFrom-Json
Write-Output ('status=' + $o.status + ' model=' + $o.model_id + ' format=' + $o.response_format +
              ' lyrics=' + $o.rewritten_lyrics)

$b64 = $null
foreach ($p in $o.PSObject.Properties) {
    if ($p.Value -is [string] -and $p.Value.Length -gt 1000) {
        Write-Output ('big string field: ' + $p.Name + ' (chars ' + $p.Value.Length + ')')
        if (-not $b64) { $b64 = $p.Value }
    }
}
if (-not $b64) { Write-Output 'NO AUDIO FIELD'; exit 1 }

$mp3 = [System.Convert]::FromBase64String($b64)
Write-Output ('decoded bytes=' + $mp3.Length)

$audio  = 'D:\MimoGame\ParvaMirabiliaDice\Assets\Resources\Audio\bgm_duel.mp3'
$backup = 'D:\MimoGame\logs\bgm_candidates\bgm_duel_gen-9s.mp3'
if (-not (Test-Path -LiteralPath $backup)) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backup) | Out-Null
    Copy-Item -LiteralPath $audio -Destination $backup -Force
    Write-Output 'backed up old gen clip to logs/bgm_candidates/bgm_duel_gen-9s.mp3'
}

[System.IO.File]::WriteAllBytes($audio, $mp3)
$f = Get-Item -LiteralPath $audio
Write-Output ('written bytes=' + $f.Length + ' at ' + $f.LastWriteTime.ToString('HH:mm:ss'))

$head = ($mp3[0..2] | ForEach-Object { $_.ToString('X2') }) -join ' '
if ($mp3[0] -eq 0x49 -and $mp3[1] -eq 0x44 -and $mp3[2] -eq 0x33) { Write-Output 'header=ID3' }
elseif ($mp3[0] -eq 0xFF -and (($mp3[1] -band 0xE0) -eq 0xE0)) { Write-Output 'header=MP3FRAME' }
else { Write-Output ('header=UNEXPECTED ' + $head) }

$md5 = (Get-FileHash -LiteralPath $audio -Algorithm MD5).Hash
Write-Output ('md5=' + $md5)
