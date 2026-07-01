$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root ".dotnet\dotnet.exe"
& $dotnet @args
exit $LASTEXITCODE
