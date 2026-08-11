@echo off

:: Docker equivalent of export-pages-kz.bat, using the SourceUtils.MapExport image instead of
:: SourceUtils.WebExport.exe. Builds SourceUtils.MapExport/Dockerfile if needed, then runs it
:: against a mounted game directory. Unlike SourceUtils.WebExport's "export" verb, this writes
:: only the raw BSP-derived JSON/PNG data - no viewer shell (index.html/js/css/config.json) and
:: no --url-prefix, since MapExport doesn't produce a hosted site by itself.

SET GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\csgo legacy\csgo
SET MAPS=kz_reach_v2
SET OUTPUT_DIR=%~dp0output
SET OPTIONS=--overwrite

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

docker build -t sourceutils-mapexport "%~dp0."
if errorlevel 1 exit /b %errorlevel%

docker run --rm ^
    -v "%GAME_DIR%:/gamedir:ro" ^
    -v "%OUTPUT_DIR%:/out" ^
    sourceutils-mapexport ^
    --gamedir /gamedir ^
    --mapsdir maps ^
    --maps %MAPS% ^
    --outdir /out ^
    %OPTIONS%
