$ErrorActionPreference = 'Stop'
$dir = 'F:\MimoGame\logs'
$file = Get-ChildItem -LiteralPath $dir -Filter 'oplog-*.jsonl' |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Output ('file=' + $file.Name + ' bytes=' + $file.Length + ' at=' + $file.LastWriteTime)
$lines = Get-Content -LiteralPath $file.FullName
Write-Output ('lines=' + $lines.Count)
$lines | Select-Object -Last 30 | ForEach-Object { Write-Output $_ }
