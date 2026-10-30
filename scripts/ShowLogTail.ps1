param(
    [string]$Path = "F:\MimoGame\logs\test-run.log",
    [int]$Tail = 80
)

$lines = Get-Content -Path $Path -Encoding UTF8
Write-Output ("total lines: " + $lines.Count)
if ($lines.Count -gt $Tail) {
    $lines[($lines.Count - $Tail)..($lines.Count - 1)]
}
else {
    $lines
}
