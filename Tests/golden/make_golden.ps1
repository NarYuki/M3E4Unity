# Builds the upstream Java sources (annotation-only dependencies stripped) and
# writes golden.txt, the reference output that the C# port must reproduce.
#   pwsh Tests/golden/make_golden.ps1 <material-color-utilities>/java
param([Parameter(Mandatory = $true)][string]$JavaDir)
$ErrorActionPreference = 'Stop'
$work = Join-Path ([IO.Path]::GetTempPath()) 'm3e4unity_golden'
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $work | Out-Null
Copy-Item (Join-Path $JavaDir '*') $work -Recurse
Get-ChildItem $work -Recurse -Filter *.java | ForEach-Object {
  $t = [IO.File]::ReadAllText($_.FullName)
  $t = [regex]::Replace($t, '(?m)^import (androidx|com\.google\.errorprone)[^;]*;\r?\n', '')
  $t = [regex]::Replace($t, '@(NonNull|Nullable|CanIgnoreReturnValue|CheckReturnValue|Var)\b\s*', '')
  [IO.File]::WriteAllText($_.FullName, $t)
}
Copy-Item (Join-Path $PSScriptRoot 'Golden.java') $work
$classes = Join-Path $work 'classes'
$sources = Get-ChildItem $work -Recurse -Filter *.java | ForEach-Object FullName
javac -nowarn -d $classes @sources
if ($LASTEXITCODE -ne 0) { throw 'javac failed' }
java -cp $classes Golden | Set-Content -Encoding utf8NoBOM (Join-Path $PSScriptRoot 'golden.txt')
