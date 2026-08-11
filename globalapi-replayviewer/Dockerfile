# Build/run the globalapi-replayviewer Nuxt site in a container.
#
# The vendored GOKZ renderer and server/middleware/gokz-export-assets.ts read map data from
# `../output` relative to the server process's cwd (so re-running SourceUtils.MapExport is
# reflected live without copying - see that file). To preserve the same relative layout in the
# container, the app is placed at /app/viewer and the exported map data is expected to be
# mounted (or copied) into the sibling directory /app/output.
#
#   docker build -f globalapi-replayviewer/Dockerfile -t globalapi-replayviewer .
#   docker run --rm -p 47990:80 -v "<repo>/output:/app/output:ro" globalapi-replayviewer
#
# See docker-compose.yml at the repo root for the intended way to run this alongside
# SourceUtils.MapExport's output.

FROM node:22-alpine AS build
WORKDIR /app/viewer

COPY globalapi-replayviewer/package.json globalapi-replayviewer/package-lock.json ./
RUN npm ci

COPY globalapi-replayviewer/ ./
RUN npm run build

FROM node:22-alpine
WORKDIR /app/viewer
COPY --from=build /app/viewer/.output ./.output

ENV NITRO_PORT=80
ENV NITRO_HOST=0.0.0.0
EXPOSE 80

CMD ["node", ".output/server/index.mjs"]
