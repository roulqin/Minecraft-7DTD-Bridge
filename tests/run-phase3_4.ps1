$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$output = "$root\work\phase3_4-test"
New-Item -ItemType Directory -Force $output | Out-Null
$env:TEMP = "$root\work"
$env:TMP = $env:TEMP
$env:PYTHONPATH = "$output\python-libs"
$env:PYTHONDONTWRITEBYTECODE = '1'
if (-not (Test-Path "$output\python-libs\jsonschema")) {
    python -m pip install --disable-pip-version-check --target "$output\python-libs" --cache-dir "$root\work\pip-cache" -r tests/entity-types/requirements.txt
    if ($LASTEXITCODE) { throw 'Schema test dependency installation failed' }
}
python tests/entity-types/test_catalog.py 2>&1 | Tee-Object "$output\catalog-tests.log"
if ($LASTEXITCODE) { throw 'Entity type design tests failed' }
& "$root\tests\run-phase3_2.ps1" *> "$output\lifecycle-regression.log"
& "$root\tests\run-phase3_3.ps1" *> "$output\marker-regression.log"
Write-Host "Phase 3.4 tests passed. Evidence: $output"
