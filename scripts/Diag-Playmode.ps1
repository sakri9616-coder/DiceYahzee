$ErrorActionPreference = 'Stop'
$l = 'D:\MimoGame\logs\playmode-run.log'
$lines = Get-Content -LiteralPath $l
Write-Output ('lines=' + $lines.Count)
# Show the interesting window: everything after the compile output that is not audio spam.
for ($i = 300; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -notlike '*audio listeners*') {
        Write-Output ($i.ToString() + ': ' + $line.Substring(0, [Math]::Min(200, $line.Length)))
    }
}
