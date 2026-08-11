# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

SourceUtils reads and exports Valve Source Engine file formats (BSP maps, VPK packages, MDL models,
VTF/VMT materials) and includes a web server + TypeScript/WebGL viewer for browsing files and viewing maps
in a browser without the game installed. Demos of exported maps live at
https://metapyziks.github.io/SourceUtils/.

## Solution layout

- `SourceUtils/` — core library. Low-level binary readers for Source engine formats: `ValveBspFile`
  and `ValveBsp/*` (BSP lumps: geometry, displacements, static props, visibility, entities, game lump,
  pakfile lump), `ValvePackage` (VPK reader), `StudioModelFile` (MDL), `ValveMaterialFile` (VMT),
  `ValveTextureFile` (VTF), `ValveVertexFile`/`ValveTriangleFile`/`ValveVertexLightingFile` (VVD/VTX/VLM).
  `ResourceLoader`/`IResourceProvider`/`FSLoader` in `ResourceLoader.cs` form the abstraction used
  everywhere else to resolve a game asset path across VPKs and loose files without caring which.
- `SourceUtils.WebExport/` — the main executable (`SourceUtils.WebExport.exe`). A CommandLine.Parser-based
  CLI (see `Program.cs`) with four verbs:
  - `host` — runs an HTTP server (via `Ziks.WebServer`) that serves BSP/VPK content converted to JSON/binary
    on demand, plus the TypeScript viewer, so you can browse a map at `http://localhost:8080/maps/<name>/index.html`.
  - `export` — statically exports one or more maps (wildcards like `de_*` supported) to a directory for
    hosting on a plain file server / GitHub Pages (`Export.cs`).
  - `modelpatch` — rewrites strings/flags inside an MDL file in place.
  - `modelextract` — extracts material names/files referenced by an MDL.
  Server-side controllers under `Bsp/` (`Geometry.cs`, `Materials.cs`, `Lightmap.cs`, `Visibility.cs`,
  `BrushModel.cs`, `Entities.cs`, `AmbientCubes.cs`) each expose one facet of a `ValveBspFile` as JSON/binary
  over HTTP; `host` and `export` share these same controllers — `export` works by internally requesting every
  URL a client would ever fetch and writing the responses to disk (`Program.AddExportUrl`/`_sToExport` in
  `Export.cs`), so new server endpoints are automatically exportable without extra wiring.
  - `Resources/` is the browser-side viewer: hand-written TypeScript under `Resources/src/**/*.ts` compiles
    (via `tsc -p Resources/`, per `Resources/tsconfig.json`) into the single bundled/vendored output
    `Resources/js/sourceutils.js`. `Resources/js/facepunch.webgame.js` (+ its `.d.ts`) is a vendored WebGL
    engine dependency, not built from this repo — treat it as third-party. Viewer architecture:
    `MapViewer.ts`/`Map.ts` drive rendering; `*Loader.ts` files (`LeafGeometryLoader`, `DispGeometryLoader`,
    `MapMaterialLoader`, `AmbientLoader`, `VisLoader`, `PagedLoader`) stream data from the corresponding
    server/export endpoints on demand as the camera moves; `Entities/*.ts` map BSP entity classes to
    renderable behavior; `Shaders/*.ts` mirror Source engine shader names (`LightmappedGeneric`, `Water`,
    `UnlitGeneric`, etc.) as WebGL programs.
- `SourceUtils.FileExport/` — a separate small CLI (`Program.cs`) for extracting/exporting individual
  files directly to disk, independent of the web server/viewer pipeline.
- `SourceUtils.Test/` — MSTest unit tests (currently covers `KeyValues` parsing).

Data flow in one sentence: `IResourceProvider` (VPK/loose files) → `SourceUtils` binary format readers →
`SourceUtils.WebExport` HTTP controllers turn parsed structures into JSON/binary responses → either served
live (`host`) or crawled and dumped to disk (`export`) → the TypeScript viewer in `Resources/` fetches those
same URLs and renders them with WebGL.

## Build

This is a **.NET 10** solution using SDK-style `.csproj`/`PackageReference` (migrated from .NET Framework
4.8), built with the `dotnet` CLI (matches CI in `.github/workflows/build-test.yml`):

```powershell
dotnet restore SourceUtils.sln
dotnet build SourceUtils.sln --configuration Debug
```

`SourceUtils`, `SourceUtils.FileExport`, and `SourceUtils.Test` target plain `net10.0`; `SourceUtils.WebExport`
targets `net10.0-windows` (it uses `System.Net.HttpListener` via `Ziks.WebServer`, which is Windows-only in
practice here). `build.sh`/`test.sh` (Mono/`xbuild`) and the `#if LINUX` shim in
`SourceUtils.WebExport/ImageMagick.cs` predate this migration and are stale — `dotnet build`/`dotnet run` is
cross-platform for the non-`-windows` projects, but `SourceUtils.WebExport` itself no longer builds on
Mono/Linux via those scripts.

Several dependencies (`Ziks.WebServer`, `Facepunch.Parse`, `LZMA-SDK`, `MediaTypeMap`, `OpenTK`) only ship
old .NET Framework-targeted NuGet assets; they still resolve via `PackageReference` with a `NU1701`
compatibility warning and have been confirmed to work at runtime under .NET 10 on Windows.

The TypeScript compile step is independent of the C# build and must be run separately whenever
`Resources/src/**/*.ts` changes — `dotnet build` does not invoke `tsc`:

```sh
tsc -p "SourceUtils.WebExport/Resources/"
```

## Test

Tests are MSTest, run via `dotnet test`:

```powershell
dotnet test SourceUtils.Test/SourceUtils.Test.csproj
```

To run a single test, add `--filter "FullyQualifiedName~<TestMethodName>"`.

## Running locally

`test.sh` and `Examples/host-example.bat` show the `host` verb pointed at a local CS:GO install; a
`--gamedir` (and usually `--mapsdir`) pointing at a real Source game's `csgo`-style folder is required —
there's no bundled game content. `export-pages.bat` / `export-pages-kz.bat` / `Examples/export-example.bat`
show the `export` verb for static site generation, including `--untextured` (skip texture export/licensing
concerns) and `--url-prefix` (for hosting under a sub-path like GitHub Pages).

`DebugPakFile`/`--debug-pakfile` and `--debug-materials` options exist specifically for diagnosing a single
map's embedded pakfile lump or material properties — reach for them first when a specific map exports oddly.
