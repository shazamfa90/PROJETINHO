@echo off
setlocal
REM Necess - abre a GUI. De duplo clique neste arquivo.
if not exist "%~dp0Necess.ps1" goto :faltando
start "" powershell -NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Necess.ps1"
exit /b 0

:faltando
echo.
echo  Nao achei o Necess.ps1 ao lado deste arquivo.
echo.
echo  Voce provavelmente abriu o Necess.bat de dentro do ZIP.
echo  Faca assim: botao direito no ZIP ^> Extrair tudo...
echo  e depois abra o Necess.bat de dentro da pasta extraida.
echo.
pause
exit /b 1
