param(
  [string]$Version = "0.0.0",
  [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'

Write-Host "Publishing app..." -ForegroundColor Cyan

dotnet publish "cm6206_dual_router/Cm6206DualRouter.csproj" `
  -c $Configuration `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:PublishReadyToRun=true `
  /p:DebugType=None `
  /p:DebugSymbols=false `
  /p:Version=$Version `
  -o "artifacts/cm6206_dual_router_win-x64"

Write-Host "Generating assets into publish folder..." -ForegroundColor Cyan

dotnet run -c $Configuration --project tools/Cm6206AssetGenerator -- --out "artifacts/cm6206_dual_router_win-x64/assets/generated" --theme all

# Inno Setup compiler
$possible = @(
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)
$iscc = $possible | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
  throw "Inno Setup not found. Install Inno Setup 6, then rerun. (Expected ISCC.exe under Program Files)"
}

Write-Host "Building installer..." -ForegroundColor Cyan
& $iscc "installer/Cm6206DualRouter.iss" "/DMyAppVersion=$Version"

Write-Host "Done. See artifacts/installer" -ForegroundColor Green
