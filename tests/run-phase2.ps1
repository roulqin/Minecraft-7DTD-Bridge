$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$env:DOTNET_CLI_HOME = "$root\work\dotnet-home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_PACKAGES = "$root\work\nuget"
$env:TEMP = "$root\work"
$env:TMP = $env:TEMP
dotnet run --project tests/phase1-runner -c Release -- $root phase2
if ($LASTEXITCODE) { throw 'Phase 2 tests failed; see work/phase2-test/results.json' }
