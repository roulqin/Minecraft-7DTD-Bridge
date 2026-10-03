$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
New-Item -ItemType Directory -Force "$root\work\phase3_5_1-test" | Out-Null
$env:JAVA_HOME = 'C:\Program Files\Java\jdk-21.0.12'
$env:PATH = "$env:JAVA_HOME\bin;$env:PATH"
& "$root\build.ps1" *> "$root\work\phase3_5_1-test\build.log"
$gson = Get-ChildItem "$root\work\gradle-home\caches" -Recurse -Filter 'gson-2.13.2.jar' | Select-Object -First 1
if (-not $gson) { throw 'Gson test dependency missing' }
$classpath = "$root\minecraft-mod\build\classes\java\test;$root\minecraft-mod\build\classes\java\main;$($gson.FullName)"
java -cp $classpath io.mc7dtd.PlayerProxyHarness $root 2>&1 | Tee-Object "$root\work\phase3_5_1-test\java-tests.log"
if ($LASTEXITCODE) { throw 'Player tracker checks failed' }
dotnet run --project tests/player-proxy-runner -c Release -- $root *> "$root\work\phase3_5_1-test\receiver-tests.log"
if ($LASTEXITCODE) { throw 'Player proxy receiver checks failed' }
& "$root\tests\run-phase3_3.ps1" *> "$root\work\phase3_5_1-test\marker-regression.log"
& "$root\tests\run-phase3_2.ps1" *> "$root\work\phase3_5_1-test\lifecycle-regression.log"
& "$root\tests\run-phase3_5_0.ps1" *> "$root\work\phase3_5_1-test\design-regression.log"
Write-Host 'Phase 3.5.1 checks passed'
