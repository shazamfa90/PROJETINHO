@echo off
REM Necess - abre a GUI. Da duplo clique neste arquivo.
start "" powershell -NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Necess.ps1"
