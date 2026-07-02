param(
    [string]$BaseUrl = "http://localhost:62382",
    [string]$UserName = "admin",
    [string]$Password = "ChangeMe123!",
    [string]$OutputDir = "artifacts/visual"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$visualRoot = Join-Path $repoRoot "tests\visual"
$resolvedOutput = Join-Path $repoRoot $OutputDir

New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null

Push-Location $visualRoot
try {
    if (-not (Test-Path "node_modules")) {
        npm install
    }

    npx playwright install chromium

    $env:BASE_URL = $BaseUrl
    $env:VISUAL_USER = $UserName
    $env:VISUAL_PASSWORD = $Password
    $env:VISUAL_OUTPUT_DIR = $resolvedOutput

    npx playwright test
}
finally {
    Pop-Location
}
