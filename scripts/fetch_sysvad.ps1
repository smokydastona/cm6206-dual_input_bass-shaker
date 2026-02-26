[CmdletBinding()]
param(
    # Git ref for the Windows-driver-samples archive. Common values:
    # - "main" (default)
    # - a commit SHA
    [Parameter(Mandatory = $false)]
    [string]$Ref = "main",

    # Destination folder where the SysVAD sample will be extracted.
    # This repo ignores this folder via .gitignore.
    [Parameter(Mandatory = $false)]
    [string]$Destination = (Join-Path $PSScriptRoot "..\virtual_audio_driver\src\sysvad")
)

$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    $item = Get-Item -LiteralPath $Path -ErrorAction SilentlyContinue
    if ($null -ne $item) { return $item.FullName }
    return $null
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

$destinationParent = Split-Path -Parent $Destination
$destinationLeaf = Split-Path -Leaf $Destination

if (-not (Test-Path -LiteralPath $destinationParent)) {
    New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
}

$destinationParentFull = (Resolve-Path -LiteralPath $destinationParent).Path
$destinationFull = Join-Path $destinationParentFull $destinationLeaf

$tmpRoot = Join-Path $repoRoot "virtual_audio_driver\.tmp"
New-Item -ItemType Directory -Path $tmpRoot -Force | Out-Null

$zipPath = Join-Path $tmpRoot "windows-driver-samples-$Ref.zip"
$extractRoot = Join-Path $tmpRoot "extract-$Ref"

if (Test-Path -LiteralPath $extractRoot) {
    Remove-Item -LiteralPath $extractRoot -Recurse -Force
}

$archiveUrl = "https://github.com/microsoft/Windows-driver-samples/archive/$Ref.zip"
Write-Host "Downloading SysVAD source from: $archiveUrl"
Invoke-WebRequest -Uri $archiveUrl -OutFile $zipPath

Write-Host "Extracting archive..."
Expand-Archive -LiteralPath $zipPath -DestinationPath $extractRoot -Force

# The extracted folder name is typically Windows-driver-samples-<ref>
$top = Get-ChildItem -LiteralPath $extractRoot | Where-Object { $_.PSIsContainer } | Select-Object -First 1
if ($null -eq $top) {
    throw "Unexpected archive layout: no top-level directory found in $extractRoot"
}

$sysvadSource = Join-Path $top.FullName "audio\sysvad"
if (-not (Test-Path -LiteralPath $sysvadSource)) {
    throw "SysVAD sample not found at expected path: $sysvadSource"
}

Write-Host "Copying SysVAD sample to: $destinationFull"
if (Test-Path -LiteralPath $destinationFull) {
    Remove-Item -LiteralPath $destinationFull -Recurse -Force
}
New-Item -ItemType Directory -Path $destinationFull -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $sysvadSource "*") -Destination $destinationFull -Recurse -Force

$refFile = Join-Path (Split-Path -Parent $destinationFull) "sysvad.upstream.ref.txt"
"$Ref" | Out-File -LiteralPath $refFile -Encoding ascii

Write-Host "Done. SysVAD is now available locally (ignored by git):"
Write-Host "  $destinationFull"
Write-Host "Next steps: open the SysVAD solution in Visual Studio with WDK installed, then follow docs/virtual_audio_driver/01_sysvad_patch_map.md."
