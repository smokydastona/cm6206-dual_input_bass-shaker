param(
  [string]$OutDir = "assets/generated",
  [ValidateSet("dark","light","all")][string]$Theme = "all"
)

$ErrorActionPreference = 'Stop'

Write-Host "Generating assets into: $OutDir (theme=$Theme)" -ForegroundColor Cyan

# Requires .NET SDK. If missing, install from: https://aka.ms/dotnet/download

dotnet run -c Release --project tools/Cm6206AssetGenerator -- --out $OutDir --theme $Theme
