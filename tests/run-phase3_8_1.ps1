$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$out="$root\work\phase3_8_1-test"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$env:DOTNET_CLI_HOME="$root\work\dotnet-home";$env:NUGET_PACKAGES="$root\work\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:GRADLE_USER_HOME="$root\work\gradle-home";$env:JAVA_HOME='C:\Program Files\Java\jdk-21.0.12'
$env:PATH="$env:JAVA_HOME\bin;$env:PATH";$env:TEMP="$root\work";$env:TMP=$env:TEMP
$env:PYTHONPATH="$root\work\phase3_4-test\python-libs";$env:PYTHONDONTWRITEBYTECODE='1'
$env:MC7DTD_TEST_BRIDGE="$root\bridge-server\bin\Phase381\net10.0\BridgeServer.dll"
dotnet build 7dtd-mod -c Phase381 *> "$out\7dtd-build.log"
if($LASTEXITCODE){Get-Content "$out\7dtd-build.log" -Tail 20;throw '7DTD build failed'}
dotnet build tests/equipment-runner -c Phase381 *> "$out\bridge-build.log"
if($LASTEXITCODE){Get-Content "$out\bridge-build.log" -Tail 20;throw 'Bridge/equipment tests build failed'}
Push-Location minecraft-mod
try{./gradlew.bat --no-daemon build writeTestClasspath *> "$out\minecraft-build.log";if($LASTEXITCODE){Get-Content "$out\minecraft-build.log" -Tail 20;throw 'Minecraft build failed'}}finally{Pop-Location}
$gson=Get-ChildItem "$root\work\gradle-home\caches" -Recurse -Filter gson-2.13.2.jar | Select-Object -First 1
$cp="$root\minecraft-mod\build\classes\java\test;$root\minecraft-mod\build\classes\java\main;$($gson.FullName)"
foreach($test in @('EquipmentHarness','EntityDebugHarness','PresentationHarness','IdentityHarness','HealthHarness','NativeProxyHarness')){
 java -cp $cp "io.mc7dtd.$test" $root *> "$out\$test.log";if($LASTEXITCODE){Get-Content "$out\$test.log" -Tail 25;throw "$test failed"}
}
dotnet run --no-build --project tests/equipment-runner -c Phase381 -- $root *> "$out\equipment.log"
if($LASTEXITCODE){Get-Content "$out\equipment.log" -Tail 25;throw 'Equipment runtime checks failed'}
foreach($project in @('presentation-runner','identity-runner','debug-runner')){
 dotnet build "tests/$project" -c Phase381 *> "$out\$project-build.log";if($LASTEXITCODE){throw "$project build failed"}
 dotnet run --no-build --project "tests/$project" -c Phase381 -- $root *> "$out\$project-regression.log";if($LASTEXITCODE){Get-Content "$out\$project-regression.log" -Tail 20;throw "$project regression failed"}
}
$schemaProcess=Start-Process -FilePath (Get-Command python).Source -ArgumentList 'tests/equipment-runner/validate.py' -WorkingDirectory $root -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput "$out\schema.log" -RedirectStandardError "$out\schema-stderr.log"
if($schemaProcess.ExitCode){Get-Content "$out\schema.log","$out\schema-stderr.log" -Tail 20;throw 'Equipment schema/protected checks failed'}
Write-Host 'Minecraft build PASS; Bridge build PASS; 7DTD build PASS; Equipment Sync checks PASS'
