# ============================================================
# air-matrix / Agri-Drone Control Platform
# Open inbound TCP ports required by the DJI dock (run as Administrator)
#
#   1883  MQTT Broker          (dock + backend)
#   1935  RTMP push            (dock pushes live video)
#   8000  HTTP-FLV / HLS       (browser pulls live video)
#   5005  Backend API          (dock downloads KMZ, FileBaseUrl)
#   8888  Frontend dev server  (optional, remote browser)
#
# Usage: right-click this file -> "Run with PowerShell"
#        (must be elevated / Administrator)
# ============================================================

$ports = @{
    "DJI-MQTT-1883"        = 1883
    "DJI-RTMP-1935"        = 1935
    "DJI-Media-HTTP-8000"  = 8000
    "DJI-Backend-API-5005" = 5005
    "DJI-Frontend-8888"    = 8888
}

foreach ($name in $ports.Keys) {
    $port = $ports[$name]
    $existing = Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host ("[skip] already exists : {0} (port {1})" -f $name, $port)
        continue
    }
    try {
        New-NetFirewallRule -DisplayName $name -Direction Inbound -Protocol TCP `
            -LocalPort $port -Action Allow -Profile Any | Out-Null
        Write-Host ("[ ok ] added          : {0} (port {1})" -f $name, $port)
    }
    catch {
        Write-Host ("[fail] {0} : {1}" -f $name, $_.Exception.Message)
    }
}

Write-Host ""
Write-Host "---- current network profile ----"
Get-NetConnectionProfile | Select-Object InterfaceAlias, NetworkCategory | Format-Table -AutoSize

Write-Host "NOTE: On a 'Public' profile Windows blocks inbound by default."
Write-Host "      These rules use -Profile Any so they apply on Public too."
Write-Host "      Optional, switch this network to Private instead:"
Write-Host "        Set-NetConnectionProfile -InterfaceAlias WLAN -NetworkCategory Private"
Write-Host ""
Write-Host "---- verify ----"
Get-NetFirewallRule -DisplayName "DJI-*" |
    Select-Object DisplayName, Direction, Action, Enabled | Format-Table -AutoSize
Write-Host "Done."
