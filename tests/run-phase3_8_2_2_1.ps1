$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'work/phase38221-test'
New-Item -ItemType Directory -Force $out | Out-Null
$env:JAVA_HOME='C:/Program Files/Java/jdk-21.0.12';$env:PATH="$env:JAVA_HOME/bin;$env:PATH"
$env:GRADLE_USER_HOME=Join-Path $root 'work/gradle-home';$env:TEMP=Join-Path $root 'work';$env:TMP=$env:TEMP
Push-Location (Join-Path $root 'minecraft-mod')
try {./gradlew.bat --no-daemon build writeTestClasspath *> "$out/minecraft-build.log";if($LASTEXITCODE){Get-Content "$out/minecraft-build.log" -Tail 30;throw 'Minecraft build failed'}}finally{Pop-Location}
$cp=(Get-Content (Join-Path $root 'minecraft-mod/build/test-classpath.txt') -Raw).Trim()
$results=@()
foreach($test in @('EntityGotoHarness','EntityNavigationHarness','ProxyAppearanceHarness','EquipmentVisualHarness','EntityDebugHarness','EquipmentHarness','HealthHarness','IdentityHarness','PresentationHarness','NativeProxyHarness')) {
 & "$env:JAVA_HOME/bin/java.exe" -cp $cp "io.mc7dtd.$test" $root *> "$out/$test.log"
 if($LASTEXITCODE){Get-Content "$out/$test.log" -Tail 30;throw "$test failed"}
 $result=Get-Content "$out/$test.log" -Tail 1;$results+=@{test=$test;result=$result};Write-Host "$test $result"
}
$results | ConvertTo-Json | Set-Content -Encoding utf8 "$out/results.json"
