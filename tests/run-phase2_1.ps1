$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$env:DOTNET_CLI_HOME = "$root\work\dotnet-home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_PACKAGES = "$root\work\nuget"
$env:TEMP = "$root\work"
$env:TMP = $env:TEMP
dotnet run --project tests/coordinate-runner -c Release -- $root
if ($LASTEXITCODE) { throw 'Phase 2.1 tests failed; see work/phase2_1-test/results.json' }
