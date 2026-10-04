param([Parameter(Mandatory=$true)][ValidateSet('bridge','minecraft','7dtd')][string]$Component)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$evidence="$root\docs\phase3_7_3-runtime-evidence"
New-Item -ItemType Directory -Force $evidence | Out-Null
$env:MC7DTD_ROOT=$root;$env:MC7DTD_ENTITY_TEST='1'
$env:TEMP="$root\work";$env:TMP=$env:TEMP
switch($Component){
 'bridge' {
  $exe='dotnet';$arguments=@("$root\bridge-server\bin\Phase373\net10.0\BridgeServer.dll","$root\config\network.json");$cwd=$root
 }
 'minecraft' {
  if(Get-CimInstance Win32_Process | Where-Object {$_.Name -eq 'java.exe' -and $_.CommandLine -like '*runtime-arguments.txt*'}){throw 'Exit the old Minecraft runtime before launching'}
  Copy-Item -LiteralPath "$root\minecraft-mod\build\libs\mc7dtd-bridge-0.1.0.jar" -Destination "$root\runtime\minecraft\mods\mc7dtd-bridge-0.1.0.jar"
  $exe='C:\Program Files\Java\jdk-21.0.12\bin\java.exe';$arguments=@("@$root\runtime\minecraft\runtime-arguments.txt");$cwd="$root\runtime\minecraft"
 }
 '7dtd' {
  if(Get-Process 7DaysToDie -ErrorAction SilentlyContinue){throw 'Exit the old 7DTD runtime before launching'}
  Copy-Item -LiteralPath "$root\7dtd-mod\bin\Phase373\net48\MC7DTD.Bridge.dll" -Destination "$root\runtime\7dtd\Mods\MC7DTD-Bridge\MC7DTD.Bridge.dll"
  $exe='D:\Steam\steamapps\common\7 Days To Die\7DaysToDie.exe';$arguments=@("-UserDataFolder=$root\runtime\7dtd",'-logfile',"$evidence\7dtd-game.log",'-screen-fullscreen','0','-screen-width','960','-screen-height','600');$cwd="$root\runtime\7dtd"
 }
}
# This script is for manual acceptance. The user explicitly requests visible game windows.
$style=if($Component -eq 'bridge'){'Hidden'}else{'Normal'}
$process=Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $cwd -WindowStyle $style -PassThru -RedirectStandardOutput "$evidence\$Component-stdout.log" -RedirectStandardError "$evidence\$Component-stderr.log"
@{component=$Component;pid=$process.Id;started=(Get-Date).ToString('o')}|ConvertTo-Json|Set-Content -Encoding utf8 "$evidence\$Component-process.json"
Write-Host "$Component started: $($process.Id)"
