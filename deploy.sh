#!/usr/bin/env bash
# Publica la versión actual en el servidor (Git Bash / Linux / macOS).
#   ./deploy.sh            construye aquí, sube solo las imágenes que cambiaron y reinicia
#   ./deploy.sh --files    solo sube archivos de configuración (compose, Caddyfile, data/) y reinicia
# Construye en esta computadora: el servidor (2 GB) no compila. No sube .env ni secrets/ (eso es aparte, una vez).
set -euo pipefail

HOST="${DEPLOY_HOST:-ubuntu@23.159.52.27}"
KEY="${DEPLOY_KEY:-$HOME/.ssh/districauchos_ed25519}"
DIR="districauchos"
IMAGES=(districauchos-api districauchos-worker districauchos-web)
SSH=(ssh -i "$KEY" -o IdentitiesOnly=yes "$HOST")

cd "$(dirname "$0")"
if [[ -n "$(git status --porcelain)" ]]; then
  echo "Hay cambios sin commit. Haz commit antes de publicar (así el servidor = una versión del repo)." >&2
  exit 1
fi
echo "Publicando $(git log --oneline -1)"

# 1) Archivos versionados que necesita el servidor (sin código fuente: va dentro de las imágenes)
git archive HEAD docker-compose.yml docker-compose.prod.yml Caddyfile data db \
  | "${SSH[@]}" "mkdir -p $DIR/media && tar -x -C $DIR"

if [[ "${1:-}" != "--files" ]]; then
  # 2) Construir aquí y subir solo las imágenes distintas a las del servidor
  docker compose --profile web build
  for img in "${IMAGES[@]}"; do
    local_id=$(docker image inspect -f '{{.Id}}' "$img:latest")
    remote_id=$("${SSH[@]}" "docker image inspect -f '{{.Id}}' $img:latest 2>/dev/null || true")
    if [[ "$local_id" == "$remote_id" ]]; then
      echo "  $img: sin cambios"
    else
      echo "  $img: subiendo…"
      docker save "$img:latest" | gzip -1 | "${SSH[@]}" "gunzip | docker load -q"
    fi
  done
fi

# 3) Reiniciar lo que cambió y limpiar imágenes viejas
"${SSH[@]}" "cd $DIR && docker compose up -d --no-build --remove-orphans && docker image prune -f >/dev/null && docker compose ps --format 'table {{.Service}}\t{{.Status}}'"
echo "Listo."
