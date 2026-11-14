@echo off
rem Carga los datos de prueba (inventados) en la API local de ZARPE - FACTURACION.
rem Antes: la API debe estar corriendo (F5 en Visual Studio). Doble clic para ejecutar.
chcp 65001 >nul
cd /d "%~dp0"

where py >nul 2>nul
if %errorlevel%==0 (
    py -3 cargar_datos_demo.py %*
) else (
    python cargar_datos_demo.py %*
)

echo.
pause
