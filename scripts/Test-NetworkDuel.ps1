param(
    [Parameter(Mandatory)][string]$Executable,
    [int]$TimeoutSeconds = 420,
    [switch]$Disconnect,
    [switch]$DisconnectHost,
    [switch]$Visual,
    [int]$Width = 1280,
    [int]$Height = 720
)
$ErrorActionPreference = 'Stop'
if ($Disconnect -and $DisconnectHost) { throw 'Choose only one disconnect direction.' }
$exePath = [System.IO.Path]::GetFullPath($Executable)
if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) { throw "Executable not found: $exePath" }
$workspace = Split-Path $PSScriptRoot
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$evidence = Join-Path $workspace ('logs\network-duel-' + $stamp + $(if ($DisconnectHost) { '-host-disconnect' } elseif ($Disconnect) { '-disconnect' } else { '' }))
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$codeFile = Join-Path $evidence 'invite-code.txt'
$hostLog = Join-Path $evidence 'host.log'
$guestLog = Join-Path $evidence 'guest.log'
$hostTrace = Join-Path $evidence 'host.trace'
$guestTrace = Join-Path $evidence 'guest.trace'
$hostProcess = $null
$guestProcess = $null
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
try {
    $headless = if ($Visual) { @('-batchmode', '-screen-width', "$Width", '-screen-height', "$Height") } else { @('-batchmode', '-nographics') }
    $hostProcess = Start-Process -FilePath $exePath -ArgumentList ($headless + @('-logFile', $hostLog, '-diceNetHost', $codeFile)) -WindowStyle Hidden -PassThru
    while (-not (Test-Path -LiteralPath $codeFile) -and (Get-Date) -lt $deadline -and -not $hostProcess.HasExited) { Start-Sleep -Milliseconds 500 }
    if (-not (Test-Path -LiteralPath $codeFile)) { throw "Host did not create a join code. Evidence: $hostLog" }
    $guestProcess = Start-Process -FilePath $exePath -ArgumentList ($headless + @('-logFile', $guestLog, '-diceNetJoin', $codeFile)) -WindowStyle Hidden -PassThru
    if ($DisconnectHost) {
        while ((Get-Date) -lt $deadline -and -not $hostProcess.HasExited -and -not $guestProcess.HasExited -and
            -not ((Test-Path -LiteralPath $hostTrace) -and (Select-String -Path $hostTrace -Pattern 'DICE_NET_SMOKE player=0 round=1' -Quiet) -and
                  (Test-Path -LiteralPath $guestTrace) -and (Select-String -Path $guestTrace -Pattern 'DICE_NET_SMOKE player=1 round=1' -Quiet))) { Start-Sleep -Milliseconds 500 }
        if ($hostProcess.HasExited -or $guestProcess.HasExited) { throw "Client exited before host disconnect check. Evidence: $evidence" }
        Stop-Process -Id $hostProcess.Id -Force
        while ((Get-Date) -lt $deadline -and -not $guestProcess.HasExited) { Start-Sleep -Milliseconds 500 }
        if (-not $guestProcess.HasExited -or $guestProcess.ExitCode -ne 3 -or
            -not (Select-String -Path $guestTrace -Pattern 'DICE_NET_SMOKE INTERRUPTED' -Quiet)) {
            throw "Guest did not stop on host disconnect. Evidence: $evidence"
        }
        [ordered]@{ result='Passed'; scenario='host-disconnect'; guestExit=$guestProcess.ExitCode; evidence=$evidence } | ConvertTo-Json
        return
    }
    if ($Disconnect) {
        while ((Get-Date) -lt $deadline -and -not $guestProcess.HasExited -and
            -not ((Test-Path -LiteralPath $hostTrace) -and (Select-String -Path $hostTrace -Pattern 'DICE_NET_SMOKE player=0 round=1' -Quiet)) -and
            -not ((Test-Path -LiteralPath $guestTrace) -and (Select-String -Path $guestTrace -Pattern 'DICE_NET_SMOKE player=1 round=1' -Quiet))) { Start-Sleep -Milliseconds 500 }
        if ($guestProcess.HasExited) { throw "Guest exited before disconnect check. Evidence: $guestTrace" }
        Stop-Process -Id $guestProcess.Id -Force
    }
    while ((Get-Date) -lt $deadline -and (-not $hostProcess.HasExited -or (-not $Disconnect -and -not $guestProcess.HasExited))) { Start-Sleep -Milliseconds 500 }
    if (-not $hostProcess.HasExited -or (-not $Disconnect -and -not $guestProcess.HasExited)) { throw "Network duel timed out. Evidence: $evidence" }
    if (-not (Test-Path -LiteralPath $hostTrace)) { throw "Host trace missing. Evidence: $evidence" }
    if (-not $Disconnect -and -not (Test-Path -LiteralPath $guestTrace)) { throw "Guest trace missing. Evidence: $evidence" }
    if (-not $Disconnect -and -not (Select-String -Path $guestTrace -Pattern 'DICE_NET_SMOKE invite code entered through clickable UI' -Quiet)) {
        throw "Guest did not type the invite code through the visible input field. Evidence: $evidence"
    }
    $hostComplete = Select-String -Path $hostTrace -Pattern 'DICE_NET_SMOKE COMPLETE winner=(\d+)' | Select-Object -Last 1
    $guestComplete = if (Test-Path -LiteralPath $guestTrace) { Select-String -Path $guestTrace -Pattern 'DICE_NET_SMOKE COMPLETE winner=(\d+)' | Select-Object -Last 1 }
    $hostInterrupted = Select-String -Path $hostTrace -Pattern 'DICE_NET_SMOKE INTERRUPTED' -Quiet
    if ($Disconnect) {
        if (-not $hostInterrupted -or $hostProcess.ExitCode -ne 3) { throw "Host did not stop on guest disconnect. Evidence: $evidence" }
        [ordered]@{ result='Passed'; scenario='guest-disconnect'; hostExit=$hostProcess.ExitCode; evidence=$evidence } | ConvertTo-Json
    }
    else {
        $hostRounds = @(Select-String -Path $hostTrace -Pattern 'DICE_NET_SMOKE player=0 round=(\d+) phase=(\d+) hp=(\d+),(\d+)' | ForEach-Object { $_.Matches[0].Value -replace 'player=0', 'player=*' })
        $guestRounds = @(Select-String -Path $guestTrace -Pattern 'DICE_NET_SMOKE player=1 round=(\d+) phase=(\d+) hp=(\d+),(\d+)' | ForEach-Object { $_.Matches[0].Value -replace 'player=1', 'player=*' })
        if (-not $hostComplete -or -not $guestComplete -or $hostProcess.ExitCode -ne 0 -or $guestProcess.ExitCode -ne 0 -or
            $hostComplete.Matches[0].Groups[1].Value -ne $guestComplete.Matches[0].Groups[1].Value -or
            $hostRounds.Count -lt 2 -or ($hostRounds -join '|') -ne ($guestRounds -join '|')) {
            throw "Host and guest did not agree on rounds, HP, or winner. Evidence: $evidence"
        }
        [ordered]@{ result='Passed'; scenario='full-match'; winner=[int]$hostComplete.Matches[0].Groups[1].Value;
            synchronizedStates=$hostRounds.Count; hostExit=$hostProcess.ExitCode; guestExit=$guestProcess.ExitCode; evidence=$evidence } | ConvertTo-Json
    }
}
finally {
    if ($hostProcess -and -not $hostProcess.HasExited) { Stop-Process -Id $hostProcess.Id -Force }
    if ($guestProcess -and -not $guestProcess.HasExited) { Stop-Process -Id $guestProcess.Id -Force }
}
