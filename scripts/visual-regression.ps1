param(
    [string]$BaseUrl = "http://localhost:62382",
    [string]$UserName = "admin",
    [string]$Password = "ChangeMe123!",
    [string]$OutputDir = "artifacts/visual",
    [string]$BrowserExecutablePath = ""
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

    if ([string]::IsNullOrWhiteSpace($BrowserExecutablePath)) {
        $browserCandidates = @(
            "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            "C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            "C:\Program Files\Google\Chrome\Application\chrome.exe",
            "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe"
        )

        $BrowserExecutablePath = $browserCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }

    $env:BASE_URL = $BaseUrl
    $env:VISUAL_USER = $UserName
    $env:VISUAL_PASSWORD = $Password
    $env:VISUAL_OUTPUT_DIR = $resolvedOutput
    $env:PLAYWRIGHT_EXECUTABLE_PATH = $BrowserExecutablePath

    if ([string]::IsNullOrWhiteSpace($BrowserExecutablePath)) {
        npx playwright install chromium
    }

    npx playwright test
}
finally {
    Pop-Location
}
