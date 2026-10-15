param([Parameter(Mandatory)][string]$Output, [switch]$Repack)
$ErrorActionPreference = 'Stop'
$outputPath = [System.IO.Path]::GetFullPath($Output)
$loader = Get-ChildItem -LiteralPath (Join-Path $outputPath 'Build') -Filter '*.loader.js' -File
if ($loader.Count -ne 1) { throw "Expected one Unity loader in $outputPath" }
$prefix = $loader[0].Name -replace '\.loader\.js$', ''
$report = Get-Content -LiteralPath (Join-Path $outputPath 'build-report.json') -Raw | ConvertFrom-Json
if ($report.result -ne 'Succeeded') { throw "Build report is not successful: $outputPath" }
$template = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'WebShell.html') -Raw
$html = $template.Replace('{{BUILD_PREFIX}}', $prefix).Replace('{{PRODUCT_VERSION}}', $report.version)
[System.IO.File]::WriteAllText((Join-Path $outputPath 'index.html'), $html, [System.Text.UTF8Encoding]::new($false))
if ($Repack) {
    $zipPath = $outputPath + '.zip'
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($outputPath, $zipPath,
        [System.IO.Compression.CompressionLevel]::Optimal, $false)
}
Write-Output (Join-Path $outputPath 'index.html')
