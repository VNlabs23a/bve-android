@echo off
REM Build script for OpenBVE-Android (Windows)
REM Usage: build.bat [debug|release|aab|all]

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%"

set BUILD_TYPE=%1
if "%BUILD_TYPE%"=="" set BUILD_TYPE=debug

echo ==========================================
echo OpenBVE-Android Build Script
echo Build type: %BUILD_TYPE%
echo ==========================================

REM Check for Godot
where godot >nul 2>nul
if errorlevel 1 (
    echo ERROR: Godot not found in PATH
    echo Please install Godot 4.3+ and add to PATH
    exit /b 1
)

for /f "tokens=*" %%i in ('godot --version') do set GODOT_VERSION=%%i
echo Godot version: %GODOT_VERSION%

REM Export based on build type
if /i "%BUILD_TYPE%"=="debug" (
    echo Building Debug APK...
    godot --headless --export-debug "Android Debug" build\OpenBVE-Android-Debug.apk
) else if /i "%BUILD_TYPE%"=="release" (
    echo Building Release APK...
    godot --headless --export-release "Android Release" build\OpenBVE-Android-Release.apk
) else if /i "%BUILD_TYPE%"=="aab" (
    echo Building AAB for Play Store...
    godot --headless --export-release "Android AAB (Play Store)" build\OpenBVE-Android.aab
) else if /i "%BUILD_TYPE%"=="all" (
    echo Building all variants...
    godot --headless --export-debug "Android Debug" build\OpenBVE-Android-Debug.apk
    godot --headless --export-release "Android Release" build\OpenBVE-Android-Release.apk
    godot --headless --export-release "Android AAB (Play Store)" build\OpenBVE-Android.aab
) else (
    echo Usage: %0 [debug|release|aab|all]
    exit /b 1
)

REM Verify output
if /i "%BUILD_TYPE%"=="debug" (
    if exist build\OpenBVE-Android-Debug.apk (
        echo ✓ Debug APK created: build\OpenBVE-Android-Debug.apk
        dir build\OpenBVE-Android-Debug.apk
    )
)

if /i "%BUILD_TYPE%"=="release" (
    if exist build\OpenBVE-Android-Release.apk (
        echo ✓ Release APK created: build\OpenBVE-Android-Release.apk
        dir build\OpenBVE-Android-Release.apk
    )
)

if /i "%BUILD_TYPE%"=="aab" (
    if exist build\OpenBVE-Android.aab (
        echo ✓ AAB created: build\OpenBVE-Android.aab
        dir build\OpenBVE-Android.aab
    )
)

if /i "%BUILD_TYPE%"=="all" (
    if exist build\OpenBVE-Android-Debug.apk (
        echo ✓ Debug APK created: build\OpenBVE-Android-Debug.apk
    )
    if exist build\OpenBVE-Android-Release.apk (
        echo ✓ Release APK created: build\OpenBVE-Android-Release.apk
    )
    if exist build\OpenBVE-Android.aab (
        echo ✓ AAB created: build\OpenBVE-Android.aab
    )
)

echo ==========================================
echo Build complete!
echo ==========================================