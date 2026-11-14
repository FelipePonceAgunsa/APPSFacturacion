@echo off
rem Levanta el frontend de ZARPE - FACTURACION en http://localhost:4200 sin PowerShell.
rem Uso: doble clic. La API debe estar corriendo en http://localhost:5228 (F5 en Visual Studio).
rem La primera vez instala las dependencias (npm install), lo que tarda unos minutos.
chcp 65001 >nul
cd /d "%~dp0"
if not exist node_modules (
    echo Instalando dependencias por primera vez...
    call npm.cmd install
    if errorlevel 1 (echo Fallo npm install. Copia el texto de arriba y envialo. & pause & exit /b 1)
)
echo.
echo Abriendo ZARPE - FACTURACION en http://localhost:4200 ... (cierra esta ventana para detenerlo)
start "" http://localhost:4200
call npm.cmd start
pause
