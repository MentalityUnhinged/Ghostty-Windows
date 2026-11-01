param(
 [string]$Zig = "$env:USERPROFILE\tools\zig-x86_64-windows-0.16.0\zig.exe",
 [string]$Dotnet = "$env:USERPROFILE\tools\dotnet\dotnet.exe",
 [switch]$SkipCore,
 [switch]$Test
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
 & "$PSScriptRoot/make-icon.ps1" -Dotnet $Dotnet
 if (-not $SkipCore) { & $Zig build -Demit-lib-vt -Doptimize=ReleaseFast -Dtarget=x86_64-windows-gnu -Dcpu=baseline; if ($LASTEXITCODE) { throw 'libghostty build failed' } }
 & $Zig cc -target x86_64-windows-gnu -mcpu=baseline -shared -O2 -DGHOSTTY_STATIC -I include windows/native/bridge.c zig-out/lib/ghostty-vt-static.lib -o windows/native/ghostty-win.dll
 if ($LASTEXITCODE) { throw 'Windows adapter build failed' }
 & $Dotnet publish windows/Ghostty.Windows.csproj -c Release -r win-x64 --self-contained true -o windows/dist
 if ($LASTEXITCODE) { throw 'Windows frontend build failed' }
 Copy-Item LICENSE windows/dist/GHOSTTY-LICENSE.txt -Force
 Copy-Item windows/README.md windows/dist/GHOSTTY-WINDOWS-README.md -Force
 Copy-Item KEYBINDS.md windows/dist/KEYBINDS.md -Force
 Copy-Item windows/PORTING.md windows/dist/PORTING.md -Force
 Copy-Item windows/AUDIT.md windows/dist/AUDIT.md -Force
 Copy-Item windows/CONFIG_SUPPORT.md, windows/ACTION_SUPPORT.md, windows/VERIFICATION.md windows/dist/ -Force
 Copy-Item windows/Assets/ghostty.ico windows/dist/Ghostty.Windows.ico -Force
 & "$PSScriptRoot/verify-icon.ps1"
 # Reports from a previous binary must never ship as evidence for this build.
 foreach ($generated in @('windows/dist/test-results.json', 'windows/dist/rea-executable-evidence.json', 'windows/dist/verification-results.json')) {
  if (Test-Path -LiteralPath $generated) { Remove-Item -LiteralPath $generated }
 }
 $engineBase = (Get-Content -LiteralPath "$PSScriptRoot/engine-base.txt" -Raw).Trim()
 & git diff --quiet $engineBase -- src include build.zig build.zig.zon
 $engineSourceMatches = $LASTEXITCODE -eq 0
 @{
  upstream_repository = 'https://github.com/ghostty-org/ghostty'
  upstream_commit = $engineBase
  frontend_commit = (& git rev-parse HEAD)
  source_tree_dirty = [bool](& git status --porcelain)
  engine_source_tree_matches_base = $engineSourceMatches
  engine_archive_rebuilt_this_run = (-not $SkipCore)
  frontend = 'windows/'
  status = 'experimental-preview-not-feature-equivalent'
  target = 'x86_64-windows-gnu; baseline CPU'
  zig = (& $Zig version)
  dotnet_sdk = (& $Dotnet --version)
  executable_sha256 = (Get-FileHash windows/dist/Ghostty.Windows.exe).Hash.ToLower()
  managed_frontend_sha256 = (Get-FileHash windows/dist/Ghostty.Windows.dll).Hash.ToLower()
  adapter_sha256 = (Get-FileHash windows/dist/ghostty-win.dll).Hash.ToLower()
  engine_archive_sha256 = (Get-FileHash zig-out/lib/ghostty-vt-static.lib).Hash.ToLower()
 } | ConvertTo-Json | Set-Content windows/dist/build-manifest.json
 if ($Test) {
  if (Test-Path -LiteralPath windows/artifacts/result.json) { Remove-Item -LiteralPath windows/artifacts/result.json }
  $proc = Start-Process -FilePath "$PSScriptRoot\dist\Ghostty.Windows.exe" -ArgumentList @('--self-test','--artifacts',"`"$PSScriptRoot\artifacts`"") -PassThru -WindowStyle Hidden
  if (-not $proc.WaitForExit(180000)) { Stop-Process -Id $proc.Id; throw 'Smoke tests timed out' }
  Get-Content "$PSScriptRoot\artifacts\result.json"
  if ($proc.ExitCode -ne 0) { throw 'Smoke tests failed' }
  Copy-Item windows/artifacts/result.json windows/dist/test-results.json -Force
 }
} finally { Pop-Location }
