# Turns the measured screen geometry (logs/screen-*.json, written by ScreenDumpTests) into an
# HTML sheet so the layout can be *looked at* instead of asserted at.
#
#   powershell -File scripts/RenderScreens.ps1
#
# Writes logs/screens.html (all screens on one sheet) plus one logs/screen-<name>.html per
# screen, which is what gets screenshotted at full size when the details matter.

$ErrorActionPreference = 'Stop'

$logsDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\logs'))

$order = @('title', 'battle', 'battle-marked', 'rules')
$files = @()
foreach ($name in $order) {
    $path = Join-Path $logsDir "screen-$name.json"
    if (Test-Path $path) { $files += $path }
}

if ($files.Count -eq 0) {
    Write-Error "no logs/screen-*.json found - run scripts/Run-PlaymodeTests.ps1 first"
}

$alignMap = @{
    'UpperLeft'    = 'flex-start|flex-start'
    'UpperCenter'  = 'center|flex-start'
    'UpperRight'   = 'flex-end|flex-start'
    'MiddleLeft'   = 'flex-start|center'
    'MiddleCenter' = 'center|center'
    'MiddleRight'  = 'flex-end|center'
    'LowerLeft'    = 'flex-start|flex-end'
    'LowerCenter'  = 'center|flex-end'
    'LowerRight'   = 'flex-end|flex-end'
}

$style = @'
<style>
  body { margin:0; padding:28px; background:#08080f; color:#8a8aa0;
         font:13px/1.5 "Segoe UI", sans-serif; }
  body.tight { padding:10px; }
  h2 { margin:34px 0 10px; font-size:15px; letter-spacing:.14em;
       text-transform:uppercase; color:#e8c87a; font-weight:600; }
  h2 small { color:#6a6a86; text-transform:none; letter-spacing:0;
             font-weight:400; margin-left:12px; }
  .stage { position:relative; width:1280px; height:720px; overflow:hidden;
           background:#12122A; box-shadow:0 0 0 1px #2a2a44, 0 18px 60px #000a;
           font-family:"Yu Gothic UI","Yu Gothic",Meiryo,sans-serif; }
  .el { position:absolute; box-sizing:border-box; }
  .tx { position:absolute; inset:0; box-sizing:border-box; display:flex;
        white-space:pre-wrap; overflow:hidden; }
  .nowrap .tx { white-space:pre; }
  .guide { position:absolute; inset:0; pointer-events:none; }
  .guide:before, .guide:after { content:""; position:absolute; background:#ff2d6a55; }
  .guide:before { left:640px; top:0; width:1px; height:100%; }
  .guide:after  { top:360px; left:0; height:1px; width:100%; }
  .edge { position:absolute; inset:0; pointer-events:none;
          box-shadow:inset 0 0 0 1px #e8c87a44; }
</style>
'@

function Convert-ElementToHtml($el) {
    if (-not $el.visible) { return '' }

    # Canvas space is y-up from the centre; HTML is y-down from the top-left.
    $left = [double]$el.x + 640
    $top  = 360 - ([double]$el.y + [double]$el.h)

    $css = "left:$([math]::Round($left,2))px;top:$([math]::Round($top,2))px;" +
           "width:$([math]::Round([double]$el.w,2))px;height:$([math]::Round([double]$el.h,2))px;"

    $hasFill = ($null -ne $el.PSObject.Properties['fill']) -and $el.imageOn
    if ($hasFill) {
        $css += "background:$($el.fill);"
        if ($el.sprite -eq 'Circle') { $css += 'border-radius:50%;' }
    }

    $opacity = 1.0
    if ($null -ne $el.PSObject.Properties['groupAlpha']) { $opacity = [double]$el.groupAlpha }
    if ($opacity -lt 1.0) { $css += "opacity:$opacity;" }

    $classes = 'el'
    if ($null -ne $el.PSObject.Properties['wrap'] -and $el.wrap -eq 'NoWrap') {
        $classes += ' nowrap'
    }

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("  <div class=`"$classes`" style=`"$css`" title=`"$($el.path)`">")

    $hasText = ($null -ne $el.PSObject.Properties['text']) -and $el.textOn -and
               -not [string]::IsNullOrEmpty($el.text)
    if ($hasText) {
        $key = 'MiddleCenter'
        if ($null -ne $el.PSObject.Properties['align']) { $key = [string]$el.align }
        if (-not $alignMap.ContainsKey($key)) { $key = 'MiddleCenter' }
        $pair = $alignMap[$key] -split '\|'

        $t = "font-size:$($el.fontSize)px;color:$($el.fontColor);" +
             "justify-content:$($pair[0]);align-items:$($pair[1]);"
        if ([double]$el.lineSpacing -gt 0) {
            $t += "line-height:$([math]::Round([double]$el.lineSpacing * 1.2,3));"
        }
        if ($el.bestFit) { $t += 'font-size:14px;' }

        $body = [string]$el.text
        $body = $body -replace '&', '&amp;' -replace '<', '&lt;' -replace '>', '&gt;'
        [void]$sb.AppendLine("    <div class=`"tx`" style=`"$t`">$body</div>")
    }

    [void]$sb.AppendLine('  </div>')
    return $sb.ToString()
}

$all = New-Object System.Text.StringBuilder
[void]$all.AppendLine('<!doctype html>')
[void]$all.AppendLine('<html lang="ja"><head><meta charset="utf-8">')
[void]$all.AppendLine('<title>ARCANE DUEL - measured screen geometry</title>')
[void]$all.AppendLine($style)
[void]$all.AppendLine('</head><body>')

foreach ($file in $files) {
    $doc = Get-Content -Raw -Encoding UTF8 $file | ConvertFrom-Json
    $title = $doc.screen

    $stage = New-Object System.Text.StringBuilder
    [void]$stage.AppendLine('<div class="stage">')
    foreach ($el in $doc.elements) {
        [void]$stage.Append((Convert-ElementToHtml $el))
    }
    [void]$stage.AppendLine('<div class="guide"></div><div class="edge"></div>')
    [void]$stage.AppendLine('</div>')

    $heading = "<h2>$title <small>$($doc.elements.Count) elements - canvas " +
               "$($doc.canvas.width) x $($doc.canvas.height)</small></h2>"
    [void]$all.AppendLine($heading)
    [void]$all.AppendLine($stage.ToString())

    # One screen per file, at full size, so a screenshot of it shows the detail that matters.
    $one = New-Object System.Text.StringBuilder
    [void]$one.AppendLine('<!doctype html>')
    [void]$one.AppendLine('<html lang="ja"><head><meta charset="utf-8">')
    [void]$one.AppendLine("<title>$title</title>")
    [void]$one.AppendLine($style)
    [void]$one.AppendLine('</head><body class="tight">')
    [void]$one.AppendLine($stage.ToString())
    [void]$one.AppendLine('</body></html>')
    [System.IO.File]::WriteAllText((Join-Path $logsDir "view-$title.html"),
        $one.ToString(), (New-Object System.Text.UTF8Encoding($false)))
}

[void]$all.AppendLine('</body></html>')
[System.IO.File]::WriteAllText((Join-Path $logsDir 'screens.html'),
    $all.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Output "wrote $(Join-Path $logsDir 'screens.html') and $($files.Count) view-*.html"
