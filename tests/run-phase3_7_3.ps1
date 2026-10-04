$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$out="$root\work\phase3_7_3-test"
New-Item -ItemType Directory -Force $out | Out-Null
$env:DOTNET_CLI_HOME="$root\work\dotnet-home"; $env:NUGET_PACKAGES="$root\work\nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:GRADLE_USER_HOME="$root\work\gradle-home";$env:JAVA_HOME='C:\Program Files\Java\jdk-21.0.12'
$env:PATH="$env:JAVA_HOME\bin;$env:PATH";$env:TEMP="$root\work";$env:TMP=$env:TEMP
$env:PYTHONPATH="$root\work\phase3_4-test\python-libs";$env:PYTHONDONTWRITEBYTECODE='1'
dotnet build 7dtd-mod -c Phase373 *> "$out\7dtd-build.log"
if($LASTEXITCODE){throw '7DTD build failed'}
dotnet build tests/presentation-runner -c Phase373 *> "$out\bridge-build.log"
if($LASTEXITCODE){throw 'Bridge / Presentation tests build failed'}
Push-Location minecraft-mod
try{./gradlew.bat --no-daemon build writeTestClasspath *> "$out\minecraft-build.log";if($LASTEXITCODE){throw 'Minecraft build failed'}}finally{Pop-Location}
$gson=Get-ChildItem "$root\work\gradle-home\caches" -Recurse -Filter gson-2.13.2.jar | Select-Object -First 1
$cp="$root\minecraft-mod\build\classes\java\test;$root\minecraft-mod\build\classes\java\main;$($gson.FullName)"
foreach($test in @('PresentationHarness','IdentityHarness','HealthHarness')){java -cp $cp "io.mc7dtd.$test" $root *> "$out\$test.log";if($LASTEXITCODE){throw "$test failed"}}
dotnet run --no-build --project tests/presentation-runner -c Phase373 -- $root *> "$out\presentation.log"
if($LASTEXITCODE){throw 'Presentation runtime tests failed'}
$env:MC7DTD_TEST_BRIDGE="$root\bridge-server\bin\Phase373\net10.0\BridgeServer.dll"
dotnet build tests/identity-runner -c Phase373 *> "$out\regression-build.log"
if($LASTEXITCODE){throw 'Identity regression build failed'}
dotnet run --no-build --project tests/identity-runner -c Phase373 -- $root *> "$out\identity-regression.log"
if($LASTEXITCODE){throw 'Health / Identity runtime regression failed'}
# Use a file redirect at the process boundary: successful unittest progress on stderr is not a PowerShell error.
$schemaProcess=Start-Process -FilePath 'python' -ArgumentList 'tests/identity-runner/validate_wire.py' -WorkingDirectory $root -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput "$out\identity-schema-regression.log" -RedirectStandardError "$out\identity-schema-regression-stderr.log"
if($schemaProcess.ExitCode){throw 'Existing component schema regression failed'}
python tests/presentation-runner/validate.py *> "$out\schema.log"
if($LASTEXITCODE){throw 'Presentation schema / preservation checks failed'}
Write-Host 'Minecraft build PASS; Bridge build PASS; 7DTD build PASS; Presentation checks PASS'
