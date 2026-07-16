param(
    [Parameter(Mandatory = $true)]
    [string]$CoverageFile,

    [string]$SummaryFile,

    [ValidateRange(0, 1)]
    [double]$MinimumLineRate = 0,

    [ValidateRange(0, 1)]
    [double]$MinimumBranchRate = 0
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $CoverageFile -PathType Leaf)) {
    throw "Coverage report was not found: $CoverageFile"
}

[xml]$document = Get-Content -LiteralPath $CoverageFile -Raw
$coverage = $document.coverage
if ($null -eq $coverage) {
    throw 'The coverage report is not valid Cobertura XML.'
}

$culture = [System.Globalization.CultureInfo]::InvariantCulture
$lineRate = [double]::Parse([string]$coverage.'line-rate', $culture)
$branchRate = [double]::Parse([string]$coverage.'branch-rate', $culture)
$linesCovered = [int]$coverage.'lines-covered'
$linesValid = [int]$coverage.'lines-valid'
$branchesCovered = [int]$coverage.'branches-covered'
$branchesValid = [int]$coverage.'branches-valid'

$linePercent = $lineRate.ToString('P1', $culture)
$branchPercent = $branchRate.ToString('P1', $culture)
$summary = @"
## Code coverage

| Metric | Coverage | Details |
| --- | ---: | ---: |
| Lines | $linePercent | $linesCovered / $linesValid |
| Branches | $branchPercent | $branchesCovered / $branchesValid |
"@

Write-Output $summary
if (-not [string]::IsNullOrWhiteSpace($SummaryFile)) {
    Add-Content -LiteralPath $SummaryFile -Value $summary
}

if ($lineRate -lt $MinimumLineRate) {
    $minimumPercent = $MinimumLineRate.ToString('P1', $culture)
    throw "Line coverage $linePercent is below the required $minimumPercent baseline."
}

if ($branchRate -lt $MinimumBranchRate) {
    $minimumPercent = $MinimumBranchRate.ToString('P1', $culture)
    throw "Branch coverage $branchPercent is below the required $minimumPercent baseline."
}
