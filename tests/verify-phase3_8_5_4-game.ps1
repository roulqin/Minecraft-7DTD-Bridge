$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$evidence=Join-Path $root 'work/phase3854-test'
$sandbox=Join-Path $root 'work/phase3854-game-runtime-smoke'
$fixture=Join-Path $sandbox 'fixture'
$mod=Join-Path $sandbox 'Mods/AvatarPolishSmoke'
if(Get-Process 7DaysToDie -ErrorAction SilentlyContinue){throw 'Exit existing 7DTD before isolated engine verification'}
New-Item -ItemType Directory -Force $evidence,$mod,"$fixture/assets/avatar/skins","$fixture/assets/avatar/bundles/windows" | Out-Null
dotnet build "$root/tests/avatar-polish-smoke/AvatarPolishSmoke.csproj" -c RuntimeSmoke | Tee-Object "$evidence/runtime-harness-build.log"
if($LASTEXITCODE){throw 'Harness build failed'}
Copy-Item -LiteralPath "$root/tests/avatar-polish-smoke/bin/RuntimeSmoke/net48/MC7DTD.AvatarPolishSmoke.dll","$root/tests/avatar-polish-smoke/ModInfo.xml" -Destination $mod
Copy-Item -LiteralPath "$root/assets/avatar/avatar_config.json" -Destination "$fixture/assets/avatar"
Copy-Item -LiteralPath "$root/assets/avatar/skins/player_default.png" -Destination "$fixture/assets/avatar/skins"
Copy-Item -LiteralPath "$root/assets/avatar/bundles/windows/minecraft_avatar_v1" -Destination "$fixture/assets/avatar/bundles/windows"
$env:AVATAR_RUNTIME_FIXTURE=$fixture;$env:AVATAR_RUNTIME_EVIDENCE=$evidence
'NOT_EXECUTED' | Set-Content "$evidence/7dtd-runtime-results.txt"
$globalModInfo='D:/Steam/steamapps/common/7 Days To Die/Mods/MC7DTD.Bridge/ModInfo.xml'
$disabledInfo="$globalModInfo.avatar-runtime-disabled"
if(Test-Path -LiteralPath $disabledInfo){throw 'Previous isolation file exists; restore first'}
$disabled=$false
try {
 if(Test-Path -LiteralPath $globalModInfo){Move-Item -LiteralPath $globalModInfo -Destination $disabledInfo;$disabled=$true}
 $p=Start-Process -FilePath 'D:/Steam/steamapps/common/7 Days To Die/7DaysToDie.exe' -ArgumentList @("-UserDataFolder=$sandbox",'-logfile',"$evidence/7dtd-runtime-smoke.log",'-screen-fullscreen','0','-screen-width','640','-screen-height','400') -WorkingDirectory $sandbox -WindowStyle Hidden -PassThru
 Write-Host "Isolated engine test started: $($p.Id); no world entry; auto exit"
 $deadline=(Get-Date).AddMinutes(3)
 while(-not $p.WaitForExit(5000)){if((Get-Date)-gt $deadline){Stop-Process -Id $p.Id;throw 'Isolated engine test timeout'}}
} finally {if($disabled){Move-Item -LiteralPath $disabledInfo -Destination $globalModInfo;Write-Host 'Global Bridge ModInfo restored'}}
$result=Get-Content "$evidence/7dtd-runtime-results.txt"
$result | Write-Output
if($result.Count -ne 54 -or $result -match 'FAIL|ERROR|NOT_EXECUTED'){throw 'Avatar runtime engine verification failed'}
