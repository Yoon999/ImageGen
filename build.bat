@echo off
setlocal
title ImageGen Build Tool

echo ========================================================
echo           ImageGen Build Script (Windows x64)
echo ========================================================
echo.

:: 1. .NET SDK 설치 여부 확인
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] .NET SDK가 설치되어 있지 않습니다.
    echo .NET 8.0 SDK를 설치해주세요: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [Step 1] 이전 빌드 파일 정리 중 (Clean)...
:: 기존 결과물 폴더가 있다면 삭제하여 깨끗한 상태로 시작
if exist "Release_Output" rmdir /s /q "Release_Output"
dotnet clean --configuration Release --verbosity quiet

echo.
echo [Step 2] 패키지 복원 중 (Restore)...
dotnet restore

echo.
echo [Step 3] 실행 파일 생성 중 (Publish)...
echo 옵션: 단일 파일(SingleFile), 자체 포함(Self-Contained)
:: -c Release: 배포용 최적화 모드
:: -r win-x64: 윈도우 64비트 타겟
:: --self-contained true: .NET 런타임을 포함 (사용자 PC에 .NET 설치 불필요)
:: -p:PublishSingleFile=true: 하나의 exe 파일로 합침
:: -p:IncludeNativeLibrariesForSelfExtract=true: 압축 해제 성능 최적화
:: -o Release_Output: 결과물을 저장할 폴더 이름
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o Release_Output

if %errorlevel% neq 0 (
    echo.
    echo [FAIL] 빌드 중 오류가 발생했습니다. 위의 에러 메시지를 확인하세요.
    pause
    exit /b 1
)

echo.
echo ========================================================
echo [SUCCESS] 빌드가 완료되었습니다!
echo 생성된 파일 위치: %~dp0Release_Output\ImageGen.exe
echo ========================================================
echo.
pause