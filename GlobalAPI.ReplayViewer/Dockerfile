# Build/run the GlobalAPI.ReplayViewer Nuxt site in a container.
#
# The vendored GOKZ renderer and server/middleware/gokz-export-assets.ts read map data from
# `../output` relative to the server process's cwd (so re-running SourceUtils.MapExport is
# reflected live without copying - see that file). To preserve the same relative layout in the
# container, the app is placed at /app/viewer and the exported map data is expected to be
# mounted (or copied) into the sibling directory /app/output.
#
#   docker build -f GlobalAPI.ReplayViewer/Dockerfile -t globalapi-replayviewer .
#   docker run --rm -p 47990:47990 -v "<repo>/output:/app/output:ro" globalapi-replayviewer
#
# See docker-compose.yml at the repo root for the intended way to run this alongside
# SourceUtils.MapExport's output.

FROM node:22-alpine AS build
WORKDIR /app/viewer

COPY GlobalAPI.ReplayViewer/package.json GlobalAPI.ReplayViewer/package-lock.json ./
RUN npm ci

COPY GlobalAPI.ReplayViewer/ ./
RUN npm run build

FROM node:22-alpine
WORKDIR /app/viewer
COPY --from=build /app/viewer/.output ./.output

ENV NITRO_PORT=47990
ENV NITRO_HOST=0.0.0.0
EXPOSE 47990

CMD ["node", ".output/server/index.mjs"]
