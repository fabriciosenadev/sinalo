param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$coverageOutput = Join-Path $projectRoot "TestResults\coverage"

$solutionPath = Join-Path $projectRoot "Sinalo.slnx"

dotnet test $solutionPath `
    --configuration $Configuration `
    /p:CollectCoverage=true `
    /p:CoverletOutputFormat=cobertura `
    /p:CoverletOutput=$coverageOutput `
    /p:Threshold=75 `
    /p:ThresholdType=line `
    /p:ThresholdStat=total

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

dotnet test $solutionPath `
    --configuration $Configuration `
    /p:CollectCoverage=true `
    /p:CoverletOutputFormat=cobertura `
    /p:CoverletOutput=$coverageOutput `
    /p:Threshold=75 `
    /p:ThresholdType=branch `
    /p:ThresholdStat=total

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
