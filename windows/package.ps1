param([string]$Destination = "$PSScriptRoot\Ghostty-Windows-Preview.zip")
$ErrorActionPreference = 'Stop'
$dist = Join-Path $PSScriptRoot 'dist'
$report = Get-Content -LiteralPath (Join-Path $dist 'test-results.json') -Raw | ConvertFrom-Json
if (-not $report.ok -or $report.scope -ne 'full') { throw 'A passing full-suite report is required before packaging' }
foreach ($name in @('Ghostty.Windows.exe','Ghostty.Windows.dll','ghostty-win.dll')) {
 $expected = $report.artifacts.$name
 if (-not $expected -or (Get-FileHash -LiteralPath (Join-Path $dist $name)).Hash.ToLower() -ne $expected) {
  throw "Tested artifact has changed: $name. Rebuild with -Test."
 }
}
$verificationPath = Join-Path $dist 'verification-results.json'
if (Test-Path -LiteralPath $verificationPath) {
 $verification = Get-Content -LiteralPath $verificationPath -Raw | ConvertFrom-Json
 if (-not $verification.ok -or $verification.scope -ne 'single-host-bounded-verification-not-physical-or-cross-machine-conformance') { throw 'Invalid extended verification evidence' }
 foreach ($name in @('Ghostty.Windows.exe','Ghostty.Windows.dll','ghostty-win.dll')) {
  if ($verification.artifacts.$name -ne $report.artifacts.$name) { throw "Extended verification artifact has changed: $name" }
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
