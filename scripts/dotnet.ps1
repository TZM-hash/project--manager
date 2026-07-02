$root = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $root ".dotnet\dotnet.exe"
$dotnet = if (Test-Path $localDotnet) {
    $localDotnet
} else {
    (Get-Command dotnet -ErrorAction SilentlyContinue).Source
}

if ([string]::IsNullOrWhiteSpace($dotnet)) {
    Write-Error "dotnet executable was not found. Install .NET SDK 9.0 or restore the local .dotnet folder."
    exit 1
}

& $dotnet @args
exit $LASTEXITCODE
