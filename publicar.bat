@echo off
chcp 65001 >nul
cd /d "%~dp0"
where dotnet >nul 2>nul || (echo .NET SDK 8 nao encontrado. Instale em https://dotnet.microsoft.com/download & pause & exit /b 1)
echo Publicando para Windows...
dotnet publish src\GestaoComercial.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -p:DebugType=none -o dist\windows-x64 || (echo Falhou. & pause & exit /b 1)
echo.
echo Pronto! O programa esta em: dist\windows-x64\GestaoComercial.exe
pause
