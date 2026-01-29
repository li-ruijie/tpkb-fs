@echo off
setlocal

set CONFIG=%~1
if "%CONFIG%"=="" set CONFIG=Release

set MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe
set FSHARP_TOOLS=C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\Common7\IDE\CommonExtensions\Microsoft\FSharp\Tools

:: Check if Microsoft.IO.Redist.dll is missing from F# tools
set FSC_TOOL_PATH=
if not exist "%FSHARP_TOOLS%\Microsoft.IO.Redist.dll" (
    echo Microsoft.IO.Redist.dll missing from F# tools, using patched copy...
    set "TEMP_FSC=%TEMP%\tpkb-fsharp-tools"
    if not exist "%TEMP_FSC%" mkdir "%TEMP_FSC%"
    xcopy /Y /Q "%FSHARP_TOOLS%\*" "%TEMP_FSC%\" >nul 2>&1
    copy /Y "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Microsoft.IO.Redist.dll" "%TEMP_FSC%\" >nul
    set "FSC_TOOL_PATH=/p:FscToolPath=%TEMP_FSC%"
)

"%MSBUILD%" "%~dp0tpkb.fsproj" /t:Restore /v:minimal
"%MSBUILD%" "%~dp0tpkb.fsproj" /t:Rebuild /p:Configuration=%CONFIG% /v:minimal %FSC_TOOL_PATH%
