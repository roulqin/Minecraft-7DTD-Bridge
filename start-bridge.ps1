$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$dll = Join-Path $PSScriptRoot 'bridge-server\bin\Release\net10.0\BridgeServer.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw 'Run .\build.ps1 first' }
New-Item -ItemType Directory -Path "$PSScriptRoot\work\logs" -Force | Out-Null
dotnet $dll "$PSScriptRoot\config\network.json" 2>&1 | Tee-Object -FilePath "$PSScriptRoot\work\logs\bridge.log"
if ($LASTEXITCODE) { throw "Bridge exited with code $LASTEXITCODE" }
