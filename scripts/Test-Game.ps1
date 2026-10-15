param(
    [ValidateSet('EditMode', 'PlayMode')][string]$Platform = 'EditMode',
    [string]$Filter = '',
    [string]$ProjectPath = '',
    [int]$TimeoutSeconds = 900
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot
$project = Join-Path $workspace 'ParvaMirabiliaDice'
if ($ProjectPath) { $project = [System.IO.Path]::GetFullPath($ProjectPath) }
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
$runStamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$base = Join-Path $workspace ('logs\validation-' + $Platform.ToLowerInvariant() + '-' + $runStamp)
$results = $base + '.xml'
$log = $base + '.log'
$arguments = @('-batchmode', '-projectPath', $project, '-runTests', '-testPlatform', $Platform,
    '-testResults', $results, '-logFile', $log)
if ($Filter) { $arguments += @('-testFilter', $Filter) }
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    throw "Unity test timeout. Evidence: $log. The Unity process was preserved; PID=$($process.Id)."
}
if (-not (Test-Path -LiteralPath $results)) { throw "Unity exit=$($process.ExitCode); missing test results. Log: $log" }
[xml]$xml = Get-Content -LiteralPath $results -Raw
$suite = $xml.'test-run'
$summary = [ordered]@{ platform=$Platform; total=[int]$suite.total; passed=[int]$suite.passed;
    failed=[int]$suite.failed; skipped=[int]$suite.skipped; result=[string]$suite.result;
    exitCode=$process.ExitCode; results=$results; log=$log }
$summary | ConvertTo-Json | Write-Output
foreach ($case in $xml.SelectNodes("//test-case[@result='Failed']")) {
    Write-Output $case.fullname
    Write-Output $case.failure.message.InnerText
}
if ($process.ExitCode -ne 0 -or [int]$suite.total -eq 0 -or [int]$suite.failed -gt 0 -or $suite.result -ne 'Passed') {
    exit 1
}
exit 0
