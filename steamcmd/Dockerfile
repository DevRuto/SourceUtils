# Downloads/updates a CS:GO dedicated server install (Steam app 740, downloadable anonymously -
# no Steam login needed) via SteamCMD. Used to provision a --gamedir for SourceUtils.MapExport
# on hosts that don't already have a Source game installed locally - see the `gamedir` service in
# docker-compose.yml at the repo root.
#
#   docker build -t sourceutils-gamedir steamcmd
#   docker run --rm -v "<host-dir>:/gamedir" sourceutils-gamedir \
#       +force_install_dir /gamedir +login anonymous +app_update 740 validate +quit

FROM debian:bookworm-slim

RUN dpkg --add-architecture i386 \
    && apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl lib32gcc-s1 lib32stdc++6 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /opt/steamcmd
RUN curl -sqL "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz" \
    | tar zxf -

ENTRYPOINT ["/opt/steamcmd/steamcmd.sh"]
