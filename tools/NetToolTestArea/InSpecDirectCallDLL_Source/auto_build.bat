@echo off
chcp 65001 >nul
echo ========================================
echo InSpecDirectCallDLL.dll Auto Build Script
echo ========================================
echo.

REM 查找 Visual Studio
set "VS_PATH="
if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars32.bat" (
    set "VS_PATH=C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars32.bat"
    echo [Found] Visual Studio 2022 Community (x86
) else if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvars32.bat" (
    set "VS_PATH=C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvars32.bat"
    echo [Found] Visual Studio 2019 Community (x86
) else (
    echo [ERROR] Visual Studio not found!
    pause
    exit /b 1
)

echo.
echo [INFO] 初始化建置環境...
call "%VS_PATH%"

echo.
echo [INFO] 建置 InSpecDirectCallDLL.dll...
cl.exe /LD /MT InSpecDirectCallDLL.cpp /DEF:InSpecDirectCallDLL.def /OUT:InSpecDirectCallDLL.dll kernel32.lib user32.lib

if errorlevel 1 (
    echo.
    echo ========================================
    echo [失敗] 建置失敗
    echo ========================================
    pause
    exit /b 1
)

echo.
echo ========================================
echo [成功] 建置完成
echo ========================================
echo.
echo DLL 位置: %CD%\InSpecDirectCallDLL.dll

REM 複製到 Test 目錄
if exist "..\Test" (
    copy /Y InSpecDirectCallDLL.dll "..\Test\InSpecDirectCallDLL.dll" >nul 2>&1
    if errorlevel 1 (
        echo [失敗] 複製到 ..\Test 失敗
    ) else (
        echo [成功] 已複製到 ..\Test\InSpecDirectCallDLL.dll
    )
)

REM 複製到 bin\Debug\Test 目錄
if exist "..\bin\Debug\Test" (
    copy /Y InSpecDirectCallDLL.dll "..\bin\Debug\Test\InSpecDirectCallDLL.dll" >nul 2>&1
    if errorlevel 1 (
        echo [失敗] 複製到 ..\bin\Debug\Test 失敗
    ) else (
        echo [成功] 已複製到 ..\bin\Debug\Test\InSpecDirectCallDLL.dll
    )
)

echo.
echo [INFO] 清理建置暫存檔...
del /Q *.obj *.exp *.lib 2>nul
echo [成功] 清理完成

pause

