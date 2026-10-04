$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$output = "$root\work\phase3_6_0-test"
$libraries = "$root\work\phase3_4-test\python-libs"
New-Item -ItemType Directory -Force $output | Out-Null
$env:TEMP = "$root\work"
$env:TMP = $env:TEMP
$env:PYTHONPATH = $libraries
$env:PYTHONDONTWRITEBYTECODE = '1'
if (-not (Test-Path "$libraries\jsonschema")) {
    python -m pip install --disable-pip-version-check --target $libraries --cache-dir "$root\work\pip-cache" -r tests/entity-types/requirements.txt
    if ($LASTEXITCODE) { throw 'Schema dependencies unavailable' }
}
if (-not (Test-Path "$output\protected-before.json")) {
    python tests/bidirectional-protocol/test_contract.py --snapshot
    if ($LASTEXITCODE) { throw 'Could not capture source fingerprint' }
}
python tests/bidirectional-protocol/test_contract.py 2>&1 | Tee-Object "$output\tests.log"
if ($LASTEXITCODE) { throw 'Bidirectional protocol design verification failed' }
Write-Host 'Phase 3.6.0 offline design checks passed'
