@echo off
setlocal
title ImageGen Build Tool
set "JSON_BACKUP_DIR="

echo ========================================================
echo           ImageGen Build Script (Windows x64)
echo ========================================================
echo.

:: 1. .NET SDK
where dotnet >nul 2>&1
if %errorlevel% neq 0 goto :InstallDotNet

dotnet --list-sdks | findstr /R "8\.[0-9]" >nul 2>&1
if %errorlevel% neq 0 goto :InstallDotNet

:: 2. Git Update
where git >nul 2>&1
if %errorlevel% neq 0 (
    echo [WARNING] Git not found. Skipping update step.
    goto :BuildStep
)

echo.
echo --------------------------------------------------------
echo Update Source Code?
echo --------------------------------------------------------
set /p "update_choice=Update (Y/N)? "
if /i "%update_choice%"=="Y" (
    echo.
    echo [Step 0] Updating...
    
    :: Git URL (Fill this in if needed)
    set GIT_URL=https://github.com/Yoon999/ImageGen.git

    if "%GIT_URL%"=="" (
        echo [INFO] GIT_URL is empty. Running 'git pull'...
        git pull
    ) else (
        echo [INFO] Pulling from %GIT_URL%...
        git pull %GIT_URL% main
    )

    if %errorlevel% neq 0 (
        echo.
        echo [FAIL] Git pull failed.
        set /p "cont_choice=Continue build anyway (Y/N)? "
        if /i "%cont_choice%" neq "Y" exit /b 1
    )
)

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
echo [Step 1] Preserve JSON files and clean...
call :BackupJsonFiles
if %errorlevel% neq 0 (
    echo.
    echo [FAIL] Failed to back up JSON files. Build canceled to protect local data.
    pause
    exit /b 1
)

if exist "Release_Output" rmdir /s /q "Release_Output"
if exist "Release_Output" (
    echo.
    echo [FAIL] Could not clean Release_Output.
    call :RestoreJsonFiles
    pause
    exit /b 1
)

dotnet clean --configuration Release --verbosity quiet

echo.
echo [Step 2] Restore...
dotnet restore

echo.
echo [Step 3] Publish...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o Release_Output
set "PUBLISH_EXIT=%errorlevel%"

call :RestoreJsonFiles
set "RESTORE_EXIT=%errorlevel%"

if %RESTORE_EXIT% neq 0 (
    echo.
    echo [FAIL] Failed to restore JSON files.
    echo [IMPORTANT] Your JSON backup is still available at:
    echo %JSON_BACKUP_DIR%
    pause
    exit /b 1
)

if %PUBLISH_EXIT% neq 0 (
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
exit /b 0

:BackupJsonFiles
if not exist "Release_Output" exit /b 0

set "JSON_BACKUP_DIR=%TEMP%\ImageGen_Json_Backup_%RANDOM%_%RANDOM%"
mkdir "%JSON_BACKUP_DIR%" >nul 2>&1
if %errorlevel% neq 0 exit /b 1

robocopy "Release_Output" "%JSON_BACKUP_DIR%" *.json /S /COPY:DAT /DCOPY:T /R:1 /W:1 /NFL /NDL /NJH /NJS /NP >nul
set "ROBOCOPY_EXIT=%errorlevel%"
if %ROBOCOPY_EXIT% geq 8 (
    rmdir /s /q "%JSON_BACKUP_DIR%"
    set "JSON_BACKUP_DIR="
    exit /b 1
)

echo [INFO] Existing JSON files backed up.
exit /b 0

:RestoreJsonFiles
if not defined JSON_BACKUP_DIR exit /b 0
if not exist "%JSON_BACKUP_DIR%" exit /b 0

if not exist "Release_Output" mkdir "Release_Output" >nul 2>&1
robocopy "%JSON_BACKUP_DIR%" "Release_Output" *.json /S /COPY:DAT /DCOPY:T /R:1 /W:1 /NFL /NDL /NJH /NJS /NP >nul
set "ROBOCOPY_EXIT=%errorlevel%"
if %ROBOCOPY_EXIT% geq 8 exit /b 1

rmdir /s /q "%JSON_BACKUP_DIR%"
set "JSON_BACKUP_DIR="
echo [INFO] Existing JSON files restored.
exit /b 0
