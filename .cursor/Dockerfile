# Cloud Agent base image for DataMan's cross-platform slice.
# The WinUI 3 host (DataMan/DataMan.csproj) targets net8.0-windows and only
# builds on Windows. Contracts, Core, the SampleCsv plugin, and the xUnit
# tests all target plain net8.0 and build, test, and run here on Linux.
#
# The official .NET 8 SDK image is the 8.0.4xx feature band. That band is
# required: the code uses C# 12 collection expressions in String.Split calls
# that the older 8.0.1xx Roslyn (shipped in the Ubuntu apt package) rejects
# with a spurious CS0121 overload-ambiguity error.
FROM mcr.microsoft.com/dotnet/sdk:8.0

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# git and curl already ship in the SDK image (buildpack-deps base); install is
# a no-op refresh that keeps the environment self-documenting and resilient.
RUN apt-get update \
    && apt-get install -y --no-install-recommends git curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*
