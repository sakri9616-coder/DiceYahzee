Get-Process Unity -ErrorAction SilentlyContinue | ForEach-Object {
    [PSCustomObject]@{
        Id        = $_.Id
        Started   = $_.StartTime.ToString('HH:mm:ss')
        Title     = $_.MainWindowTitle
    }
} | Format-Table -AutoSize | Out-String -Width 100
