<#
Driver-only installer for the C-Media CM6206 package.
- Requires Admin.
- Uses pnputil to add/install the INF.

This does NOT run the vendor setup UI.
#>

$ErrorActionPreference = 'Stop'

function Assert-Admin {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Please run this script as Administrator (right-click -> Run with PowerShell).'
    }
}

Assert-Admin

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$infPath = Join-Path $scriptDir 'package\CMUAC.inf'

if (-not (Test-Path $infPath)) {
        throw @"
INF not found: $infPath

This repo does not ship vendor driver payload files.

Create the folder:
    $scriptDir\package\
and place CMUAC.inf (plus the matching CAT/SYS/DLL files) inside it,
then re-run this script.
"@
}

Write-Host "Installing driver from: $infPath" -ForegroundColor Cyan

# Add + install
pnputil /add-driver "$infPath" /install | Out-Host

Write-Host ''
Write-Host 'Done. If the device does not immediately switch to the C-Media driver, unplug/replug the CM6206 or reboot.' -ForegroundColor Green
