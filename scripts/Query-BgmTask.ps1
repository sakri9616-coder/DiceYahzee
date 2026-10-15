$cli = 'C:\Users\sakur\AppData\Roaming\ai.opencode.desktop\cli\2.0.26\opencode-cli.exe'
$raw = (& $cli auth export 2>&1 | Out-String)
$j = $raw | ConvertFrom-Json
if ($j -isnot [array]) { $j = @($j) }
$key = ($j | Where-Object { $_.integrationID -eq 'stepfun' } | Select-Object -First 1).value.key
$tasks = Get-Content -LiteralPath 'D:\MimoGame\logs\bgm-tasks.json' -Raw | ConvertFrom-Json
foreach ($t in @($tasks)) {
    $body = '{"task_id":"' + $t.task_id + '"}'
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
    try {
        $resp = $req.GetResponse()
        $sr = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $txt = $sr.ReadToEnd()
        $resp.Close()
        if ($txt.Length -gt 400) { $txt = $txt.Substring(0, 400) }
        Write-Output ($t.name + " :: " + $txt)
    }
    catch [System.Net.WebException] {
        $er = $_.Exception.Response
        if ($er) {
            $sr = New-Object System.IO.StreamReader($er.GetResponseStream())
            $txt = $sr.ReadToEnd()
            if ($txt.Length -gt 400) { $txt = $txt.Substring(0, 400) }
            Write-Output ($t.name + " :: ERR " + $er.StatusCode.value__ + " " + $txt)
        }
        else {
            Write-Output ($t.name + " :: EXC " + $_.Exception.Message)
        }
    }
}
