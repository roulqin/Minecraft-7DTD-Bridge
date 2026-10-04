param([Parameter(Mandatory)][string]$ProjectRoot,[Parameter(Mandatory)][string]$Evidence,[ValidatePattern('^Phase[0-9]+$')][string]$BuildConfiguration='Phase3853')
$ErrorActionPreference='Stop'
$env:MC7DTD_ROOT=$ProjectRoot
$globalInfo='D:/Steam/steamapps/common/7 Days To Die/Mods/MC7DTD.Bridge/ModInfo.xml'
$disabledInfo="$globalInfo.avatar-runtime-disabled"
$disabled=$false
try {
 if(Get-Process 7DaysToDie -ErrorAction SilentlyContinue){throw 'Old 7DTD is still running'}
 if(Test-Path -LiteralPath $disabledInfo){throw 'Isolation backup already exists; restore it before launch'}
 if(Test-Path -LiteralPath $globalInfo){Move-Item -LiteralPath $globalInfo -Destination $disabledInfo;$disabled=$true}
 $target="$ProjectRoot/runtime/7dtd/Mods/MC7DTD-Bridge"
 New-Item -ItemType Directory -Force $target | Out-Null
 Copy-Item -LiteralPath "$ProjectRoot/7dtd-mod/bin/$BuildConfiguration/net48/MC7DTD.Bridge.dll","$ProjectRoot/7dtd-mod/ModInfo.xml" -Destination $target
 $p=Start-Process -FilePath 'D:/Steam/steamapps/common/7 Days To Die/7DaysToDie.exe' -ArgumentList @("-UserDataFolder=$ProjectRoot/runtime/7dtd",'-logfile',"$Evidence/7dtd-game.log",'-screen-fullscreen','0','-screen-width','960','-screen-height','600') -WorkingDirectory "$ProjectRoot/runtime/7dtd" -WindowStyle Normal -PassThru
 @{component='7dtd';pid=$p.Id;started=(Get-Date).ToString('o')}|ConvertTo-Json|Set-Content "$Evidence/7dtd-process.json"
 $p.WaitForExit()
}catch {$_ | Out-String | Set-Content "$Evidence/7dtd-launch-error.txt";throw}
finally {if($disabled){Move-Item -LiteralPath $disabledInfo -Destination $globalInfo}}
