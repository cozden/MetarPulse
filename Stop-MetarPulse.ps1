# MetarPulse servisleri durdur
# Komut satirina gore hedef alinir - Explorer.exe etkilenmez

Write-Host "=========================================="
Write-Host "Stopping MetarPulse Solution"
Write-Host "=========================================="

# [1] dotnet run ile calisan MetarPulse surecleri
Write-Host ""
Write-Host "[1/4] Stopping MetarPulse dotnet processes..."
$dotnetProcs = Get-WmiObject Win32_Process |
Where-Object { $_.Name -eq "dotnet.exe" -and $_.CommandLine -match "MetarPulse" }
foreach ($p in $dotnetProcs) {
    Write-Host "  Killing dotnet.exe PID $($p.ProcessId)"
    Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
}

# MAUI uygulamasinin kendi exe'si
foreach ($name in @("MetarPulse.Maui", "MetarPulse.Api", "MetarPulse.Admin", "MetarPulse.Web")) {
    $proc = Get-Process -Name $name -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "  Killing $name PID $($proc.Id)"
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }
}

# [2] ngrok
Write-Host ""
Write-Host "[2/4] Stopping ngrok..."
Stop-Process -Name "ngrok" -Force -ErrorAction SilentlyContinue

# [3] Port temizleme - sadece dotnet/MetarPulse surecleri
Write-Host ""
Write-Host "[3/4] Freeing ports 5000 / 5225 / 5269..."
foreach ($port in @(5000, 5225, 5269)) {
    $conn = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($conn) {
        $owningPid = $conn.OwningProcess
        $proc = Get-Process -Id $owningPid -ErrorAction SilentlyContinue
        if ($proc -and $proc.Name -match "dotnet|MetarPulse") {
            Write-Host "  Freeing port $port (PID $owningPid - $($proc.Name))"
            Stop-Process -Id $owningPid -Force -ErrorAction SilentlyContinue
        }
        elseif ($proc) {
            Write-Host "  Skipping port $port - owned by $($proc.Name) (not MetarPulse)"
        }
    }
}

# [4] Docker
Write-Host ""
Write-Host "[4/4] Stopping Docker services..."
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$composeFile = Join-Path $scriptDir "docker-compose.yml"
$dockerBin = "C:\Program Files\Docker\Docker\resources\bin"
if (Test-Path "$dockerBin\docker.exe") { $env:PATH = "$dockerBin;$env:PATH" }
if (Test-Path $composeFile) {
    docker compose -f $composeFile down
}
else {
    Write-Host "  WARNING: docker-compose.yml bulunamadi: $composeFile"
}

Write-Host ""
Write-Host "=========================================="
Write-Host "All MetarPulse processes stopped."
Write-Host "NOTE: Database volume preserved."
Write-Host "  To delete data: docker compose down -v"
Write-Host "=========================================="
