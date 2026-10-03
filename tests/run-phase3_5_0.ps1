$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$output = "$root\work\phase3_5_0-test"
$libraries = "$root\work\phase3_4-test\python-libs"
New-Item -ItemType Directory -Force $output, "$root\work\phase3_4-test" | Out-Null
$env:DOTNET_CLI_HOME = "$root\work\dotnet-home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_PACKAGES = "$root\work\nuget"
$env:TEMP = "$root\work"
$env:TMP = $env:TEMP
$env:PYTHONPATH = $libraries
$env:PYTHONDONTWRITEBYTECODE = '1'
if (-not (Test-Path "$libraries\jsonschema")) {
    python -m pip install --disable-pip-version-check --target $libraries --cache-dir "$root\work\pip-cache" -r tests/entity-types/requirements.txt
    if ($LASTEXITCODE) { throw 'Schema dependency installation failed' }
}
python tests/entity-types/test_player_proxy.py 2>&1 | Tee-Object "$output\catalog-tests.log"
if ($LASTEXITCODE) { throw 'Player proxy configuration design tests failed' }
dotnet run --project tests/player-proxy-design-runner -c Release -- $root 2>&1 | Tee-Object "$output\example-tests.log"
if ($LASTEXITCODE) { throw 'Player proxy protocol example validation failed' }
Write-Host "Phase 3.5.0 offline design checks passed: $output"
