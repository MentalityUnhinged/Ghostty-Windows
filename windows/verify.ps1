param(
 [switch]$UseCurrentBuild,
 [string]$Zig = "$env:USERPROFILE\tools\zig-x86_64-windows-0.16.0\zig.exe"
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
 if (-not $UseCurrentBuild) { & "$PSScriptRoot/build.ps1" -SkipCore -Test; if ($LASTEXITCODE) { throw 'Build/full suite failed' } }
 $full = Get-Content windows/dist/test-results.json -Raw | ConvertFrom-Json
 if (-not $full.ok -or $full.scope -ne 'full') { throw 'A passing full-suite build is required' }
 foreach ($name in @('Ghostty.Windows.exe','Ghostty.Windows.dll','ghostty-win.dll')) {
  if ((Get-FileHash "windows/dist/$name").Hash.ToLowerInvariant() -ne $full.artifacts.$name) { throw "Full-suite artifact changed: $name" }
 }
 $directory = Join-Path $PSScriptRoot 'artifacts/verification'
 New-Item -ItemType Directory -Path $directory -Force | Out-Null
 foreach ($stale in @("$directory/summary.json", "$directory/compression.json", "$PSScriptRoot/dist/verification-results.json")) {
  if (Test-Path -LiteralPath $stale) { Remove-Item -LiteralPath $stale }
 }
 $process = Start-Process -FilePath "$PSScriptRoot/dist/Ghostty.Windows.exe" -ArgumentList @('--test-verification','--artifacts',"`"$directory`"") -WindowStyle Hidden -PassThru
 try {
  if (-not $process.WaitForExit(240000)) { $process.Kill($true); $process.WaitForExit(); throw 'Owned verification process exceeded four-minute bound' }
  $suite = Get-Content "$directory/result.json" -Raw | ConvertFrom-Json
  if ($process.ExitCode -ne 0 -or -not $suite.ok) { throw "Verification failed: $($suite.error)" }
 } finally { $process.Dispose() }
 $manifest = Get-Content windows/dist/build-manifest.json -Raw | ConvertFrom-Json
 $archiveHash = (Get-FileHash zig-out/lib/ghostty-vt-static.lib).Hash.ToLowerInvariant()
 if ($archiveHash -ne $manifest.engine_archive_sha256) { throw 'Probe archive differs from the archive used to build the frontend' }
 New-Item -ItemType Directory -Path windows/bin -Force | Out-Null
 & $Zig cc -O2 -DGHOSTTY_STATIC -I include windows/tools/verification-probe.c zig-out/lib/ghostty-vt-static.lib -o windows/bin/verification-compression.exe
 if ($LASTEXITCODE) { throw 'Native compression probe build failed' }
 $compressionText = & ./windows/bin/verification-compression.exe
 if ($LASTEXITCODE) { throw 'Native production compression probe failed' }
 $compression = $compressionText | ConvertFrom-Json
 $compressionText | Set-Content "$directory/compression.json"
 $summary = @{
  ok = $true
  captured_utc = (Get-Date).ToUniversalTime().ToString('o')
  scope = 'single-host-bounded-verification-not-physical-or-cross-machine-conformance'
  regression_checks = $full.tests.Count
  verification_checks = $suite.tests.Count
  artifacts = $full.artifacts
  engine_archive_sha256 = $archiveHash
  compression_probe_sha256 = (Get-FileHash windows/bin/verification-compression.exe).Hash.ToLowerInvariant()
  known_unsupported = (Get-Content "$directory/verification-metrics.json" -Raw | ConvertFrom-Json).knownUnsupported
  compression = $compression
 }
 $summary | ConvertTo-Json -Depth 8 | Set-Content "$directory/summary.json"
 Copy-Item -LiteralPath "$directory/summary.json" -Destination "$PSScriptRoot/dist/verification-results.json" -Force
 $summary | ConvertTo-Json -Depth 8
} finally { Pop-Location }
