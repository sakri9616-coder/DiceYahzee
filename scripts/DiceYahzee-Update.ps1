param(
    [Parameter(Mandatory)][string]$Archive,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedSha,
    [Parameter(Mandatory)][string]$InstallDirectory,
    [Parameter(Mandatory)][int]$WaitPid,
    [Parameter(Mandatory)][string]$LogPath,
    [switch]$SkipRestart
)
$ErrorActionPreference = 'Stop'

function Write-UpdateLog([string]$Message) {
    Add-Content -LiteralPath $LogPath -Value ((Get-Date -Format o) + ' ' + $Message)
}

# This helper is copied outside the install directory before the game exits. No token or
# authenticated URL crosses the process boundary. The ZIP and its digest were verified in-game;
# checking again protects the interval between download and replacement.
try {
    $archivePath = [IO.Path]::GetFullPath($Archive)
    $installPath = [IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\', '/')
    $logPath = [IO.Path]::GetFullPath($LogPath)
    if (-not (Test-Path -LiteralPath $installPath -PathType Container)) { throw 'Install directory is missing' }
    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) { throw 'Update archive is missing' }
    if ($WaitPid -le 0) { throw 'Invalid game process ID' }

    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        if (-not (Get-Process -Id $WaitPid -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Milliseconds 500
    }
    if (Get-Process -Id $WaitPid -ErrorAction SilentlyContinue) { throw 'Game did not exit in 60 seconds' }
    if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $ExpectedSha.ToUpperInvariant()) {
        throw 'SHA-256 mismatch'
    }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $work = Join-Path (Split-Path $archivePath) ('install-' + [Guid]::NewGuid().ToString('N'))
    $extract = Join-Path $work 'extracted'
    $backup = Join-Path $work 'backup'
    New-Item -ItemType Directory -Path $extract, $backup -Force | Out-Null
    $zip = [IO.Compression.ZipFile]::OpenRead($archivePath)
    $files = New-Object 'System.Collections.Generic.List[string]'
    try {
        foreach ($entry in $zip.Entries) {
            $relative = $entry.FullName.Replace('\', '/')
            if (-not $relative -or $relative.EndsWith('/')) { continue }
            $parts = $relative.Split('/')
            if ($relative.StartsWith('/') -or $relative.Contains(':') -or
                @($parts | Where-Object { $_ -eq '' -or $_ -eq '.' -or $_ -eq '..' }).Count -gt 0) {
                throw 'Unsafe ZIP path'
            }
            # Unix symlinks are not regular files. Refuse them even when Windows extraction
            # would otherwise interpret them as plain files.
            if ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) { throw 'ZIP link refused' }
            if ($files.Contains($relative)) { throw 'Duplicate ZIP entry' }
            $target = Join-Path $extract ($relative.Replace('/', '\'))
            $folder = Split-Path $target
            New-Item -ItemType Directory -Path $folder -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $false)
            $files.Add($relative)
        }
    }
    finally { $zip.Dispose() }
    foreach ($required in @('DiceYahzee.exe', 'UnityPlayer.dll', 'DiceYahzee_Data/data.unity3d',
                             'DiceYahzee-Update.ps1')) {
        if (-not $files.Contains($required)) { throw "Incomplete update ZIP: $required" }
    }

    # Back up only files named by the update; never rename an arbitrary folder that the user
    # might have extracted the first ZIP into alongside unrelated files.
    $existing = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($relative in $files) {
        $destination = Join-Path $installPath ($relative.Replace('/', '\'))
        if (Test-Path -LiteralPath $destination -PathType Leaf) {
            $saved = Join-Path $backup ($relative.Replace('/', '\'))
            New-Item -ItemType Directory -Path (Split-Path $saved) -Force | Out-Null
            Copy-Item -LiteralPath $destination -Destination $saved
            [void]$existing.Add($relative)
        }
    }

    $touched = New-Object 'System.Collections.Generic.List[string]'
    try {
        foreach ($relative in $files) {
            $destination = Join-Path $installPath ($relative.Replace('/', '\'))
            $source = Join-Path $extract ($relative.Replace('/', '\'))
            New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
            $touched.Add($relative)
            Copy-Item -LiteralPath $source -Destination $destination -Force
        }
    }
    catch {
        foreach ($relative in $touched) {
            $destination = Join-Path $installPath ($relative.Replace('/', '\'))
            if ($existing.Contains($relative)) {
                Copy-Item -LiteralPath (Join-Path $backup ($relative.Replace('/', '\'))) -Destination $destination -Force
            }
            elseif (Test-Path -LiteralPath $destination) {
                Remove-Item -LiteralPath $destination -Force
            }
        }
        throw
    }
    Write-UpdateLog "Installed verified update; backup retained at $backup"
    if (-not $SkipRestart) {
        Start-Process -FilePath (Join-Path $installPath 'DiceYahzee.exe') -WorkingDirectory $installPath -WindowStyle Hidden
    }
}
catch {
    try { Write-UpdateLog ('Update failed: ' + $_.Exception.Message) } catch { }
    exit 1
}
