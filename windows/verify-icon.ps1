param([string]$Executable = "$PSScriptRoot\dist\Ghostty.Windows.exe")
$ErrorActionPreference = 'Stop'
if (-not ('GhosttyShellIconVerifier' -as [type])) {
 Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class GhosttyShellIconVerifier {
 [DllImport("shell32.dll", EntryPoint="ExtractIconExW", CharSet=CharSet.Unicode, ExactSpelling=true, SetLastError=true)]
 public static extern uint Extract(string path,int index,out IntPtr large,out IntPtr small,uint count);
 [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr handle);
}
'@
}
$large = [IntPtr]::Zero; $small = [IntPtr]::Zero
$count = [GhosttyShellIconVerifier]::Extract($Executable,0,[ref]$large,[ref]$small,1)
try {
 # Both requested handles contribute to the returned count; do not require count == 1.
 if ($count -eq 0 -or $count -eq [uint32]::MaxValue -or $large -eq [IntPtr]::Zero -or $small -eq [IntPtr]::Zero) {
  throw "Windows Shell could not extract both icon sizes: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
 }
 $report = @{ ok = $true; large_icon = $true; small_icon = $true; extracted = $count; executable_sha256 = (Get-FileHash -LiteralPath $Executable).Hash.ToLower() }
 $report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path (Split-Path $Executable -Parent) 'icon-shell-check.json')
 Write-Output 'Windows Shell successfully extracted both application icons'
} finally {
 if ($large -ne [IntPtr]::Zero) { [GhosttyShellIconVerifier]::DestroyIcon($large) | Out-Null }
 if ($small -ne [IntPtr]::Zero) { [GhosttyShellIconVerifier]::DestroyIcon($small) | Out-Null }
}
