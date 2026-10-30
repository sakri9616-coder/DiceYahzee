# Prints a side-by-side summary of the two balance reports (formations off / on).
$ErrorActionPreference = 'Stop'
$files = @(
    @{ Tag = 'v1(off)'; Path = 'F:\MimoGame\logs\simulation-report.json' },
    @{ Tag = 'v2(on) '; Path = 'F:\MimoGame\logs\simulation-report-formations.json' }
)

$rows = @()
foreach ($f in $files) {
    if (-not (Test-Path -LiteralPath $f.Path)) { Write-Output ("MISSING " + $f.Path); continue }
    $j = Get-Content -LiteralPath $f.Path -Raw | ConvertFrom-Json
    $rows += [pscustomobject]@{
        Tag          = $f.Tag
        Rounds       = $j.averageRounds
        DmgPerMatch  = $j.damagePerMatch
        DrawShare    = $j.drawShare
        LimitShare   = $j.roundLimitShare
        Formations   = $j.formations
    }
}
$rows | Format-Table -AutoSize | Out-String -Width 200 | Write-Output

foreach ($f in $files) {
    if (-not (Test-Path -LiteralPath $f.Path)) { continue }
    $j = Get-Content -LiteralPath $f.Path -Raw | ConvertFrom-Json
    Write-Output ("--- " + $f.Tag + " pairings ---")
    foreach ($p in $j.pairings) {
        $avgR = ($p.a.averageRounds + $p.b.averageRounds) / 2
        $cash = ($p.a.formationCashedShare + $p.b.formationCashedShare) / 2
        $bon  = ($p.a.bonusDamageShare + $p.b.bonusDamageShare) / 2
        $atk  = ($p.a.mostUsedAttackShare + $p.b.mostUsedAttackShare) / 2
        Write-Output ("{0,-11} {1,-11} avgR={2,6:N2} cash={3,6:N3} bonusShare={4,6:N3} atkShare={5,6:N3}" -f `
            $p.lineA, $p.lineB, $avgR, $cash, $bon, $atk)
    }
    Write-Output ''
}
