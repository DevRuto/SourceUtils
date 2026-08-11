@echo off

"SourceUtils.WebExport\bin\Debug\net10.0-windows\SourceUtils.WebExport.exe" export ^
	--maps "kz_reach_v2" ^
	--outdir "output" ^
	--gamedir "C:\Program Files (x86)\Steam\steamapps\common\csgo legacy\csgo" ^
	--mapsdir "maps" ^
	--overwrite --verbose --url-prefix "/GOKZReplayViewer/resources"
