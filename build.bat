@echo off
setlocal
title ImageGen Build Tool

echo ========================================================
echo           ImageGen Build Script (Windows x64)
echo ========================================================
echo.

:: 1. .NET SDK 설치 확인
:: dotnet 명령어가 있는지, 그리고 버전 8.0이 목록에 있는지 확인
where dotnet >nul 2>&1
if %errorlevel% neq 0 goto :InstallDotNet

dotnet --list-sdks | findstr /R "8\.[0-9]" >nul 2>&1
if %errorlevel% neq 0 goto :InstallDotNet

goto :BuildStep

:InstallDotNet
echo [WARNING] .NET 8.0 SDK가 설치되어 있지 않거나 찾을 수 없습니다.
echo.
echo --------------------------------------------------------
echo 선택지: Winget을 통해 .NET 8.0 SDK를 지금 설치하시겠습니까?
echo --------------------------------------------------------
set /p "choice=설치 진행 (Y/N)? "
if /i "%choice%" neq "Y" (
    echo.
    echo [INFO] 설치를 취소했습니다. 수동으로 설치해주세요.
    echo 다운로드: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo.
echo [Step 0] Winget으로 .NET 8.0 SDK 설치 중...
winget install Microsoft.DotNet.SDK.8 --source winget --accept-package-agreements --accept-source-agreements

if %errorlevel% neq 0 (
    echo.
    echo [FAIL] Winget 설치 중 오류가 발생했습니다.
    pause
    exit /b 1
)

echo.
echo [SUCCESS] 설치가 완료되었습니다!
echo ********************************************************
echo  중요: 환경 변수 적용을 위해 이 창을 닫고 다시 실행해주세요.
echo ********************************************************
pause
exit /b 0

:BuildStep
echo [Step 1] 이전 빌드 파일 정리 중 (Clean)...
if exist "Release_Output" rmdir /s /q "Release_Output"
dotnet clean --configuration Release --verbosity quiet

echo.
echo [Step 2] 패키지 복원 중 (Restore)...
dotnet restore

echo.
echo [Step 3] 실행 파일 생성 중 (Publish)...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o Release_Output

if %errorlevel% neq 0 (
    echo.
    echo [FAIL] 빌드 실패. 위 에러 메시지를 확인하세요.
    pause
    exit /b 1
)

echo.
echo ========================================================
echo [SUCCESS] 빌드 완료!
echo 파일 위치: %~dp0Release_Output\ImageGen.exe
echo ========================================================
echo.
pause