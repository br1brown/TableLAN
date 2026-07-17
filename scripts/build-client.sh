#!/usr/bin/env bash
# Compila il client Angular dei giocatori e lo pubblica nella wwwroot del
# server, accanto alla dashboard /admin (che non viene toccata).
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
client_dir="$repo_root/clients/player"
wwwroot="$repo_root/src/TableLAN.Server/wwwroot"

cd "$client_dir"
if [ ! -d node_modules ]; then
  npm ci
fi
npm run build

# Rimuove il build precedente preservando la dashboard del Master.
find "$wwwroot" -mindepth 1 -maxdepth 1 ! -name admin -exec rm -rf {} +
cp -r "$client_dir/dist/player/browser/." "$wwwroot/"

echo "Client pubblicato in $wwwroot"
