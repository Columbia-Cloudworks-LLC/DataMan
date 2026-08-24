$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$props = Get-Content -LiteralPath (Join-Path $root "Directory.Build.props") -Raw
if ($props -notmatch "<Version>([^<]+)</Version>") {
    throw "Directory.Build.props is missing Version"
}
$version = $Matches[1]

$publishDir = Join-Path $root "artifacts\win-x64"
$zipDir = Join-Path $root "artifacts"
if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir, $zipDir -Force | Out-Null

$project = Join-Path $root "DataMan\DataMan.csproj"
& dotnet publish $project `
    -c Release `
    -p:Platform=x64 `
    -r win-x64 `
    --self-contained true `
    -p:PublishTrimmed=false `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishReadyToRun=false `
    -o $publishDir `
    --nologo | ForEach-Object { Write-Host $_ }
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$zip = Join-Path $zipDir "DataMan-$version-win-x64.zip"
if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zip -Force
Write-Output $zip
