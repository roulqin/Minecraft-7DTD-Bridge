$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'work/phase3831-test'
New-Item -ItemType Directory -Force $out | Out-Null
$env:DOTNET_CLI_HOME=Join-Path $root 'work/dotnet-home';$env:NUGET_PACKAGES=Join-Path $root 'work/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:JAVA_HOME='C:/Program Files/Java/jdk-21.0.12';$env:PATH="$env:JAVA_HOME/bin;$env:PATH"
$env:GRADLE_USER_HOME=Join-Path $root 'work/gradle-home';$env:TEMP=Join-Path $root 'work';$env:TMP=$env:TEMP
foreach($project in @('bridge-server','7dtd-mod')) {
 dotnet build (Join-Path $root $project) -c Phase3831 *> "$out/$project-build.log"
 if($LASTEXITCODE){Get-Content "$out/$project-build.log" -Tail 25;throw "$project build failed"}
 Write-Host "$project build PASS (source unchanged)"
}
Push-Location (Join-Path $root 'minecraft-mod')
try {./gradlew.bat --no-daemon build writeTestClasspath *> "$out/minecraft-build.log";if($LASTEXITCODE){Get-Content "$out/minecraft-build.log" -Tail 30;throw 'Minecraft build failed'}}finally{Pop-Location}
$cp=(Get-Content (Join-Path $root 'minecraft-mod/build/test-classpath.txt') -Raw).Trim()
$results=@()
foreach($test in @('RendererHarness','EntityGotoHarness','EntityNavigationHarness','ProxyAppearanceHarness','EquipmentVisualHarness','EntityDebugHarness','EquipmentHarness','HealthHarness','IdentityHarness','PresentationHarness','NativeProxyHarness')) {
 & "$env:JAVA_HOME/bin/java.exe" -cp $cp "io.mc7dtd.$test" $root *> "$out/$test.log"
 if($LASTEXITCODE){Get-Content "$out/$test.log" -Tail 35;throw "$test failed"}
 $result=Get-Content "$out/$test.log" -Tail 1;$results+=@{test=$test;result=$result};Write-Host "$test $result"
}
$results | ConvertTo-Json | Set-Content -Encoding utf8 "$out/results.json"
