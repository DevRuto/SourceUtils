# Build/run SourceUtils.MapExport (headless BSP -> JSON/PNG exporter, no HTTP server, no viewer
# shell) in a container:
#   docker build -t sourceutils-mapexport .
#
# Only SourceUtils.MapExport, SourceUtils.MapExport.Core, SourceUtils, and Facepunch.Parse are
# needed - all plain `net10.0` (unlike SourceUtils.WebExport, which targets net10.0-windows and
# can't run here).

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

ENTRYPOINT ["dotnet", "SourceUtils.MapExport.dll"]
