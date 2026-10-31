param([string]$Destination = "$PSScriptRoot\Ghostty-Windows-Preview.zip")
$ErrorActionPreference = 'Stop'
$dist = Join-Path $PSScriptRoot 'dist'
$report = Get-Content -LiteralPath (Join-Path $dist 'test-results.json') -Raw | ConvertFrom-Json
if (-not $report.ok) { throw 'A passing smoke report is required before packaging' }
foreach ($name in @('Ghostty.Windows.exe','Ghostty.Windows.dll','ghostty-win.dll')) {
 $expected = $report.artifacts.$name
 if (-not $expected -or (Get-FileHash -LiteralPath (Join-Path $dist $name)).Hash.ToLower() -ne $expected) {
  throw "Tested artifact has changed: $name. Rebuild with -Test."
 }
}
$evidencePath = Join-Path $dist 'rea-executable-evidence.json'
$iconReport = Get-Content -LiteralPath (Join-Path $dist 'icon-shell-check.json') -Raw | ConvertFrom-Json
if (-not $iconReport.ok -or $iconReport.executable_sha256 -ne $report.artifacts.'Ghostty.Windows.exe') {
 throw 'Windows Shell icon verification does not match the tested executable'
}
if (Test-Path -LiteralPath $evidencePath) {
 $evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
 if ($evidence.normalized_result.artifact.sha256 -ne $report.artifacts.'Ghostty.Windows.exe') {
  throw 'REA artifact evidence does not match the tested executable'
 }
}
if (Test-Path -LiteralPath $Destination) {
 $backup = $Destination + '.backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
 Copy-Item -LiteralPath $Destination -Destination $backup
}
Compress-Archive -Path "$dist\*" -DestinationPath $Destination -Force -CompressionLevel Optimal
Get-Item -LiteralPath $Destination | Select-Object FullName,Length
