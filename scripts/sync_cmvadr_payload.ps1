<#
.SYNOPSIS
  Sync CMVADR driver artifacts into virtual_audio_driver_payload for packaging.

.DESCRIPTION
  Copies CMVADR.inf + (optionally) CMVADR.sys/CMVADR.cat from the driver workspace
  (typically the git submodule under external/dual-cm6206-driver) into:
    virtual_audio_driver_payload/WIN10/Driver/

  This repo intentionally commits only the minimal payload needed by the Inno Setup
  installer. The full driver source/workspace remains in a separate repo.
#>

[CmdletBinding()]
param(
  # Path to the CMVADR driver workspace (repo root).
  [Parameter(Mandatory = $false)]
  [string]$DriverRepoDir,

  # Destination payload directory used by the installer.
  [Parameter(Mandatory = $false)]
  [string]$PayloadDir,

  # Allow syncing only the INF (useful while scaffolding).
  # For packaging/installer builds, leave this OFF so we require INF+SYS+CAT.
  [Parameter(Mandatory = $false)]
  [switch]$AllowInfOnly
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($DriverRepoDir)) {
  $DriverRepoDir = Join-Path $scriptDir "..\external\dual-cm6206-driver\cmvadr"
}
if ([string]::IsNullOrWhiteSpace($PayloadDir)) {
  $PayloadDir = Join-Path $scriptDir "..\virtual_audio_driver_payload\WIN10\Driver"
}

$driverRoot = (Resolve-Path -LiteralPath $DriverRepoDir).Path
$payloadRoot = (Resolve-Path -LiteralPath $PayloadDir -ErrorAction SilentlyContinue)
if (-not $payloadRoot) {
  New-Item -ItemType Directory -Force -Path $PayloadDir | Out-Null
  $payloadRoot = (Resolve-Path -LiteralPath $PayloadDir).Path
}
else {
  $payloadRoot = $payloadRoot.Path
}

function Find-SingleFile {
  param(
    [Parameter(Mandatory = $true)][string]$Root,
    [Parameter(Mandatory = $true)][string]$FileName,
    [Parameter(Mandatory = $true)][string]$Description,
    [Parameter(Mandatory = $false)][switch]$Required
  )

  $matches = Get-ChildItem -LiteralPath $Root -Recurse -Force -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ieq $FileName } |
    Sort-Object LastWriteTime -Descending

  if (-not $matches -or $matches.Count -eq 0) {
    if ($Required) {
      throw "Missing $Description ($FileName) under: $Root"
    }
    return $null
  }

  if ($matches.Count -gt 1) {
    Write-Warning "Multiple $Description matches found for $FileName. Using newest: $($matches[0].FullName)"
  }

  return $matches[0].FullName
}

$infPath = Join-Path $driverRoot "driver\CMVADR.inf"
if (-not (Test-Path -LiteralPath $infPath)) {
  # Fall back to searching (in case layout changes)
  $infPath = Find-SingleFile -Root $driverRoot -FileName "CMVADR.inf" -Description "INF" -Required
}

$sysPath = Find-SingleFile -Root $driverRoot -FileName "CMVADR.sys" -Description "SYS" -Required:(-not $AllowInfOnly)
$catPath = Find-SingleFile -Root $driverRoot -FileName "CMVADR.cat" -Description "CAT" -Required:(-not $AllowInfOnly)

Write-Host "Syncing CMVADR payload..." -ForegroundColor Cyan
Write-Host "  Driver repo: $driverRoot"
Write-Host "  Payload dir: $payloadRoot"

Copy-Item -LiteralPath $infPath -Destination (Join-Path $payloadRoot "CMVADR.inf") -Force
Write-Host "  Copied: CMVADR.inf" -ForegroundColor Green

Copy-Item -LiteralPath $sysPath -Destination (Join-Path $payloadRoot "CMVADR.sys") -Force
Write-Host "  Copied: CMVADR.sys" -ForegroundColor Green

Copy-Item -LiteralPath $catPath -Destination (Join-Path $payloadRoot "CMVADR.cat") -Force
Write-Host "  Copied: CMVADR.cat" -ForegroundColor Green

Write-Host "Done." -ForegroundColor Green
