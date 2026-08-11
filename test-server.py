#!/usr/bin/env python3
"""Local static server for testing a SourceUtils static export in ./output.

The exported viewer resolves absolute URLs (e.g. "/maps/foo/index.json") by
prefixing them with the "urlPrefix" from output/config.json (see Config.ts),
which is normally something like "/GOKZReplayViewer/resources" for GitHub
Pages hosting. This server reads that same config.json and strips the prefix
back off incoming requests, so the export behaves the same locally as it will
once actually deployed - no need to edit config.json just to test it.

Usage:
    python serve-output.py [port]      (default port 8080)
"""
import http.server
import json
import os
import sys

OUTPUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "output")
PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8080

config_path = os.path.join(OUTPUT_DIR, "config.json")
prefix = ""
if os.path.exists(config_path):
    with open(config_path) as f:
        prefix = (json.load(f).get("urlPrefix") or "").rstrip("/")


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=OUTPUT_DIR, **kwargs)

    def translate_path(self, path):
        path_only = path.split("?", 1)[0].split("#", 1)[0]
        if prefix and (path_only == prefix or path_only.startswith(prefix + "/")):
            path = path_only[len(prefix):] or "/"
        return super().translate_path(path)


if __name__ == "__main__":
    if not os.path.isdir(OUTPUT_DIR):
        sys.exit(f"No output directory found at {OUTPUT_DIR}")

    maps_dir = os.path.join(OUTPUT_DIR, "maps")
    map_names = sorted(
        d for d in os.listdir(maps_dir) if os.path.isdir(os.path.join(maps_dir, d))
    ) if os.path.isdir(maps_dir) else []

    with http.server.ThreadingHTTPServer(("", PORT), Handler) as httpd:
        print(f"Serving {OUTPUT_DIR}")
        print(f"urlPrefix from config.json: {prefix!r}")
        print("Open one of:")
        for name in map_names:
            print(f"  http://localhost:{PORT}{prefix}/maps/{name}/index.html")
        if not map_names:
            print(f"  http://localhost:{PORT}{prefix}/")
        httpd.serve_forever()
