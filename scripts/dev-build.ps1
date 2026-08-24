$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

dotnet build (Join-Path $root "DataMan.Contracts\DataMan.Contracts.csproj") -c Debug --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build (Join-Path $root "DataMan.Core\DataMan.Core.csproj") -c Debug --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build (Join-Path $root "DataMan.Tests\DataMan.Tests.csproj") -c Debug --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build (Join-Path $root "DataMan\DataMan.csproj") -c Debug -p:Platform=x64 --nologo
exit $LASTEXITCODE
