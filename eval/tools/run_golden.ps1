# Wrapper: calls run_golden.py (UTF-8 safe module names)
# From client repo root: .\eval\tools\run_golden.ps1

$ErrorActionPreference = "Stop"
$ClientRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
python (Join-Path $PSScriptRoot "run_golden.py") @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
