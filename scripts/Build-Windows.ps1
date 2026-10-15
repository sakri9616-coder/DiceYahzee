param([int]$TimeoutSeconds = 1200, [string]$ProjectPath = '')
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
$project = Join-Path $workspace 'ParvaMirabiliaDice'
if ($ProjectPath) { $project = [System.IO.Path]::GetFullPath($ProjectPath) }
$runStamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$log = Join-Path $workspace ('logs\build-windows-' + $runStamp + '.log')
$output = Join-Path $workspace ('Builds\Windows-' + $runStamp)
$exe = Join-Path $output 'DiceYahzee.exe'
$process = Start-Process -FilePath $unity -ArgumentList @('-batchmode', '-quit', '-projectPath',
    $project, '-executeMethod', 'ParvaMirabilia.Editor.ReleaseBuild.Windows',
    '-arcaneBuildPath', $exe, '-logFile', $log) -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { throw "Build timeout. PID=$($process.Id); evidence: $log" }
$reportPath = Join-Path $output 'build-report.json'
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath) -or -not (Test-Path -LiteralPath $exe)) {
    throw "Build failed, exit=$($process.ExitCode). Log: $log"
}
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($report.result -ne 'Succeeded' -or $report.errors -ne 0) { throw "Build report failed: $reportPath" }
Copy-Item -LiteralPath (Join-Path $workspace 'README.md') -Destination (Join-Path $output 'README.md')
Copy-Item -LiteralPath (Join-Path $workspace 'docs\ASSET_LICENSES.md') -Destination (Join-Path $output 'ASSET_LICENSES.md')
$licenseOutput = Join-Path $output 'Licenses'
New-Item -ItemType Directory -Path $licenseOutput -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $workspace 'ThirdParty\CREDITS.md') -Destination (Join-Path $licenseOutput 'CREDITS.md')
Copy-Item -LiteralPath (Join-Path $workspace 'ThirdParty\AoboshiOne\OFL.txt') -Destination (Join-Path $licenseOutput 'AoboshiOne-OFL.txt')
Copy-Item -LiteralPath (Join-Path $workspace 'ThirdParty\NotoSansJP\LICENSE.txt') -Destination (Join-Path $licenseOutput 'NotoSansJP-OFL.txt')
Copy-Item -LiteralPath (Join-Path $workspace 'ThirdParty\Kenney-FantasyUI\LICENSE.txt') -Destination (Join-Path $licenseOutput 'Kenney-CC0.txt')
foreach ($pack in @('Kenney-UIPack', 'Kenney-InputPrompts', 'Kenney-GameIcons')) {
    Copy-Item -LiteralPath (Join-Path $workspace "ThirdParty\$pack\LICENSE.txt") -Destination (Join-Path $licenseOutput "$pack-CC0.txt")
}
Copy-Item -LiteralPath (Join-Path $workspace 'ThirdParty\DiceYahzeeAssets.json') -Destination $licenseOutput
[ordered]@{ executable=$exe; report=$reportPath; log=$log; result=$report.result } | ConvertTo-Json | Write-Output
