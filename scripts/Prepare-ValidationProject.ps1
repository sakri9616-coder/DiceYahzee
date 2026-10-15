$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot
$source = Join-Path $workspace 'ParvaMirabiliaDice'
$destination = Join-Path $workspace '.validation\ParvaMirabiliaDice'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($directory in @('Assets','Packages','ProjectSettings')) {
    $target = Join-Path $destination $directory
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    & robocopy (Join-Path $source $directory) $target /E /XF 'InitTestScene*' /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -gt 7) { throw "Validation source copy failed: $directory, robocopy=$LASTEXITCODE" }
}
# Cached packages avoid a fresh download and preserve the versions already resolved locally.
$cache = Join-Path $source 'Library\PackageCache'
if (Test-Path -LiteralPath $cache) {
    & robocopy $cache (Join-Path $destination 'Library\PackageCache') /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -gt 7) { throw 'Package cache copy failed.' }
}
Write-Output $destination
exit 0
