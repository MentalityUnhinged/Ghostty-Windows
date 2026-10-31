param([string]$Dotnet = "$env:USERPROFILE\tools\dotnet\dotnet.exe")
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& $Dotnet run --project "$PSScriptRoot/tools/IconBuilder/IconBuilder.csproj" -c Release -- $root "$PSScriptRoot/Assets/ghostty.ico"
if ($LASTEXITCODE) { throw 'Icon container generation failed' }
Copy-Item "$root/images/gnome/256.png" "$PSScriptRoot/Assets/ghostty.png" -Force
