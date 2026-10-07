# Regenerates Runtime/Core/MaterialColor from a material-color-utilities checkout
# and builds the verification project.
#   pwsh tools/port_mcu.ps1 <path to material-color-utilities>/java
param([Parameter(Mandatory = $true)][string]$JavaDir)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'Packages/com.m3e4unity/Runtime/Core/MaterialColor'
python (Join-Path $PSScriptRoot 'java2cs.py') $JavaDir $out | Out-Null
python (Join-Path $PSScriptRoot 'java2cs_fixups.py') $out
if ($LASTEXITCODE -ne 0) { throw 'some fixups did not apply' }
dotnet build (Join-Path $root 'Tests/CoreTests') -nologo -v q
