@echo off
setlocal enabledelayedexpansion

REM ============================
REM CONFIG
REM ============================
set CONFIG=Release
set ROOT=%~dp0
set BUILDS=%ROOT%Builds

REM --- Android SDK
set "ANDROID_SDK_ROOT=%LOCALAPPDATA%\Android\Sdk"

REM --- Android NDK
set "ANDROID_NDK_ROOT=%ANDROID_SDK_ROOT%\ndk\30.0.14904198"
set "LLVM_BIN=%ANDROID_NDK_ROOT%\toolchains\llvm\prebuilt\windows-x86_64\bin"

set "PATH=%LLVM_BIN%;%PATH%"

REM ============================
REM PREPARE OUTPUT DIRS
REM ============================
if not exist "%BUILDS%" mkdir "%BUILDS%"

:build_android
echo.
echo ============================
echo Publishing NUUTop (Android)
echo ============================

dotnet publish "%ROOT%NUUTop\NUUTop.csproj" ^
 -c %CONFIG% ^
 -r linux-bionic-arm64 ^
 -o "%BUILDS%\Android"

if errorlevel 1 goto :error

goto :done

REM ============================
REM END
REM ============================
:done
echo.
echo Build complete
echo Output directory:
echo   %BUILDS%
exit /b 0

:error
echo.
echo Build failed
exit /b 1
