param([string]$UnityEditor='D:/wenjian/xuexi/2022.3.62f3c1/Editor/Unity.exe')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$project=Join-Path $root 'assets/avatar/production_unity'
$evidence=Join-Path $root 'work/phase3854-test'
New-Item -ItemType Directory -Force $evidence,"$root/assets/avatar/bundles/windows" | Out-Null
python "$root/tests/produce-avatar-resources.py"
if($LASTEXITCODE){throw 'Avatar source production failed'}
$env:AVATAR_RESOURCE_EVIDENCE=$evidence
$env:AVATAR_RESOURCE_OUTPUT=Join-Path $root 'assets/avatar/bundles/windows'
$p=Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-quit','-projectPath',$project,'-executeMethod','AvatarResourceBuild.Build','-logFile',"$evidence/unity-production.log") -WindowStyle Hidden -PassThru
$p.Id | Set-Content "$evidence/unity-build.pid"
$p.WaitForExit()
if($p.ExitCode -ne 0){throw "Unity production failed: exit $($p.ExitCode); read $evidence/unity-production.log"}
if(!(Test-Path "$evidence/unity-resource-results.txt") -or !(Select-String -Path "$evidence/unity-production.log" -Pattern 'AVATAR_RESOURCE_PRODUCTION PASS')){throw 'Unity did not complete resource verification'}
$bundle=Join-Path $root 'assets/avatar/bundles/windows/minecraft_avatar_v1'
@{resource_version='minecraft_avatar_v1';avatar_id='minecraft_alex_default';model='minecraft_humanoid';variant='alex_slim';skeleton='minecraft_avatar_v1';platform='windows';prefab='Assets/Avatar/Generated/MinecraftAvatarPrefab.prefab';controller='Assets/Avatar/Generated/MinecraftAvatarAnimator.controller';humanoid_avatar='Assets/Avatar/Generated/MinecraftAvatarHumanoid.asset';animation_avatar='Assets/Avatar/Generated/MinecraftAvatarGeneric.asset';animator_backend='generic_transform_controller';bundle='bundles/windows/minecraft_avatar_v1';bundle_sha256=(Get-FileHash -LiteralPath $bundle -Algorithm SHA256).Hash;build_status='verified_editor'} | ConvertTo-Json | Set-Content -Encoding utf8 "$root/assets/avatar/resource_manifest.json"
Write-Host 'Unity Avatar resource production PASS'
