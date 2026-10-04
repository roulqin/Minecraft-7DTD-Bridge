$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'work/phase390-test'
New-Item -ItemType Directory -Force $out | Out-Null
foreach($project in @('7dtd-mod/MC7DTD.Mod.csproj','bridge-server/BridgeServer.csproj')) {
 dotnet build "$root/$project" -c Phase390
 if($LASTEXITCODE){throw "$project build failed"}
}
& "$root/tests/run-phase3_8_4_1.ps1" *> "$out/minecraft-regression.log"
Copy-Item -LiteralPath "$root/work/phase3841-test/minecraft-build.log" -Destination $out
dotnet run --project "$root/tests/player-proxy-runner/PlayerProxyRunner.csproj" -- $root *> "$out/player-proxy-regression.log"
if($LASTEXITCODE){throw 'Player proxy regression failed'}
& "$root/tests/verify-phase3_9_0-game.ps1"
