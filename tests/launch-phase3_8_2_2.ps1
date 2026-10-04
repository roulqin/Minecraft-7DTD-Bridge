param(
 [ValidateSet('all','bridge','minecraft','7dtd')][string]$Component='all',
 [ValidatePattern('^[0-9]{8}-[0-9]{6}$')][string]$RunId=(Get-Date -Format 'yyyyMMdd-HHmmss'),
 [ValidateSet('Phase3822','Phase3822Navigation')][string]$BuildConfiguration='Phase3822'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$network=Get-Content (Join-Path $root 'config/network.json') -Raw | ConvertFrom-Json
$minecraftRunning=@(Get-CimInstance Win32_Process | Where-Object {$_.Name -eq 'java.exe' -and $_.CommandLine -like '*runtime-arguments.txt*'}).Count -gt 0
$dtdRunning=@(Get-Process 7DaysToDie -ErrorAction SilentlyContinue).Count -gt 0
$portUsed=@([System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() | Where-Object Port -eq $network.port).Count -gt 0
if($Component -eq 'all') {
 if($minecraftRunning -or $dtdRunning -or $portUsed){throw 'Normally exit the old games and stop the old Bridge before launching all'}
 foreach($file in @("bridge-server/bin/$BuildConfiguration/net10.0/BridgeServer.dll","7dtd-mod/bin/$BuildConfiguration/net48/MC7DTD.Bridge.dll",'minecraft-mod/build/libs/mc7dtd-bridge-0.1.0.jar')) {
  if(-not (Test-Path -LiteralPath (Join-Path $root $file))){throw "Build Phase 3.8.2.2 first: missing $file"}
 }
 foreach($part in @('bridge','minecraft','7dtd')){& $PSCommandPath -Component $part -RunId $RunId -BuildConfiguration $BuildConfiguration}
 return
}
$evidence=Join-Path $root "docs/phase3_8_2_2-runtime-evidence/$RunId"
New-Item -ItemType Directory -Force $evidence | Out-Null
$env:MC7DTD_ROOT=$root;$env:MC7DTD_ENTITY_TEST='1'
$env:TEMP=Join-Path $root 'work';$env:TMP=$env:TEMP
switch($Component) {
 'bridge' {
  if($portUsed){throw 'Stop the old Bridge; network port already in use'}
  $exe='dotnet';$arguments=@("$root/bridge-server/bin/$BuildConfiguration/net10.0/BridgeServer.dll","$root/config/network.json");$cwd=$root
 }
 'minecraft' {
  if($minecraftRunning){throw 'Normally exit the old Minecraft instance first'}
  Copy-Item -LiteralPath "$root/minecraft-mod/build/libs/mc7dtd-bridge-0.1.0.jar" -Destination "$root/runtime/minecraft/mods/mc7dtd-bridge-0.1.0.jar"
  $exe='C:\Program Files\Java\jdk-21.0.12\bin\java.exe';$arguments=@("@$root/runtime/minecraft/runtime-arguments.txt");$cwd="$root/runtime/minecraft"
 }
 '7dtd' {
  if($dtdRunning){throw 'Normally exit the old 7DTD instance first'}
  Copy-Item -LiteralPath "$root/7dtd-mod/bin/$BuildConfiguration/net48/MC7DTD.Bridge.dll" -Destination "$root/runtime/7dtd/Mods/MC7DTD-Bridge/MC7DTD.Bridge.dll"
  $exe='D:\Steam\steamapps\common\7 Days To Die\7DaysToDie.exe';$arguments=@("-UserDataFolder=$root/runtime/7dtd",'-logfile',"$evidence/7dtd-game.log",'-screen-fullscreen','0','-screen-width','960','-screen-height','600');$cwd="$root/runtime/7dtd"
 }
}
$style=if($Component -eq 'bridge'){'Hidden'}else{'Normal'}
$process=Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $cwd -WindowStyle $style -PassThru -RedirectStandardOutput "$evidence/$Component-stdout.log" -RedirectStandardError "$evidence/$Component-stderr.log"
@{component=$Component;pid=$process.Id;started=(Get-Date).ToString('o');evidence=$evidence}|ConvertTo-Json|Set-Content -Encoding utf8 "$evidence/$Component-process.json"
Write-Host "$Component started: $($process.Id); evidence $evidence"
