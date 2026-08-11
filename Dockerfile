# Build/run SourceUtils.MapExport (headless BSP -> JSON/PNG exporter, no HTTP server, no viewer
# shell) in a container:
#   docker build -t sourceutils-mapexport .
#
# Only SourceUtils.MapExport, SourceUtils.MapExport.Core, SourceUtils, and Facepunch.Parse are
# needed - all plain `net10.0` (unlike SourceUtils.WebExport, which targets net10.0-windows and
# can't run here).
#
# Also bundles /usr/local/bin/watch-maps.sh, which polls a maps directory for new .bsp files and
# exports each one as it appears - overridable as the container entrypoint for a long-running
# "watch" mode instead of a one-off export (see docker-compose.yml's mapexport-watch service).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SourceUtils.MapExport/SourceUtils.MapExport.csproj SourceUtils.MapExport/
COPY SourceUtils.MapExport.Core/SourceUtils.MapExport.Core.csproj SourceUtils.MapExport.Core/
COPY SourceUtils/SourceUtils.csproj SourceUtils/
COPY Facepunch.Parse/Facepunch.Parse.csproj Facepunch.Parse/
RUN dotnet restore SourceUtils.MapExport/SourceUtils.MapExport.csproj -r linux-x64

COPY SourceUtils.MapExport/ SourceUtils.MapExport/
COPY SourceUtils.MapExport.Core/ SourceUtils.MapExport.Core/
COPY SourceUtils/ SourceUtils/
COPY Facepunch.Parse/ Facepunch.Parse/

RUN dotnet publish SourceUtils.MapExport/SourceUtils.MapExport.csproj \
    -c Release -r linux-x64 --self-contained false -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
COPY watch-maps.sh /usr/local/bin/watch-maps.sh
RUN chmod +x /usr/local/bin/watch-maps.sh

ENTRYPOINT ["dotnet", "SourceUtils.MapExport.dll"]
