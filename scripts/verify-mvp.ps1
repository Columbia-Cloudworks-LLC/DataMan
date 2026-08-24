$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

dotnet test (Join-Path $root "DataMan.Tests\DataMan.Tests.csproj") --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build (Join-Path $root "DataMan\DataMan.csproj") -c Debug -p:Platform=x64 --nologo
exit $LASTEXITCODE
