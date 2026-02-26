[CmdletBinding()]
param(
    # Path to the CMVADR payload folder (containing CMVADR.inf)
    [Parameter(Mandatory = $false)]
    [string]$PayloadDir = (Join-Path $PSScriptRoot "..\virtual_audio_driver_payload\WIN10\Driver")
)

$ErrorActionPreference = "Stop"

$payloadFull = (Resolve-Path -LiteralPath $PayloadDir).Path
$inf = Join-Path $payloadFull "CMVADR.inf"

if (-not (Test-Path -LiteralPath $inf)) {
    throw "CMVADR.inf not found at: $inf"
}

Write-Host "Installing CMVADR virtual endpoints driver via pnputil..."
Write-Host "INF: $inf"

$pnputil = Join-Path $env:WINDIR "System32\pnputil.exe"
& $pnputil /add-driver $inf /install

Write-Host "Done. Verify endpoints appear in Sound Settings: Virtual Game Audio / Virtual Shaker Audio."
Write-Host "Then probe IOCTL interface:" 
Write-Host "  dotnet run -c Release --project cm6206_dual_router/Cm6206DualRouter.csproj -- --probe-cmvadr"
