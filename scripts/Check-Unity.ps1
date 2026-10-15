# Shows each Unity process's command line so our project's batch run can be told apart
# from other agents' editors - only ours may ever be stopped.
$ErrorActionPreference = 'SilentlyContinue'
Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
    Select-Object ProcessId, CreationDate, @{ n = 'Cmd'; e = { $_.CommandLine } } |
    ForEach-Object {
        Write-Output ("PID {0}  started {1}" -f $_.ProcessId, $_.CreationDate)
        Write-Output ("      " + $_.Cmd)
    }
