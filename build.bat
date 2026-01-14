@echo off
setlocal
title ImageGen Build Tool

echo ========================================================
echo           ImageGen Build Script (Windows x64)
echo ========================================================
echo.

:: 1. .NET SDK
where dotnet >nul 2>&1
if %errorlevel% neq 0 goto :InstallDotNet

dotnet --list-sdks | findstr /R "8\.[0-9]" >nul 2>&1
if %errorlevel% neq 0 goto :InstallDotNet

goto :BuildStep

:InstallDotNet
echo [WARNING] .NET 8.0 SDK NOT INSTALLED
echo.
echo --------------------------------------------------------
echo Install .NET 8.0 SDK?
echo --------------------------------------------------------
set /p "choice=install (Y/N)? "
if /i "%choice%" neq "Y" (
    echo.
    echo [INFO] Installation canceled
    echo download: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo.
echo [Step 0] install .NET 8.0 SDK...
winget install Microsoft.DotNet.SDK.8 --source winget --accept-package-agreements --accept-source-agreements

if %errorlevel% neq 0 (
    echo.
    echo [FAIL] fail to install .NET 8.0 SDK
    pause
    exit /b 1
)

echo.
echo [SUCCESS] dotnet installation finished
echo ********************************************************
echo  please restart build.bat
echo ********************************************************
pause
exit /b 0

:BuildStep
echo [Step 1] Clean...
if exist "Release_Output" rmdir /s /q "Release_Output"
dotnet clean --configuration Release --verbosity quiet

echo.
echo [Step 2] Restore...
dotnet restore

echo.
echo [Step 3] Publish...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o Release_Output

if %errorlevel% neq 0 (
    echo.
    echo [FAIL] FAIL
    pause
    exit /b 1
)

echo.
echo ========================================================
echo [SUCCESS] Complete!
echo dir: %~dp0Release_Output\ImageGen.exe
echo ========================================================
echo.
pause