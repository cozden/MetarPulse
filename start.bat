@echo off
echo ==========================================
echo MetarPulse — Mod secin:
echo   1) Gelistirme modu  (dotnet run)
echo   2) Docker modu      (docker compose)
echo ==========================================
echo.
set /p MODE="Mod (1/2): "

rem Docker PATH ayarla (Docker Desktop PATH'e eklemediyse)
set "DOCKER_BIN=C:\Program Files\Docker\Docker\resources\bin"
if exist "%DOCKER_BIN%\docker.exe" set "PATH=%DOCKER_BIN%;%PATH%"

if "%MODE%"=="2" goto DOCKER

:DEV
echo.
echo [1/4] Starting PostgreSQL (Docker)...
docker compose up -d postgres
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Docker Compose failed. Is Docker Desktop running?
    pause
    exit /b 1
)

echo Waiting for PostgreSQL to be ready...
:WAIT_PG_DEV
docker inspect --format="{{.State.Health.Status}}" metarpulse-db 2>nul | findstr "healthy" >nul
if %ERRORLEVEL% NEQ 0 (
    timeout /t 2 /nobreak >NUL
    goto WAIT_PG_DEV
)
echo PostgreSQL is ready.

echo.
echo [2/4] Starting API Project...
start "MetarPulse API" cmd /k "cd /d %~dp0src\MetarPulse.Api && dotnet run --launch-profile http"

echo Waiting for API to initialize...
timeout /t 5 /nobreak >NUL

echo.
echo [3/4] Starting Web Project...
start "MetarPulse Web" cmd /k "cd /d %~dp0src\MetarPulse.Web && dotnet run --launch-profile http"

echo.
echo [4/4] Starting MAUI Project (Windows)...
start "MetarPulse MAUI" cmd /k "cd /d %~dp0src\MetarPulse.Maui && dotnet build -t:Run -f net10.0-windows10.0.19041.0"

echo.
echo ==========================================
echo  PostgreSQL : localhost:5432
echo  API        : http://localhost:5000
echo  Web        : http://localhost:5269
echo ==========================================
pause
goto END

:DOCKER
echo.
echo [Docker] Building and starting all services...
docker compose up -d --build
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: docker compose up failed. Is Docker Desktop running?
    pause
    exit /b 1
)

echo.
echo Waiting for services to be healthy...
timeout /t 10 /nobreak >NUL

echo.
echo ==========================================
echo  PostgreSQL : localhost:5432
echo  API        : http://localhost:5000
echo  Web        : http://localhost:8080
echo.
echo Durdurmak icin stop.bat calistirin.
echo Log gormek icin: docker compose logs -f
echo ==========================================
pause

:END
