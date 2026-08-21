#!/usr/bin/env bash
set -euo pipefail
set -x

# Bootstraps Conduit for a headless build/test loop (no Windows/Caesar II required).
# Installs the .NET SDK if missing, then restores/builds/tests the solution if one exists yet.

DOTNET_VERSION="8.0"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

if ! command -v dotnet >/dev/null 2>&1; then
  # Prefer the distro package (works behind egress proxies that block dot.net).
  if command -v apt-get >/dev/null 2>&1; then
    apt-get install -y -qq "dotnet-sdk-${DOTNET_VERSION}" || true
  fi
fi

if ! command -v dotnet >/dev/null 2>&1; then
  INSTALL_DIR="${HOME}/.dotnet"
  mkdir -p "${INSTALL_DIR}"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh &
  wait
  bash /tmp/dotnet-install.sh --channel "${DOTNET_VERSION}" --install-dir "${INSTALL_DIR}" || true
  export PATH="${INSTALL_DIR}:${PATH}"
fi

command -v dotnet >/dev/null 2>&1 && dotnet --version || echo "dotnet not available; skipping build/test" || true

SOLUTION=$(find . -maxdepth 2 -name '*.sln' -print -quit || true)

if [ -n "${SOLUTION}" ]; then
  dotnet restore "${SOLUTION}" &
  wait
  dotnet build "${SOLUTION}" --no-restore
  dotnet test "${SOLUTION}" --no-build || true
else
  echo "No .sln found yet — nothing to restore/build (expected before the C# project exists)."
fi
