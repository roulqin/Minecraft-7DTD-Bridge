param([ValidateSet('all','bridge','minecraft','7dtd')][string]$Component='all',[ValidatePattern('^[0-9]{8}-[0-9]{6}$')][string]$RunId=(Get-Date -Format 'yyyyMMdd-HHmmss'))
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$env:MC7DTD_ROOT=$root
$env:TEMP="$root/work";$env:TMP="$root/work";$env:MC7DTD_ENTITY_TEST="1"
$evidence=Join-Path $root "docs/phase3_9_1-runtime-evidence/$RunId"
New-Item -ItemType Directory -Force $evidence | Out-Null
if($Component -eq 'all'){
 if(Get-Process 7DaysToDie -ErrorAction SilentlyContinue){throw 'Normally exit old 7DTD first'}
 if(@(Get-CimInstance Win32_Process|Where-Object {$_.Name -eq 'java.exe' -and $_.CommandLine -like '*runtime-arguments.txt*'}).Count){throw 'Normally exit old Minecraft first'}
 $network=Get-Content "$root/config/network.json" -Raw | ConvertFrom-Json
 if(@([System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()|Where-Object Port -eq $network.port).Count){throw 'Stop old Bridge first'}
 foreach($file in @('bridge-server/bin/Phase391/net10.0/BridgeServer.dll','7dtd-mod/bin/Phase391/net48/MC7DTD.Bridge.dll','minecraft-mod/build/libs/mc7dtd-bridge-0.1.0.jar','assets/avatar/bundles/windows/minecraft_avatar_v1')){if(-not(Test-Path -LiteralPath "$root/$file")){throw "Missing build/resource: $file"}}
 foreach($part in @('bridge','minecraft','7dtd')){& $PSCommandPath -Component $part -RunId $RunId};return
}
switch($Component){
 'bridge' {
  $network=Get-Content "$root/config/network.json" -Raw | ConvertFrom-Json
  if(@([System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()|Where-Object Port -eq $network.port).Count){throw 'Old Bridge still owns network port'}
  $exe='dotnet';$argsList=@("$root/bridge-server/bin/Phase391/net10.0/BridgeServer.dll","$root/config/network.json");$cwd=$root;$style='Hidden'
 }
 'minecraft' {
  if(@(Get-CimInstance Win32_Process|Where-Object {$_.Name -eq 'java.exe' -and $_.CommandLine -like '*runtime-arguments.txt*'}).Count){throw 'Normally exit old Minecraft first'}
  Copy-Item -LiteralPath "$root/minecraft-mod/build/libs/mc7dtd-bridge-0.1.0.jar" -Destination "$root/runtime/minecraft/mods/mc7dtd-bridge-0.1.0.jar"
  $exe='C:/Program Files/Java/jdk-21.0.12/bin/java.exe';$argsList=@("@$root/runtime/minecraft/runtime-arguments.txt");$cwd="$root/runtime/minecraft";$style='Normal'
 }
 '7dtd' {
  if(Get-Process 7DaysToDie -ErrorAction SilentlyContinue){throw 'Normally exit old 7DTD first'}
  $helper=Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',"`"$root/tests/launch-avatar-game.ps1`"",'-ProjectRoot',"`"$root`"",'-Evidence',"`"$evidence`"",'-BuildConfiguration','Phase391') -WindowStyle Hidden -PassThru
  Write-Host "7DTD launch/restore helper: $($helper.Id); evidence $evidence";return
 }
}
$p=Start-Process -FilePath $exe -ArgumentList $argsList -WorkingDirectory $cwd -WindowStyle $style -PassThru -RedirectStandardOutput "$evidence/$Component-stdout.log" -RedirectStandardError "$evidence/$Component-stderr.log"
@{component=$Component;pid=$p.Id;started=(Get-Date).ToString('o')}|ConvertTo-Json|Set-Content "$evidence/$Component-process.json"
Write-Host "$Component started: $($p.Id); evidence $evidence"
