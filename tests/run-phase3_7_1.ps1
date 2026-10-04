$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$out="$root\work\phase3_7_1-test"
New-Item -ItemType Directory -Force $out | Out-Null
$env:DOTNET_CLI_HOME="$root\work\dotnet-home"
$env:NUGET_PACKAGES="$root\work\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:GRADLE_USER_HOME="$root\work\gradle-home"
$env:JAVA_HOME='C:\Program Files\Java\jdk-21.0.12'
$env:PATH="$env:JAVA_HOME\bin;$env:PATH"
$env:TEMP="$root\work"; $env:TMP=$env:TEMP
dotnet build 7dtd-mod -c Phase371 *> "$out\7dtd-build.log"
if($LASTEXITCODE){throw '7DTD build failed'}
dotnet build tests/health-runner -c Phase371 *> "$out\bridge-build.log"
if($LASTEXITCODE){throw 'Health runner / Bridge build failed'}
Push-Location minecraft-mod
try { ./gradlew.bat --no-daemon build writeTestClasspath *> "$out\minecraft-build.log"; if($LASTEXITCODE){throw 'Fabric build failed'} } finally {Pop-Location}
$gson=Get-ChildItem "$root\work\gradle-home\caches" -Recurse -Filter gson-2.13.2.jar | Select-Object -First 1
$cp="$root\minecraft-mod\build\classes\java\test;$root\minecraft-mod\build\classes\java\main;$($gson.FullName)"
java -cp $cp io.mc7dtd.HealthHarness $root *> "$out\java-health.log"
if($LASTEXITCODE){throw 'Java health tests failed'}
dotnet run --no-build --project tests/health-runner -c Phase371 -- $root *> "$out\health.log"
if($LASTEXITCODE){throw 'Runtime health tests failed'}
$env:PYTHONPATH="$root\work\phase3_4-test\python-libs"
$env:PYTHONDONTWRITEBYTECODE='1'
python tests/health-runner/validate_wire.py *> "$out\wire-schema.log"
if($LASTEXITCODE){throw 'Serialized health schema/semantics failed'}
Write-Host 'Phase 3.7.1 health checks passed'
