#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

version="$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' Directory.Build.props | head -1)"
if [[ -z "$version" ]]; then
  echo "Directory.Build.props is missing Version" >&2
  exit 1
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

out="$root/artifacts/linux-x64"
mkdir -p "$root/artifacts"
rm -rf "$out"

dotnet publish DataMan.Desktop/DataMan.Desktop.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishTrimmed=false \
  -o "$out" \
  --nologo

tarball="$root/artifacts/DataMan-$version-linux-x64.tar.gz"
rm -f "$tarball"
tar -czf "$tarball" -C "$out" .
echo "$tarball"
