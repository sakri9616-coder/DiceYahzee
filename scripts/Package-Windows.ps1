param([Parameter(Mandatory)][string]$BuildDirectory)
$ErrorActionPreference = 'Stop'

$build = [System.IO.Path]::GetFullPath($BuildDirectory).TrimEnd('\', '/')
$required = @(
    'DiceYahzee.exe',
    'UnityPlayer.dll',
    'DiceYahzee_Data\data.unity3d',
    'MonoBleedingEdge\EmbedRuntime\mono-2.0-bdwgc.dll',
    'MonoBleedingEdge\EmbedRuntime\MonoPosixHelper.dll',
    'MonoBleedingEdge\etc\mono\config'
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $build $relative) -PathType Leaf)) {
        throw "Windows build is incomplete: $relative"
    }
}

$archivePath = "$build.zip"
if (Test-Path -LiteralPath $archivePath) { throw "Archive already exists: $archivePath" }
# Windows PowerShell 5.1 needs the base assembly first: loading only
# System.IO.Compression.FileSystem leaves ZipArchiveMode unresolved.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($archivePath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in (Get-ChildItem -LiteralPath $build -Recurse -File)) {
        $relative = $file.FullName.Substring($build.Length).TrimStart('\', '/')
        if ($relative -match '(^|[\\/])DiceYahzee_BurstDebugInformation_DoNotShip([\\/]|$)') { continue }
        $entry = $relative.Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $file.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose() }

$check = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    foreach ($relative in $required) {
        if (-not $check.GetEntry($relative.Replace('\', '/'))) {
            throw "Archive is missing required runtime content: $relative"
        }
    }
    $entryCount = $check.Entries.Count
}
finally { $check.Dispose() }

[ordered]@{ archive=$archivePath; bytes=(Get-Item -LiteralPath $archivePath).Length;
    entries=$entryCount; monoBleedingEdge='included' } | ConvertTo-Json
