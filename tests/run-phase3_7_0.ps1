$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$env:PYTHONPATH="$root\work\phase3_4-test\python-libs"
$env:PYTHONDONTWRITEBYTECODE='1'
$env:TEMP="$root\work"
$env:TMP=$env:TEMP
python -c "import sys; sys.path.insert(0,'tests/state-components'); from component_contract import capture_baseline; capture_baseline()"
if($LASTEXITCODE){throw 'Could not capture protected files'}
python tests/state-components/test_components.py *> work/phase3_7_0-test/tests.log
if($LASTEXITCODE){Get-Content work/phase3_7_0-test/tests.log -Tail 35;throw 'Component contract validation failed'}
Get-Content work/phase3_7_0-test/tests.log -Tail 4
