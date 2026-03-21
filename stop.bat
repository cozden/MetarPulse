@echo off
echo ==========================================
echo Stopping MetarPulse Solution
echo ==========================================

rem Docker PATH ayarla
set "DOCKER_BIN=C:\Program Files\Docker\Docker\resources\bin"
if exist "%DOCKER_BIN%\docker.exe" set "PATH=%DOCKER_BIN%;%PATH%"

echo.
echo [1/4] Stopping dotnet run windows (if any)...
taskkill /FI "WINDOWTITLE eq MetarPulse API"  /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq MetarPulse Web"  /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq MetarPulse MAUI" /T /F >nul 2>&1
taskkill /IM MetarPulse.Api.exe /T /F >nul 2>&1

echo [2/4] Stopping ngrok...
rem ngrok.exe'yi bul, parent cmd.exe penceresini de kapat
for /f "tokens=2" %%P in ('tasklist /FI "IMAGENAME eq ngrok.exe" /NH 2^>nul ^| findstr "ngrok.exe"') do (
    rem ngrok'un parent PID'ini bul (wmic ile) ve o cmd penceresini kapat
    for /f "tokens=2 delims==" %%Q in ('wmic process where "ProcessId=%%P" get ParentProcessId /value 2^>nul ^| findstr "ParentProcessId"') do (
        taskkill /PID %%Q /T /F >nul 2>&1
    )
    taskkill /PID %%P /T /F >nul 2>&1
)
taskkill /FI "WINDOWTITLE eq MetarPulse ngrok" /T /F >nul 2>&1
taskkill /IM ngrok.exe /T /F >nul 2>&1

echo [3/4] Freeing ports 5000 / 5269 (if still in use)...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5000 " ^| findstr "LISTENING"') do (
    taskkill /PID %%a /T /F >nul 2>&1
)
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5225 " ^| findstr "LISTENING"') do (
    taskkill /PID %%a /T /F >nul 2>&1
)
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5269 " ^| findstr "LISTENING"') do (
    taskkill /PID %%a /T /F >nul 2>&1
)


echo [4/4] Stopping Docker services...
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
