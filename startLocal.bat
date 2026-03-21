@echo off
echo ==========================================
echo  MetarPulse — Local Gelistirme Ortami
echo   1) Gelistirme modu  (dotnet run)
echo   2) Docker modu      (docker compose)
echo ==========================================
echo.
set /p MODE="Mod (1/2): "

rem Docker PATH ayarla
set "DOCKER_BIN=C:\Program Files\Docker\Docker\resources\bin"
if exist "%DOCKER_BIN%\docker.exe" set "PATH=%DOCKER_BIN%;%PATH%"

rem ── Docker Desktop acik mi? ────────────────────────────────────────────────
echo.
echo Docker Desktop kontrol ediliyor...
docker info >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Docker Desktop kapali, baslatiliyor...
    start "" "C:\Program Files\Docker\Docker\Docker Desktop.exe"
    echo Docker Desktop baslatiliyor, bekleniyor...
    :WAIT_DOCKER
    timeout /t 3 /nobreak >NUL
    docker info >nul 2>&1
    if %ERRORLEVEL% NEQ 0 goto WAIT_DOCKER
    echo Docker Desktop hazir.
) else (
    echo Docker Desktop zaten calisiyor.
)

if "%MODE%"=="2" goto DOCKER

rem ══════════════════════════════════════════════════════════════════════════
rem  GELISTIRME MODU
rem ══════════════════════════════════════════════════════════════════════════

rem ── [1/6] PostgreSQL ───────────────────────────────────────────────────────
echo.
echo [1/6] PostgreSQL baslatiliyor...
cd /d %~dp0
docker compose up -d postgres
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: docker compose up postgres basarisiz.
    pause
    exit /b 1
)

echo PostgreSQL hazir olana kadar bekleniyor...
:WAIT_PG_DEV
docker inspect --format="{{.State.Health.Status}}" metarpulse-db 2>nul | findstr "healthy" >nul
if %ERRORLEVEL% NEQ 0 (
    timeout /t 2 /nobreak >NUL
    goto WAIT_PG_DEV
)
echo PostgreSQL hazir.

rem ── [2/6] API ──────────────────────────────────────────────────────────────
echo.
echo [2/6] API baslatiliyor (dotnet run)...
start "MetarPulse API" cmd /k "cd /d %~dp0src\MetarPulse.Api && dotnet run --launch-profile http"

echo API baslamasi icin bekleniyor...
timeout /t 8 /nobreak >NUL

rem ── [3/6] Admin Panel ──────────────────────────────────────────────────────
echo.
echo [3/6] Admin Panel baslatiliyor...
start "MetarPulse Admin" cmd /k "cd /d %~dp0src\MetarPulse.Admin && dotnet run --launch-profile http"

rem ── [4/6] ngrok ────────────────────────────────────────────────────────────
echo.
echo [4/6] ngrok tunnel baslatiliyor (port 5000)...
where ngrok >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo WARNING: ngrok bulunamadi. Atlaniyor.
    echo          Kurmak icin: winget install ngrok.ngrok
) else (
    start "MetarPulse ngrok" cmd /k "ngrok http --domain=gulflike-yosef-unsequenced.ngrok-free.dev 5000"
    echo ngrok baslatildi: https://gulflike-yosef-unsequenced.ngrok-free.dev
)

rem ── [5/6] Web ──────────────────────────────────────────────────────────────
echo.
echo [5/6] Web projesi baslatiliyor...
start "MetarPulse Web" cmd /k "cd /d %~dp0src\MetarPulse.Web && dotnet run --launch-profile http"

rem ── [6/6] MAUI Windows ────────────────────────────────────────────────────
echo.
echo [6/6] MAUI Windows uygulamasi baslatiliyor...
start "MetarPulse MAUI" cmd /k "cd /d %~dp0src\MetarPulse.Maui && dotnet build -t:Run -f net10.0-windows10.0.19041.0"

echo.
echo ==========================================
echo  PostgreSQL : localhost:5432
echo  API        : http://localhost:5000
echo  Admin      : http://localhost:5225
echo  Web        : http://localhost:5269
echo  ngrok      : https://gulflike-yosef-unsequenced.ngrok-free.dev
echo ==========================================
pause
goto END

rem ══════════════════════════════════════════════════════════════════════════
rem  DOCKER MODU
rem ══════════════════════════════════════════════════════════════════════════

:DOCKER
echo.
echo [Docker] Tum servisler build edilip baslatiliyor...
cd /d %~dp0
docker compose up -d --build
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: docker compose up basarisiz.
    pause
    exit /b 1
)

echo.
echo Servislerin hazir olmasi bekleniyor...
timeout /t 10 /nobreak >NUL

rem ── ngrok ──────────────────────────────────────────────────────────────────
echo.
echo [ngrok] Tunnel baslatiliyor (port 5000)...
where ngrok >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo WARNING: ngrok bulunamadi. Atlaniyor.
    echo          Kurmak icin: winget install ngrok.ngrok
) else (
    start "MetarPulse ngrok" cmd /k "ngrok http --domain=gulflike-yosef-unsequenced.ngrok-free.dev 5000"
    echo ngrok baslatildi: https://gulflike-yosef-unsequenced.ngrok-free.dev
)

rem ── MAUI Windows ──────────────────────────────────────────────────────────
echo.
echo [MAUI] Windows uygulamasi baslatiliyor...
start "MetarPulse MAUI" cmd /k "cd /d %~dp0src\MetarPulse.Maui && dotnet build -t:Run -f net10.0-windows10.0.19041.0"

echo.
echo Tarayici aciliyor...
timeout /t 3 /nobreak >NUL
start "" "http://localhost:5225"
start "" "http://localhost:8080"

echo.
echo ==========================================
echo  PostgreSQL : localhost:5432
echo  API        : http://localhost:5000
echo  Admin      : http://localhost:5225
echo  Web        : http://localhost:8080
echo  ngrok      : https://gulflike-yosef-unsequenced.ngrok-free.dev
echo.
echo Durdurmak icin stop.bat calistirin.
echo Log gormek icin: docker compose logs -f
echo ==========================================
pause

:END
