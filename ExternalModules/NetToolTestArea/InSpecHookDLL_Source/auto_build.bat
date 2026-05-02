@echo off
chcp 65001 >nul
echo ========================================
echo InSpecHookDLL.dll Auto Build Script
echo ========================================
echo.

REM Check for Visual Studio (x86 32-bit toolchain)
set VS_FOUND=0
set VS_PATH=

REM Try Visual Studio 2022 (x86)
if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars32.bat" (
    set "VS_PATH=C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars32.bat"
    set VS_FOUND=1
    echo [Found] Visual Studio 2022 Community (x86)
    goto :vs_found
)
if exist "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars32.bat" (
    set "VS_PATH=C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars32.bat"
    set VS_FOUND=1
    echo [Found] Visual Studio 2022 Professional (x86)
    goto :vs_found
)
if exist "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars32.bat" (
    set "VS_PATH=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars32.bat"
    set VS_FOUND=1
    echo [Found] Visual Studio 2022 Enterprise (x86)
    goto :vs_found
)

REM Try Visual Studio 2019 (x86)
if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvars32.bat" (
    set "VS_PATH=C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvars32.bat"
    set VS_FOUND=1
    echo [Found] Visual Studio 2019 Community (x86)
    goto :vs_found
)
if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\VC\Auxiliary\Build\vcvars32.bat" (
    set "VS_PATH=C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\VC\Auxiliary\Build\vcvars32.bat"
    set VS_FOUND=1
    echo [Found] Visual Studio 2019 Professional (x86)
    goto :vs_found
)

:vs_found

if %VS_FOUND%==0 (
    echo.
    echo [失敗] 找不到 Visual Studio C++ 編譯器（x86）
    echo.
    echo 請：
    echo 1. 安裝 Visual Studio 2019 或 2022
    echo 2. 安裝「使用 C++ 的桌面開發」工作負載
    echo 3. 或使用「Visual Studio Developer Command Prompt」執行此腳本
    echo.
    exit /b 1
)

echo.
echo [INFO] 初始化建置環境...
echo Using: %VS_PATH%
if not exist "%VS_PATH%" (
    echo [失敗] 找不到 vcvars32.bat：%VS_PATH%
    exit /b 1
)

pushd "%~dp0"
for %%I in ("%VS_PATH%") do (
    pushd "%%~dpI"
    call "%%~nxI"
    popd
)
popd

if %ERRORLEVEL% NEQ 0 (
    echo [失敗] 初始化 Visual Studio 環境失敗
    exit /b 1
)

echo.
echo [INFO] 建置 InSpecHookDLL.dll...

cl /LD /Od /W3 /EHsc /MT /I. InSpecHookDLL.cpp /link /DEF:InSpecHookDLL.def kernel32.lib user32.lib psapi.lib dbghelp.lib /OUT:InSpecHookDLL.dll

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ========================================
    echo [失敗] 建置失敗
    echo ========================================
    echo.
    exit /b 1
)

echo.
echo ========================================
echo [成功] 建置完成
echo ========================================
echo.
echo DLL 位置: %~dp0InSpecHookDLL.dll
echo.

REM Copy to project Test folder
if not exist "..\Test" mkdir "..\Test"
copy /Y InSpecHookDLL.dll "..\Test\InSpecHookDLL.dll" >nul
if %ERRORLEVEL% EQU 0 (
    echo [成功] 已複製到 ..\Test\InSpecHookDLL.dll
) else (
    echo [失敗] 複製到 ..\Test 失敗
)

REM Copy to runtime folder (bin\Debug\Test)
if not exist "..\bin\Debug\Test" mkdir "..\bin\Debug\Test"
copy /Y InSpecHookDLL.dll "..\bin\Debug\Test\InSpecHookDLL.dll" >nul
if %ERRORLEVEL% EQU 0 (
    echo [成功] 已複製到 ..\bin\Debug\Test\InSpecHookDLL.dll
) else (
    echo [失敗] 複製到 ..\bin\Debug\Test 失敗
)

echo.
echo [INFO] 清理建置暫存檔...
del /Q *.obj *.exp *.lib 2>nul
echo [成功] 清理完成
echo.


