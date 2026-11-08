#!/usr/bin/env bash
# Rebuilds the engine for the browser and copies it into the website.
# Needs the .NET 10 SDK and the wasm-tools workload:  dotnet workload install wasm-tools
# Run this after changing anything in ../Engine, then commit site/public/engine/.
set -euo pipefail
cd "$(dirname "$0")"

dotnet publish -c Release
out=bin/Release/net10.0/publish/wwwroot/_framework
dest=../site/public/engine/_framework

rm -rf "$dest"
mkdir -p "$dest"
cp "$out"/* "$dest"/
# Vercel compresses on its own, so the prebuilt .br/.gz copies aren't needed.
rm -f "$dest"/*.br "$dest"/*.gz
echo "Copied engine build to $dest"
