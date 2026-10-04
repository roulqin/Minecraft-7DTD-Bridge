$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$evidence=Join-Path $root 'work/phase3852-test'
$sandbox=Join-Path $root 'work/phase3852-game-resource-smoke'
$mod=Join-Path $sandbox 'Mods/AvatarResourceSmoke'
if(Get-Process 7DaysToDie -ErrorAction SilentlyContinue){throw 'Exit existing 7DTD before the isolated resource test'}
dotnet build "$root/tests/avatar-resource-smoke/AvatarResourceSmoke.csproj" -c ResourceSmoke | Tee-Object "$evidence/resource-smoke-build.log"
if($LASTEXITCODE){throw 'Resource smoke harness build failed'}
New-Item -ItemType Directory -Force $mod | Out-Null
Copy-Item -LiteralPath "$root/tests/avatar-resource-smoke/bin/ResourceSmoke/net48/MC7DTD.AvatarResourceSmoke.dll","$root/tests/avatar-resource-smoke/ModInfo.xml","$root/assets/avatar/bundles/windows/minecraft_avatar_v1" -Destination $mod
$env:AVATAR_RESOURCE_EVIDENCE=$evidence
'NOT_EXECUTED' | Set-Content "$evidence/7dtd-resource-results.txt"
$globalModInfo='D:/Steam/steamapps/common/7 Days To Die/Mods/MC7DTD.Bridge/ModInfo.xml'
$disabledInfo="$globalModInfo.avatar-resource-disabled"
if(Test-Path -LiteralPath $disabledInfo){throw 'Previous isolation file exists; restore it first'}
$disabled=$false
try {
 if(Test-Path -LiteralPath $globalModInfo){Move-Item -LiteralPath $globalModInfo -Destination $disabledInfo;$disabled=$true}
 $p=Start-Process -FilePath 'D:/Steam/steamapps/common/7 Days To Die/7DaysToDie.exe' -ArgumentList @("-UserDataFolder=$sandbox",'-logfile',"$evidence/7dtd-resource-smoke.log",'-screen-fullscreen','0','-screen-width','640','-screen-height','400') -WorkingDirectory $sandbox -WindowStyle Hidden -PassThru
 $p.Id | Set-Content "$evidence/game-smoke.pid"
 Write-Host "Resource-only game test started: $($p.Id); no world entry; closes automatically"
 $p.WaitForExit()
} finally {if($disabled){Move-Item -LiteralPath $disabledInfo -Destination $globalModInfo;Write-Host 'Global Bridge ModInfo restored'}}
$result=Get-Content "$evidence/7dtd-resource-results.txt"
$result | Write-Output
if($result.Count -ne 10 -or $result -match 'FAIL|ERROR'){throw '7DTD resource smoke test failed'}
$bundleHash=(Get-FileHash -LiteralPath "$mod/minecraft_avatar_v1" -Algorithm SHA256).Hash
$engineLine=(Get-Content "$evidence/7dtd-resource-smoke.log" | Select-String 'Initialize engine version:' | Select-Object -First 1).Line
@{bundle_sha256=$bundleHash;engine=$engineLine;status='verified_7dtd';checks=10} | ConvertTo-Json | Set-Content -Encoding utf8 "$evidence/game-resource-validation.json"
