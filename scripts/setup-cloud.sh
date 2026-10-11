#!/usr/bin/env bash
set -euo pipefail
# Use the current isolated checkout; do not create a worktree.
cd "$(dirname "$0")/.."
export DOTNET_CLI_HOME=/workspace/tools/dotnet-home
export NUGET_PACKAGES=/workspace/tools/nuget
export DOCKER_CONFIG=/workspace/tools/docker
export PATH=/workspace/tools/dotnet:$PATH
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$DOCKER_CONFIG"
if ! command -v dotnet >/dev/null; then
  mkdir -p /workspace/tools/dotnet
  sdk_archive=$(mktemp /tmp/mikroservis-sdk.XXXXXX.tar.gz)
  trap 'rm -f "$sdk_archive"' EXIT
  curl -fsSL https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-linux-x64.tar.gz -o "$sdk_archive"
  printf '%s  %s\n' '51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b' "$sdk_archive" | sha512sum --check --status
  tar -xzf "$sdk_archive" -C /workspace/tools/dotnet
fi
dotnet restore Mikroservis.slnx --locked-mode
dotnet build Mikroservis.slnx -c Release --no-restore
./scripts/init-env.sh
