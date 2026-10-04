param([switch]$BuildOnly)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$evidence=Join-Path $root 'work/phase3851-test/evidence'
$sandbox=Join-Path $root 'work/phase3851-runtime'
$mod=Join-Path $sandbox 'Mods/MC7DTD-AvatarPreview'
New-Item -ItemType Directory -Force $evidence,"$mod/Resources" | Out-Null
dotnet build "$root/prototypes/avatar-preview/AvatarPreview.csproj" -c Prototype | Tee-Object "$root/work/phase3851-test/prototype-build.log"
if($LASTEXITCODE -ne 0){throw 'Prototype build failed'}
if($BuildOnly){return}
if(Get-Process 7DaysToDie -ErrorAction SilentlyContinue){throw 'Exit existing 7DTD before launching the isolated prototype'}
Copy-Item -LiteralPath "$root/prototypes/avatar-preview/bin/Prototype/net48/MC7DTD.AvatarPreview.dll" -Destination $mod
Copy-Item -LiteralPath "$root/prototypes/avatar-preview/ModInfo.xml" -Destination $mod
Copy-Item -LiteralPath "$root/assets/avatar/prototype/alex_slim_mesh.json","$root/assets/avatar/prototype/player_default.png" -Destination "$mod/Resources"
if(!(Test-Path "$sandbox/Saves")){Copy-Item -LiteralPath "$root/runtime/7dtd/Saves" -Destination "$sandbox/Saves" -Recurse}
$env:AVATAR_PREVIEW_EVIDENCE=$evidence
$globalModInfo='D:/Steam/steamapps/common/7 Days To Die/Mods/MC7DTD.Bridge/ModInfo.xml'
$disabledInfo="$globalModInfo.avatar-preview-disabled"
if(Test-Path -LiteralPath $disabledInfo){throw 'Previous isolation file exists; restore it before launching'}
$disabled=$false
try {
 if(Test-Path -LiteralPath $globalModInfo){Move-Item -LiteralPath $globalModInfo -Destination $disabledInfo;$disabled=$true}
 $process=Start-Process -FilePath 'D:/Steam/steamapps/common/7 Days To Die/7DaysToDie.exe' -ArgumentList @("-UserDataFolder=$sandbox",'-logfile',"$evidence/7dtd-game.log",'-screen-fullscreen','0','-screen-width','960','-screen-height','600') -WorkingDirectory $sandbox -WindowStyle Normal -PassThru
 @{pid=$process.Id;sandbox=$sandbox;evidence=$evidence;started=(Get-Date).ToString('o')} | ConvertTo-Json | Set-Content "$evidence/process.json"
 Write-Host "Isolated AvatarPreview started: $($process.Id); evidence: $evidence"
 $process.WaitForExit()
} finally {
 if($disabled){Move-Item -LiteralPath $disabledInfo -Destination $globalModInfo;Write-Host 'Global Bridge ModInfo restored'}
}
