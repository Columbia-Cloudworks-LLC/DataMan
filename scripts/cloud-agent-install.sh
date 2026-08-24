#!/usr/bin/env bash
# Idempotent Cloud Agent install for DataMan's cross-platform slice.
#
# Restores and builds the net8.0 projects that build on Linux: Contracts,
# Core, Embeddings, Desktop, the SampleCsv plugin, and the xUnit test project.
# Building Tests and Desktop covers the Linux-buildable graph.
#
# The WinUI 3 host (DataMan/DataMan.csproj, net8.0-windows) is intentionally
# skipped: it requires the Windows SDK and cannot build on Linux.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

dotnet build DataMan.Tests/DataMan.Tests.csproj -c Debug --nologo
dotnet build DataMan.Desktop/DataMan.Desktop.csproj -c Debug --nologo

echo "cloud-agent-install: cross-platform slice restored and built"
