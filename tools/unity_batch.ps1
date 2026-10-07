# Runs a static editor method of the test project in batch mode and prints the relevant log lines.
#   pwsh tools/unity_batch.ps1 [-Method M3E4Unity.Editor.Dev.M3Batch.Catalog] [-Project ../VRC_M3E4Unity]
param(
  [string]$Method = 'M3E4Unity.Editor.Dev.M3Batch.Catalog',
  [string]$Project = (Join-Path $PSScriptRoot '../../VRC_M3E4Unity'),
  [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe',
  [int]$TimeoutSec = 1500
)
$proj = (Resolve-Path $Project).Path
$log = Join-Path $proj 'm3batch.log'
$args = @('-batchmode', '-projectPath', "`"$proj`"", '-executeMethod', $Method, '-logFile', "`"$log`"")
$p = Start-Process -FilePath $Unity -ArgumentList $args -PassThru
if (-not $p.WaitForExit($TimeoutSec * 1000)) { $p.Kill(); Write-Output "TIMEOUT" }
Write-Output "exit $($p.ExitCode)"
Select-String -Path $log -Pattern '^\[M3Batch\]|error CS\d+|Exception:|^\s+at M3E4Unity|\[UdonSharp\].*M3E4Unity|UdonSharp.*error.*M3' |
  Where-Object { $_.Line -notmatch 'Start importing' } |
  Select-Object -First 60 |
  ForEach-Object { $_.Line.Substring(0, [Math]::Min(400, $_.Line.Length)) }
