# SourceUtils

Headless exporter that reads Valve Source Engine map files (BSP, VPK, VTF/VMT, MDL) and dumps their
geometry, materials, textures, lightmaps, visibility, and entity data as JSON/PNG — for consumption by
your own viewer. It's a CLI with no HTTP server, no viewer shell, and no Windows dependency, packaged as a
Docker image so it can run anywhere `docker` runs.

## Solution layout

- `SourceUtils/` — low-level binary readers for Source engine formats: `ValveBspFile`, `ValvePackage` (VPK),
  `StudioModelFile` (MDL), `ValveMaterialFile` (VMT), `ValveTextureFile` (VTF), and friends. The
  `ResourceLoader`/`IResourceProvider`/`FSLoader` abstraction in `ResourceLoader.cs` resolves a game asset
  path across VPKs and loose files.
- `SourceUtils.Parsing/` — small parser-combinator library used by `KeyValues.cs` to parse Source's
  KeyValues text format.
- `SourceUtils.MapExport.Core/` — turns parsed BSP data into exportable JSON/PNG (geometry pages, materials,
  textures, lightmaps, visibility, ambient lighting) — the same shape of data a browser-based viewer would
  fetch on demand, just produced up front instead of served live.
- `SourceUtils.MapExport/` — the CLI entry point (`Program.cs`). Given a game directory and a list of maps,
  it crawls every URL a viewer would ever request for those maps and writes the responses straight to disk,
  plus a `config.json` (`{"urlPrefix": "..."}`) at the root of the output directory for viewers hosting the
  export under a sub-path.

## Build & run with Docker

```sh
docker build -t sourceutils-mapexport .

docker run --rm \
    -v "/path/to/game:/gamedir:ro" \
    -v "/path/to/output:/out" \
    sourceutils-mapexport \
    --gamedir /gamedir \
    --mapsdir maps \
    --maps "de_dust2,de_mirage" \
    --outdir /out \
    --overwrite
```

`export-pages-kz-docker.bat` shows a full Windows example. Run `docker run --rm sourceutils-mapexport --help`
to see every option (map name globs like `kz_*`, `--untextured`, `--debug-pakfile`, `--dry`, `--url-prefix`,
etc).

## Testing an export locally

`test-server.py` serves an `./output` directory produced by an export, resolving the same `urlPrefix` a
static host (e.g. GitHub Pages) would use so the export behaves the same locally as once deployed:

```sh
python test-server.py [port]
```

## Build without Docker

Requires the .NET 10 SDK:

```sh
dotnet restore SourceUtils.sln
dotnet build SourceUtils.sln --configuration Debug
dotnet run --project SourceUtils.MapExport -- --gamedir <path> --mapsdir maps --maps <maps> --outdir <path>
```
