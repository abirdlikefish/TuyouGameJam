@echo off
setlocal

set "ROOT=%~dp0.."
set "LUBAN_DLL=%ROOT%\Luban\Luban.dll"
set "CONF_ROOT=%~dp0"

dotnet "%LUBAN_DLL%" ^
    -t client ^
    -c cs-simple-json ^
    -d json ^
    --conf "%CONF_ROOT%luban.conf" ^
    -x "outputCodeDir=%ROOT%\Assets\Generated\Luban" ^
    -x "outputDataDir=%ROOT%\Assets\StreamingAssets\Luban"

if errorlevel 1 (
    echo Luban generation failed.
    exit /b %errorlevel%
)

echo Luban generation succeeded.
endlocal
