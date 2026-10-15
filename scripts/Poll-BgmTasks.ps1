# Polls the StepFun music tasks until none is PENDING/RUNNING (max ~6 minutes).
$cli = 'C:\Users\sakur\AppData\Roaming\ai.opencode.desktop\cli\2.0.26\opencode-cli.exe'
$raw = (& $cli auth export 2>&1 | Out-String)
$j = $raw | ConvertFrom-Json
if ($j -isnot [array]) { $j = @($j) }
$key = ($j | Where-Object { $_.integrationID -eq 'stepfun' } | Select-Object -First 1).value.key
$tasks = Get-Content -LiteralPath 'D:\MimoGame\logs\bgm-tasks.json' -Raw | ConvertFrom-Json

function Query-One($id) {
    $body = '{"task_id":"' + $id + '"}'
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
    $req = [System.Net.WebRequest]::Create('https://api.stepfun.ai/v1/audio/music/query')
    $req.Method = 'POST'
    $req.ContentType = 'application/json; charset=utf-8'
    $req.Headers.Add('Authorization', "Bearer $key")
    $req.Timeout = 60000
    $req.ContentLength = $bytes.Length
    $s = $req.GetRequestStream()
    $s.Write($bytes, 0, $bytes.Length)
    $s.Close()
    $resp = $req.GetResponse()
    $sr = New-Object System.IO.StreamReader($resp.GetResponseStream())
    $txt = $sr.ReadToEnd()
    $resp.Close()
    return $txt
}

for ($i = 1; $i -le 12; $i++) {
    $pending = $false
    foreach ($t in @($tasks)) {
        try {
            $txt = Query-One $t.task_id
            $o = $txt | ConvertFrom-Json
            $st = $o.status
            if (-not $st) { $st = ($o | ConvertTo-Json -Depth 4) }
            if ($st -eq 'PENDING' -or $st -eq 'RUNNING') { $pending = $true }
            $tail = ''
            if ($o.audio_url) { $tail = ' url=' + $o.audio_url }
            elseif ($o.url) { $tail = ' url=' + $o.url }
            Write-Output ('[' + $i + '] ' + $t.name + ' :: ' + $st + $tail)
            if ($tail -ne '') {
                $o | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath ('D:\MimoGame\logs\bgm-' + $t.name + '-result.json') -Encoding UTF8
            }
        }
        catch {
            $msg = $_.Exception.Message
            if ($_.Exception.Response) {
                $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $msg = $sr.ReadToEnd()
            }
            Write-Output ('[' + $i + '] ' + $t.name + ' :: ERR ' + $msg.Substring(0, [Math]::Min(200, $msg.Length)))
        }
    }
    if (-not $pending) { Write-Output 'ALL_TERMINAL'; break }
    Start-Sleep -Seconds 30
}
