#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

dotnet test DataMan.Tests/DataMan.Tests.csproj -c Release --nologo
dotnet build DataMan.Desktop/DataMan.Desktop.csproj -c Release --nologo

out="$root/artifacts/linux-x64-verify"
rm -rf "$out"
dotnet publish DataMan.Desktop/DataMan.Desktop.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishTrimmed=false \
  -o "$out" \
  --nologo

test -x "$out/DataMan.Desktop"
