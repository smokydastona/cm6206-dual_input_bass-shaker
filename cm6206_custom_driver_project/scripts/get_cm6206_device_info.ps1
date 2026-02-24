<#
Collects CM6206 device info (hardware IDs, current driver, endpoints) to support a custom driver / routing project.
Run in an elevated PowerShell if possible.
#>

$ErrorActionPreference = 'Stop'

Write-Host "== USB audio devices (PnP) ==" -ForegroundColor Cyan
Get-PnpDevice -Class 'AudioEndpoint' -ErrorAction SilentlyContinue | Sort-Object FriendlyName | Format-Table -AutoSize FriendlyName, Status, InstanceId

Write-Host "" 
Write-Host "== USB devices matching VID_0D8C (C-Media) ==" -ForegroundColor Cyan
$cm = Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -match 'USB\\VID_0D8C' }
$cm | Format-Table -AutoSize FriendlyName, Class, Status, InstanceId

Write-Host "" 
Write-Host "== Driver packages for CMUAC (if installed) ==" -ForegroundColor Cyan
pnputil /enum-drivers | Select-String -Pattern 'CMUAC|C-MEDIA|Cmedia|C-Media' -Context 0,6

Write-Host "" 
Write-Host "== Detailed properties for each VID_0D8C device ==" -ForegroundColor Cyan
foreach ($d in $cm) {
    Write-Host "---" -ForegroundColor DarkGray
    Write-Host $d.FriendlyName -ForegroundColor Yellow
    Write-Host $d.InstanceId -ForegroundColor DarkYellow

    try {
        Get-PnpDeviceProperty -InstanceId $d.InstanceId | 
            Where-Object { $_.KeyName -in @(
                'DEVPKEY_Device_HardwareIds',
                'DEVPKEY_Device_CompatibleIds',
                'DEVPKEY_Device_DriverInfPath',
                'DEVPKEY_Device_DriverVersion',
                'DEVPKEY_Device_DriverProvider',
                'DEVPKEY_Device_DriverDate'
            ) } | Format-List
    } catch {
        Write-Host "(Could not read properties: $($_.Exception.Message))" -ForegroundColor DarkRed
    }
}

Write-Host "" 
Write-Host "Done. Save this output into a text file when you’re ready." -ForegroundColor Green
