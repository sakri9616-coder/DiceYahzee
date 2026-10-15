param([int]$TimeoutSeconds = 1800, [string]$ProjectPath = '', [switch]$SkipZip)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
$project = Join-Path $workspace 'ParvaMirabiliaDice'
if ($ProjectPath) { $project = [System.IO.Path]::GetFullPath($ProjectPath) }
if ($TimeoutSeconds -le 0) { throw 'TimeoutSeconds must be positive.' }
if (-not (Test-Path -LiteralPath $unity -PathType Leaf)) { throw "Unity not found: $unity" }
if (-not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings\ProjectVersion.txt'))) {
    throw "Unity project not found: $project"
}
$runStamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$logDirectory = Join-Path $workspace 'logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$log = Join-Path $logDirectory ('build-web-' + $runStamp + '.log')
$output = Join-Path $workspace ('Builds\Web-' + $runStamp)
# Quote paths because Unity/project locations can contain spaces. Start-Process
# joins ArgumentList into a command line rather than preserving argument boundaries.
$arguments = @('-batchmode', '-quit', '-buildTarget', 'WebGL', '-projectPath',
    ('"' + $project + '"'), '-executeMethod', 'ParvaMirabilia.Editor.ReleaseBuild.Web',
    '-arcaneBuildPath', ('"' + $output + '"'), '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    throw "Build timeout. Unity may still be running; PID=$($process.Id). Log: $log"
}
$process.Refresh()
$reportPath = Join-Path $output 'build-report.json'
$index = Join-Path $output 'index.html'
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath) -or -not (Test-Path -LiteralPath $index)) {
    throw "Web build failed, exit=$($process.ExitCode). Log: $log"
}
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($report.result -ne 'Succeeded' -or $report.errors -ne 0 -or $report.target -ne 'WebGL' -or $report.compression -ne 'Disabled') {
    throw "Web build report failed: $reportPath"
}
$buildDirectory = Join-Path $output 'Build'
foreach ($pattern in @('*.loader.js', '*.framework.js', '*.data', '*.wasm')) {
    if (-not (Get-ChildItem -LiteralPath $buildDirectory -Filter $pattern -File)) {
        throw "Web build is incomplete: missing $pattern in $buildDirectory"
    }
}
& (Join-Path $PSScriptRoot 'Format-WebBuild.ps1') -Output $output | Out-Null
Copy-Item -LiteralPath (Join-Path $workspace 'docs\ASSET_LICENSES.md') -Destination (Join-Path $output 'ASSET_LICENSES.md')
$licenseOutput = Join-Path $output 'Licenses'
New-Item -ItemType Directory -Path $licenseOutput -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $workspace 'ThirdParty\CREDITS.md') -Destination $licenseOutput
Copy-Item -LiteralPath (Join-Path $workspace 'ThirdParty\AoboshiOne\OFL.txt') -Destination (Join-Path $licenseOutput 'AoboshiOne-OFL.txt')
Copy-Item -LiteralPath (Join-Path $workspace 'ThirdParty\NotoSansJP\LICENSE.txt') -Destination (Join-Path $licenseOutput 'NotoSansJP-OFL.txt')
foreach ($pack in @('Kenney-FantasyUI', 'Kenney-UIPack', 'Kenney-InputPrompts', 'Kenney-GameIcons')) {
    Copy-Item -LiteralPath (Join-Path $workspace "ThirdParty\$pack\LICENSE.txt") -Destination (Join-Path $licenseOutput "$pack-CC0.txt")
}
Copy-Item -LiteralPath (Join-Path $workspace 'ThirdParty\DiceYahzeeAssets.json') -Destination $licenseOutput
$zipPath = $null
if (-not $SkipZip) {
    $zipPath = $output + '.zip'
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $output -File -Recurse) {
            $entryName = $file.FullName.Substring($output.Length + 1).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName,
                $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally { $zip.Dispose() }
}
[ordered]@{ directory=$output; index=$index; zip=$zipPath; report=$reportPath; log=$log; result=$report.result } |
    ConvertTo-Json | Write-Output
