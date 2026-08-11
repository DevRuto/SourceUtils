# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

SourceUtils reads Valve Source Engine file formats (BSP maps, VPK packages, MDL models, VTF/VMT materials)
and exports a map's geometry, materials, textures, lightmaps, visibility, and entities as JSON/PNG. It's a
headless CLI packaged as a Docker image — no HTTP server, no bundled viewer, no Windows dependency.

## Solution layout

- `SourceUtils/` — core library. Low-level binary readers for Source engine formats: `ValveBspFile` and
  `ValveBsp/*` (BSP lumps: geometry, displacements, static props, visibility, entities, game lump, pakfile
  lump), `ValvePackage` (VPK reader), `StudioModelFile` (MDL), `ValveMaterialFile` (VMT), `ValveTextureFile`
  (VTF), `ValveVertexFile`/`ValveTriangleFile`/`ValveVertexLightingFile` (VVD/VTX/VLM). `KeyValues.cs` parses
  Source's KeyValues text format using `Facepunch.Parse`. `ResourceLoader`/`IResourceProvider`/`FSLoader`
  in `ResourceLoader.cs` form the abstraction used everywhere else to resolve a game asset path across VPKs
  and loose files without caring which.
- `Facepunch.Parse/` — small parser-combinator library (`Parser.cs`, `GrammarBuilder.cs`,
  `GrammarParser.cs`, etc.) used by `SourceUtils/KeyValues.cs`. Vendored from the original Facepunch.Parse,
  converted to a plain SDK-style `net10.0` project.
- `SourceUtils.MapExport.Core/` — turns a parsed `ValveBspFile` into exportable JSON/PNG. `Bsp/*` mirrors
  one facet each: `Index.cs` (map index), `Geometry.cs` (leaf/displacement/studio-model/vertex-lighting
  pages), `BrushModel.cs`, `Lightmap.cs`, `Visibility.cs`, `Entities.cs`, `AmbientCubes.cs`,
  `MaterialPage.cs`. `Material.cs`/`Texture.cs`/`Texture.Convert.cs` handle VMT/VTF conversion to JSON/PNG
  (via `Magick.NET`/`OpenTK`). `MapLocator.cs` finds and loads BSPs by name/glob from a maps directory.
  `Url.cs` defines the same URL scheme a live viewer would fetch from
  (`/maps/<name>/...`, `/materials/...`); `ExportContext.cs` holds export-wide settings (untextured mode,
  debug materials, the active `ResourceLoader`).
- `SourceUtils.MapExport/` — the CLI (`Program.cs`, `SourceUtils.MapExport.exe`/`.dll`). Parses options with
  CommandLine.Parser, then crawls every URL a viewer would ever request for the given maps (starting from
  `/maps/<name>/index.json`, following references discovered via `UrlCrawl`) and writes each response
  straight to disk under `--outdir`, plus a `config.json` (`{"urlPrefix": "..."}`, from `--url-prefix`) at
  the output root. It does not produce a full viewer shell (no index.html/js/css) — just the raw data and
  that one config file.

Data flow in one sentence: `IResourceProvider` (VPK/loose files) → `SourceUtils` binary format readers →
`SourceUtils.MapExport.Core` converts parsed structures into JSON/binary payloads → `SourceUtils.MapExport`
crawls the URL graph for the requested maps and dumps every payload to disk.

## Build

This is a **.NET 10** solution using SDK-style `.csproj`/`PackageReference`, built with the `dotnet` CLI
(matches CI in `.github/workflows/build-test.yml`). All remaining projects target plain `net10.0` and are
fully cross-platform:

```powershell
dotnet restore SourceUtils.sln
dotnet build SourceUtils.sln --configuration Debug
```

## Docker

The `Dockerfile` builds and runs `SourceUtils.MapExport` in a `linux-x64` container — only
`SourceUtils.MapExport`, `SourceUtils.MapExport.Core`, `SourceUtils`, and `Facepunch.Parse` are needed:

```sh
docker build -t sourceutils-mapexport .
docker run --rm -v "<gamedir>:/gamedir:ro" -v "<outdir>:/out" sourceutils-mapexport \
    --gamedir /gamedir --mapsdir maps --maps <maps> --outdir /out --overwrite
```

`export-pages-kz-docker.bat` shows a full Windows example (mounts a local game dir, wipes and repopulates
an `output/` folder).

## Running locally without Docker

```powershell
dotnet run --project SourceUtils.MapExport -- --gamedir <path> --mapsdir maps --maps <maps> --outdir <path>
```

`--gamedir` (a real Source game's `csgo`-style folder) and `--maps` are required; there's no bundled game
content. `--debug-pakfile` and `--debug-materials` exist specifically for diagnosing a single map's embedded
pakfile lump or material properties — reach for them first when a specific map exports oddly. `--dry` tests
an export without writing files (including skipping `config.json`); `--untextured` skips texture
export/licensing concerns; `--url-prefix` sets the `urlPrefix` written into `config.json` for hosting under
a sub-path (e.g. GitHub Pages).

## Testing an export

`test-server.py` serves a static export from `./output` for local testing, resolving the same `urlPrefix`
from `output/config.json` that a real static host (e.g. GitHub Pages) would use, so the export behaves
identically locally and once deployed:

```sh
python test-server.py [port]
```

There is currently no automated test suite in this repo.
