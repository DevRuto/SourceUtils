@echo off

:: Docker equivalent of export-pages-kz.bat, using the SourceUtils.MapExport image instead of
:: running it via `dotnet run`. Builds the Dockerfile if needed, then runs it against a mounted
:: game directory. Unlike SourceUtils.WebExport's old "export" verb, this writes only the raw
:: BSP-derived JSON/PNG data plus config.json - no viewer shell (index.html/js/css), since
:: MapExport doesn't produce a hosted site by itself.

SET GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\csgo legacy\csgo
SET MAPS=kz_reach_v2
SET OUTPUT_DIR=%~dp0output
SET OPTIONS=--overwrite --url-prefix "/GOKZReplayViewer/resources"

:: Wipe the output dir first so stale files from a previous run (or from export-pages-kz.bat,
:: which writes to the same "output" folder but produces a different set of files) don't linger
:: alongside the current run's output.
if exist "%OUTPUT_DIR%" rd /s /q "%OUTPUT_DIR%"
mkdir "%OUTPUT_DIR%"

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
