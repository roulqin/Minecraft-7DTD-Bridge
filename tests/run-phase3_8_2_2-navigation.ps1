$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'work/phase3822-navigation-test'
New-Item -ItemType Directory -Force $out | Out-Null
$env:DOTNET_CLI_HOME=Join-Path $root 'work/dotnet-home';$env:NUGET_PACKAGES=Join-Path $root 'work/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:GRADLE_USER_HOME=Join-Path $root 'work/gradle-home';$env:JAVA_HOME='C:/Program Files/Java/jdk-21.0.12'
$env:PATH="$env:JAVA_HOME/bin;$env:PATH";$env:TEMP=Join-Path $root 'work';$env:TMP=$env:TEMP
foreach($project in @('bridge-server','7dtd-mod','tests/debug-runner')) {
 $label=Split-Path $project -Leaf
 dotnet build (Join-Path $root $project) -c Phase3822Navigation *> "$out/$label-build.log"
 if($LASTEXITCODE){Get-Content "$out/$label-build.log" -Tail 25;throw "$label build failed"}
 Write-Host "$label build PASS"
}
Push-Location (Join-Path $root 'minecraft-mod')
try {./gradlew.bat --no-daemon build writeTestClasspath *> "$out/minecraft-build.log";if($LASTEXITCODE){Get-Content "$out/minecraft-build.log" -Tail 30;throw 'Minecraft build failed'}}finally{Pop-Location}
$gson=Get-ChildItem (Join-Path $root 'work/gradle-home') -Recurse -Filter gson-2.13.2.jar | Select-Object -First 1 -ExpandProperty FullName
$cp="$root/minecraft-mod/build/classes/java/test;$root/minecraft-mod/build/classes/java/main;$root/minecraft-mod/build/resources/main;$gson"
$results=@()
foreach($test in @('EntityNavigationHarness','ProxyAppearanceHarness','EquipmentVisualHarness','EntityDebugHarness','EquipmentHarness','HealthHarness','IdentityHarness','PresentationHarness','NativeProxyHarness')) {
 & "$env:JAVA_HOME/bin/java.exe" '-Dfile.encoding=UTF-8' -cp $cp "io.mc7dtd.$test" $root *> "$out/$test.log"
 if($LASTEXITCODE){Get-Content "$out/$test.log" -Tail 30;throw "$test failed"}
 $result=Get-Content "$out/$test.log" -Tail 1;$results+=@{test=$test;result=$result};Write-Host "$test $result"
}
$env:MC7DTD_TEST_BRIDGE=Join-Path $root 'bridge-server/bin/Phase3822Navigation/net10.0/BridgeServer.dll'
dotnet run --no-build --project (Join-Path $root 'tests/debug-runner') -c Phase3822Navigation -- $root *> "$out/debug-regression.log"
if($LASTEXITCODE){Get-Content "$out/debug-regression.log" -Tail 30;throw 'Shared debug config regression failed'}
$results | ConvertTo-Json | Set-Content -Encoding utf8 "$out/results.json"
Get-Content "$out/EntityNavigationHarness.log"
Get-Content "$out/debug-regression.log" -Tail 1
