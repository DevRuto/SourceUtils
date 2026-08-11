@echo off

:: Non-docker equivalent of export-pages-kz-docker.bat, running SourceUtils.MapExport directly via
:: `dotnet run` instead of through the container. Writes only the raw BSP-derived JSON/PNG data
:: plus config.json - no viewer shell (index.html/js/css), since MapExport doesn't produce a
:: hosted site by itself.

dotnet run --project SourceUtils.MapExport --configuration Release -- ^
	--maps "kz_reach_v2" ^
	--outdir "output" ^
	--gamedir "C:\Program Files (x86)\Steam\steamapps\common\csgo legacy\csgo" ^
	--mapsdir "maps" ^
	--overwrite --verbose --url-prefix "/GOKZReplayViewer/resources"
