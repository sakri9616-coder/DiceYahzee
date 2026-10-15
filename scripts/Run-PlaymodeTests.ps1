# Compatibility entry point: preserve run evidence and fail on missing or zero results.
param([string]$ProjectPath = '')
& (Join-Path $PSScriptRoot 'Test-Game.ps1') -Platform PlayMode -ProjectPath $ProjectPath
exit $LASTEXITCODE
