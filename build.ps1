param([string]$GameDir = 'D:\Steam\steamapps\common\7 Days To Die')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
New-Item -ItemType Directory -Path "$PSScriptRoot\work" -Force | Out-Null
$env:DOTNET_CLI_HOME = "$PSScriptRoot\work\dotnet-home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_PACKAGES = "$PSScriptRoot\work\nuget"
$env:GRADLE_USER_HOME = "$PSScriptRoot\work\gradle-home"
$env:TEMP = "$PSScriptRoot\work"
$env:TMP = $env:TEMP
dotnet build bridge-server/BridgeServer.csproj -c Release
if ($LASTEXITCODE) { throw 'Bridge build failed' }
dotnet build 7dtd-mod/MC7DTD.Mod.csproj -c Release "-p:GameDir=$GameDir"
if ($LASTEXITCODE) { throw '7DTD build failed (requires .NET Framework 4.8 reference assemblies and game references)' }
dotnet build tests/client-harness/ClientHarness.csproj -c Release
if ($LASTEXITCODE) { throw 'Client harness build failed' }
Push-Location minecraft-mod
try {
    .\bootstrap-gradle.ps1
    .\gradlew.bat --no-daemon build writeTestClasspath
    if ($LASTEXITCODE) { throw 'Fabric build failed' }
} finally { Pop-Location }

