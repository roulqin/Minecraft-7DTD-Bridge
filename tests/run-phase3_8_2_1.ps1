$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'work/phase3821-test'
New-Item -ItemType Directory -Force $out | Out-Null
$env:GRADLE_USER_HOME=Join-Path $root 'work/gradle-home'
$env:JAVA_HOME='C:\Program Files\Java\jdk-21.0.12'
$env:PATH="$env:JAVA_HOME\bin;$env:PATH"
$env:TEMP=Join-Path $root 'work';$env:TMP=$env:TEMP
Push-Location (Join-Path $root 'minecraft-mod')
try {
    ./gradlew.bat --no-daemon build writeTestClasspath *> "$out/build.log"
    if($LASTEXITCODE){Get-Content "$out/build.log" -Tail 35;throw 'Minecraft build failed'}
}finally{Pop-Location}
$gson=Get-ChildItem $env:GRADLE_USER_HOME -Recurse -Filter gson-2.13.2.jar | Select-Object -First 1 -ExpandProperty FullName
if(-not $gson){throw 'Gson dependency not found'}
$cp="$root/minecraft-mod/build/classes/java/test;$root/minecraft-mod/build/classes/java/main;$root/minecraft-mod/build/resources/main;$gson"
$results=@()
foreach($test in @('EquipmentVisualHarness','EntityDebugHarness','EquipmentHarness','HealthHarness','IdentityHarness','PresentationHarness','NativeProxyHarness')) {
    & "$env:JAVA_HOME/bin/java.exe" -cp $cp "io.mc7dtd.$test" $root *> "$out/$test.log"
    if($LASTEXITCODE){Get-Content "$out/$test.log" -Tail 35;throw "$test failed"}
    $line=Get-Content "$out/$test.log" -Tail 1
    $results+=@{test=$test;result=$line}
    Write-Host "$test $line"
}
$results | ConvertTo-Json | Set-Content -Encoding utf8 "$out/results.json"
Write-Host 'Phase 3.8.2.1 Minecraft build and data derivation checks PASS'
