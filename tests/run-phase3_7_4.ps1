$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$out="$root\work\phase3_7_4-test"
New-Item -ItemType Directory -Force $out | Out-Null
$env:DOTNET_CLI_HOME="$root\work\dotnet-home";$env:NUGET_PACKAGES="$root\work\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:GRADLE_USER_HOME="$root\work\gradle-home";$env:JAVA_HOME='C:\Program Files\Java\jdk-21.0.12'
$env:PATH="$env:JAVA_HOME\bin;$env:PATH";$env:TEMP="$root\work";$env:TMP=$env:TEMP
$env:PYTHONPATH="$root\work\phase3_4-test\python-libs";$env:PYTHONDONTWRITEBYTECODE='1'
$env:MC7DTD_TEST_BRIDGE="$root\bridge-server\bin\Phase374\net10.0\BridgeServer.dll"
dotnet build 7dtd-mod -c Phase374 *> "$out\7dtd-build.log"
if($LASTEXITCODE){throw '7DTD build failed'}
dotnet build tests/debug-runner -c Phase374 *> "$out\bridge-build.log"
if($LASTEXITCODE){throw 'Bridge / debug tests build failed'}
Push-Location minecraft-mod
try{./gradlew.bat --no-daemon build writeTestClasspath *> "$out\minecraft-build.log";if($LASTEXITCODE){throw 'Minecraft build failed'}}finally{Pop-Location}
$gson=Get-ChildItem "$root\work\gradle-home\caches" -Recurse -Filter gson-2.13.2.jar | Select-Object -First 1
$cp="$root\minecraft-mod\build\classes\java\test;$root\minecraft-mod\build\classes\java\main;$($gson.FullName)"
foreach($test in @('EntityDebugHarness','PresentationHarness','IdentityHarness','HealthHarness','NativeProxyHarness')){java -cp $cp "io.mc7dtd.$test" $root *> "$out\$test.log";if($LASTEXITCODE){throw "$test failed"}}
dotnet run --no-build --project tests/debug-runner -c Phase374 -- $root *> "$out\debug.log"
if($LASTEXITCODE){throw 'Inspector runtime checks failed'}
foreach($project in @('presentation-runner','identity-runner')){
 dotnet build "tests/$project" -c Phase374 *> "$out\$project-build.log"
 if($LASTEXITCODE){throw "$project regression build failed"}
 dotnet run --no-build --project "tests/$project" -c Phase374 -- $root *> "$out\$project-regression.log"
 if($LASTEXITCODE){throw "$project regression failed"}
}
$schemaProcess=Start-Process -FilePath 'python' -ArgumentList 'tests/identity-runner/validate_wire.py' -WorkingDirectory $root -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput "$out\identity-schema-regression.log" -RedirectStandardError "$out\identity-schema-regression-stderr.log"
if($schemaProcess.ExitCode){throw 'Existing component schema regression failed'}
python tests/debug-runner/validate.py *> "$out\schema.log"
if($LASTEXITCODE){throw 'Debug schema / core preservation checks failed'}
Write-Host 'Minecraft build PASS; Bridge build PASS; 7DTD build PASS; Debug Layer checks PASS'
