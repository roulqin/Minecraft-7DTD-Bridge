$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$out = "$root\work\phase3_6_1-test"
New-Item -ItemType Directory -Force $out | Out-Null
$env:DOTNET_CLI_HOME = "$root\work\dotnet-home"
$env:NUGET_PACKAGES = "$root\work\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:GRADLE_USER_HOME = "$root\work\gradle-home"
$env:TEMP = "$root\work"
$env:TMP = $env:TEMP
$env:JAVA_HOME = 'C:\Program Files\Java\jdk-21.0.12'
$env:PATH = "$env:JAVA_HOME\bin;$env:PATH"
# Separate configuration avoids overwriting binaries held by a running game/Bridge.
dotnet build bridge-server -c Phase361 *> "$out\bridge-build.log"
if ($LASTEXITCODE) { throw 'Bridge build failed' }
dotnet build 7dtd-mod -c Phase361 *> "$out\7dtd-build.log"
if ($LASTEXITCODE) { throw '7DTD build failed' }
dotnet build tests/client-harness -c Phase361 *> "$out\harness-build.log"
if ($LASTEXITCODE) { throw 'Harness build failed' }
Push-Location minecraft-mod
try {
    ./gradlew.bat --no-daemon build writeTestClasspath *> "$out\fabric-build.log"
    if ($LASTEXITCODE) { throw 'Fabric build failed' }
} finally { Pop-Location }
dotnet run --project tests/native-transport-runner -c Phase361 -- $root *> "$out\tests.log"
if ($LASTEXITCODE) { throw 'Native v2 tests failed; see tests.log' }
$env:PYTHONPATH = "$root\work\phase3_4-test\python-libs"
python tests/native-transport-runner/validate_sender.py *> "$out\schema-tests.log"
if ($LASTEXITCODE) { throw 'Sender v2 contract validation failed' }
$env:MC7DTD_TEST_BRIDGE = "$root\bridge-server\bin\Phase361\net10.0\BridgeServer.dll"
$env:MC7DTD_TEST_CLIENT = "$root\tests\client-harness\bin\Phase361\net48\ClientHarness.exe"
try {
    foreach ($project in @('entity-runner','marker-runner','player-proxy-runner')) {
        $arguments = @('run','--project',"tests/$project",'-c','Phase361','--',$root)
        if ($project -eq 'entity-runner') { $arguments += 'phase3_2' }
        & dotnet @arguments *> "$out\$project-regression.log"
        if ($LASTEXITCODE) { throw "$project regression failed" }
    }
} finally {
    Remove-Item Env:MC7DTD_TEST_BRIDGE, Env:MC7DTD_TEST_CLIENT -ErrorAction SilentlyContinue
}
Write-Host 'Phase 3.6.1 build, transport and v1 regressions passed'
