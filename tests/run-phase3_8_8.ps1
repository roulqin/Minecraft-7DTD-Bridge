$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'work/phase388-test'
New-Item -ItemType Directory -Force $out | Out-Null
& "$root/tests/build-phase3_8_8-resources.ps1"
foreach($project in @('7dtd-mod/MC7DTD.Mod.csproj','bridge-server/BridgeServer.csproj')) {
 dotnet build "$root/$project" -c Phase388
 if($LASTEXITCODE){throw "$project build failed"}
}
& "$root/tests/run-phase3_8_4_1.ps1"
dotnet run --project "$root/tests/player-proxy-runner/PlayerProxyRunner.csproj" -- $root *> "$out/player-proxy-regression.log"
if($LASTEXITCODE){throw 'Player proxy controller/transport regression failed'}
& "$root/tests/verify-phase3_8_8-game.ps1"
