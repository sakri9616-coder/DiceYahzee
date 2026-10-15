# Compatibility entry point: preserve run evidence and fail on missing or zero results.
param([string]$ProjectPath = '')
& (Join-Path $PSScriptRoot 'Test-Game.ps1') -Platform EditMode -ProjectPath $ProjectPath
exit $LASTEXITCODE
