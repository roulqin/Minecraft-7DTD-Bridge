$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$out="$root\work\phase3_6_2-test"
New-Item -ItemType Directory -Force $out | Out-Null
$env:DOTNET_CLI_HOME="$root\work\dotnet-home"
$env:NUGET_PACKAGES="$root\work\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:GRADLE_USER_HOME="$root\work\gradle-home"
$env:JAVA_HOME='C:\Program Files\Java\jdk-21.0.12'
$env:PATH="$env:JAVA_HOME\bin;$env:PATH"
$env:TEMP="$root\work"
$env:TMP=$env:TEMP
$env:PYTHONPATH="$root\work\phase3_4-test\python-libs"
Push-Location minecraft-mod
try { ./gradlew.bat --no-daemon build writeTestClasspath *> "$out\build.log"; if($LASTEXITCODE){throw 'Fabric build failed'} } finally {Pop-Location}
$gson=Get-ChildItem "$root\work\gradle-home\caches" -Recurse -Filter gson-2.13.2.jar | Select-Object -First 1
$cp="$root\minecraft-mod\build\classes\java\test;$root\minecraft-mod\build\classes\java\main;$($gson.FullName)"
foreach($test in @('NativeProxyHarness','PlayerProxyHarness')) {
 java -cp $cp "io.mc7dtd.$test" $root *> "$out\$test.log"
 if($LASTEXITCODE){throw "$test failed"}
}
python tests/entity-types/test_catalog.py *> "$out\catalog.log"
if($LASTEXITCODE){throw 'Catalog tests failed'}
dotnet build tests/native-transport-runner -c Phase362 *> "$out\dotnet-build.log"
if($LASTEXITCODE){throw 'Transport build failed'}
$env:MC7DTD_TEST_BRIDGE="$root\bridge-server\bin\Phase362\net10.0\BridgeServer.dll"
$env:MC7DTD_TEST_ROOT=$root
try {
 dotnet run --no-build --project tests/native-transport-runner -c Phase362 -- $root io.mc7dtd.NativeProxyReceiverHarness work/phase3_6_2-test *> "$out\transport.log"
 if($LASTEXITCODE){throw 'Projection WebSocket tests failed'}
 foreach($project in @('entity-runner','marker-runner','player-proxy-runner')) {
   $params=@('run','--project',"tests/$project",'-c','Phase362','--',$root)
   if($project -eq 'entity-runner'){$params+='phase3_2'}
   & dotnet @params *> "$out\$project-regression.log"
   if($LASTEXITCODE){throw "$project regression failed"}
 }
} finally {Remove-Item Env:MC7DTD_TEST_BRIDGE,Env:MC7DTD_TEST_ROOT -ErrorAction SilentlyContinue}
Write-Host 'Phase 3.6.2 checks passed'
