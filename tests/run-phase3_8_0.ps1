$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location -LiteralPath $root
$oldPythonPath=$env:PYTHONPATH
$oldBytecode=$env:PYTHONDONTWRITEBYTECODE
try {
    $env:PYTHONPATH="$root\work\phase3_4-test\python-libs"
    $env:PYTHONDONTWRITEBYTECODE='1'
    $output="$root\work\phase3_8_0-test"
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    # Snapshot this invocation's protected files, never overwrite the original design baseline.
    $files=@(Get-ChildItem -LiteralPath 'minecraft-mod\src','7dtd-mod\src','bridge-server','components','config' -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' -and $_.Extension -in @('.java','.cs','.json','.csproj') })
    $files+=Get-Item -LiteralPath '7dtd-mod\MC7DTD.Mod.csproj','minecraft-mod\build.gradle','docs\entity_components_v1.schema.json','docs\entity_state_v2.schema.json','docs\entity_debug_inspector.schema.json'
    $hashes=@{}
    foreach($file in $files){$hashes[$file.FullName]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}
    $hashes | ConvertTo-Json | Set-Content -LiteralPath "$output\protected-run-before.json" -Encoding UTF8
    if(!(Test-Path -LiteralPath "$output\protected-before.json")){
        Copy-Item -LiteralPath "$output\protected-run-before.json" -Destination "$output\protected-before.json"
    }
    $process=Start-Process -FilePath (Get-Command python).Source -ArgumentList 'tests/equipment-components/test_equipment.py' -WorkingDirectory $root -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput "$output\tests-stdout.log" -RedirectStandardError "$output\tests.log"
    Get-Content -LiteralPath "$output\tests.log" -Tail 5
    if($process.ExitCode -ne 0){throw 'Equipment design contract validation failed'}
    foreach($path in $hashes.Keys){if(!(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $hashes[$path]){throw "Protected file changed: $path"}}
    Write-Host "Equipment Component v1 design PASS; $($hashes.Count) protected files unchanged"
} finally {
    $env:PYTHONPATH=$oldPythonPath
    $env:PYTHONDONTWRITEBYTECODE=$oldBytecode
    Pop-Location
}
