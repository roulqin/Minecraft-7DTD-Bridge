# Stages artifacts inside this project. Does not launch or modify installed games.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$mc = "$root\runtime\minecraft\mods"
$td = "$root\runtime\7dtd\Mods\MC7DTD-Bridge"
New-Item -ItemType Directory -Path $mc,$td -Force | Out-Null
Copy-Item -LiteralPath "$root\minecraft-mod\build\libs\mc7dtd-bridge-0.1.0.jar" -Destination $mc -Force
$fabricApi = 'D:\wenjian\minecraft\.minecraft\versions\1.21.11-Voxy\mods\fabric-api-0.140.2+1.21.11.jar'
if (-not (Test-Path -LiteralPath $fabricApi)) { throw 'Phase 2 requires fabric-api-0.140.2+1.21.11.jar in the isolated runtime mods directory' }
Copy-Item -LiteralPath $fabricApi -Destination $mc -Force
Copy-Item -LiteralPath "$root\7dtd-mod\dist\MC7DTD-Bridge\MC7DTD.Bridge.dll" -Destination $td -Force
Copy-Item -LiteralPath "$root\7dtd-mod\dist\MC7DTD-Bridge\ModInfo.xml" -Destination $td -Force
Write-Output 'Staged under runtime/. See tests/phase1_test.md for isolated game setup.'
