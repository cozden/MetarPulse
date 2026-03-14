@echo off
echo ==========================================
echo Stopping MetarPulse Solution
echo ==========================================

rem Docker PATH ayarla
set "DOCKER_BIN=C:\Program Files\Docker\Docker\resources\bin"
if exist "%DOCKER_BIN%\docker.exe" set "PATH=%DOCKER_BIN%;%PATH%"

echo.
echo [1/3] Stopping dotnet run windows (if any)...
taskkill /FI "WINDOWTITLE eq MetarPulse API"  /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq MetarPulse Web"  /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq MetarPulse MAUI" /T /F >nul 2>&1
taskkill /IM MetarPulse.Api.exe /T /F >nul 2>&1

echo [2/3] Freeing ports 5000 / 5269 (if still in use)...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5000 " ^| findstr "LISTENING"') do (
    taskkill /PID %%a /T /F >nul 2>&1
)
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5269 " ^| findstr "LISTENING"') do (
    taskkill /PID %%a /T /F >nul 2>&1
)

echo [3/3] Stopping Docker services...
docker compose down
if %ERRORLEVEL% EQU 0 (
    echo Docker services stopped.
) else (
    echo WARNING: Could not stop Docker containers ^(Is Docker Desktop running?^)
)

echo.
echo ==========================================
echo All MetarPulse processes stopped.
echo NOTE: Database volume preserved.
echo   To delete data: docker compose down -v
echo ==========================================
pause
