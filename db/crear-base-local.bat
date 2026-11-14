@echo off
rem Crea la base local zarpe_facturacion en el PostgreSQL instalado (localhost:5432) sin PowerShell ni Docker.
rem Uso: doble clic en este archivo. Pide la clave del usuario postgres; no la guarda en ningun archivo.
rem Si la base zarpe_facturacion ya existe, la BORRA y la vuelve a crear. No toca otras bases.
chcp 65001 >nul
cd /d "%~dp0.."
set "PATH=%USERPROFILE%\.dotnet\tools;%PATH%"
echo.
echo Base local de ZARPE - FACTURACION (localhost:5432, usuario postgres)
echo.
set /p "ZARPE_DB_PASSWORD=Clave del usuario postgres: "
echo.
python db\local_sin_docker.py --puerto 5432
echo.
if errorlevel 1 (echo Hubo un error. Copia el texto de arriba y envialo.) else (echo Base lista.)
set "ZARPE_DB_PASSWORD="
pause
