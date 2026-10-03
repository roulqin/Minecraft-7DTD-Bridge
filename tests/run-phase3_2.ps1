$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$env:DOTNET_CLI_HOME = "$root\work\dotnet-home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_PACKAGES = "$root\work\nuget"
$env:TEMP = "$root\work"
$env:TMP = $env:TEMP
dotnet build tests/client-harness/ClientHarness.csproj -c Release
if ($LASTEXITCODE) { throw 'Client harness build failed' }
dotnet run --project tests/entity-runner -c Release -- $root phase3_2
if ($LASTEXITCODE) { throw 'Lifecycle tests failed; see work/phase3_2-test/results.json' }
