#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")"
IMAGE_NAME="pi"

docker build --pull \
  -t "$IMAGE_NAME" \
  -f "$SCRIPT_DIR/Dockerfile" \
  "$SCRIPT_DIR" 2>/dev/null

docker run --rm -it \
  -e LLAMA_BASE_URL=http://host.docker.internal:9931 \
  -v "$(pwd):/workspace" \
  -v "$SCRIPT_DIR/agent/models.json:/root/.pi/agent/models.json:ro" \
  -v "$SCRIPT_DIR/agent/APPEND_SYSTEM.md:/root/.pi/agent/APPEND_SYSTEM.md:ro" \
  -v "$SCRIPT_DIR/agent/settings.json:/root/.pi/agent/settings.json:ro" \
  -v "$HOME/.gitconfig:/root/.gitconfig:ro" \
  --name "Pi" \
  "$IMAGE_NAME"
