param(
    [switch]$RunPlaywright,
    [string]$BaseUrl = 'http://127.0.0.1:62383',
    [string]$BrowserPath = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
)

$ErrorActionPreference = 'Stop'

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifacts = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$env:LOCALAPPDATA = Join-Path $artifacts 'test-localappdata'
$env:NUGET_PACKAGES = 'C:\Users\TZM-NEW\.nuget\packages'
$env:DOTNET_PROCESSOR_COUNT = '1'

Push-Location $root
try {
    dotnet build 'ProjectManager.sln' -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }

    dotnet test 'ProjectManager.sln' -c Release --no-build --no-restore --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) { throw 'Automated tests failed.' }

    Get-ChildItem -LiteralPath 'src\ProjectManager.Web\wwwroot\js' -Recurse -Filter '*.js' | ForEach-Object {
        node --check $_.FullName
        if ($LASTEXITCODE -ne 0) { throw "JavaScript syntax check failed: $($_.FullName)" }
    }
    node --check 'tests\visual\project-ui.spec.js'
    if ($LASTEXITCODE -ne 0) { throw 'Playwright specification syntax check failed.' }

    Push-Location 'tests\visual'
    try {
        npx playwright test --list
        if ($LASTEXITCODE -ne 0) { throw 'Playwright test discovery failed.' }
        if ($RunPlaywright) {
            $env:BASE_URL = $BaseUrl
            if (Test-Path -LiteralPath $BrowserPath) {
                $env:PLAYWRIGHT_EXECUTABLE_PATH = $BrowserPath
            }
            npx playwright test
            if ($LASTEXITCODE -ne 0) { throw 'Playwright visual and interaction gate failed.' }
        }
    }
    finally {
        Pop-Location
    }

    Write-Host 'Quality gate passed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
