$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$brand = Join-Path $root "scripts\brand"
npm install --prefix $brand
if ($LASTEXITCODE -ne 0) {
    throw "npm install failed"
}
node (Join-Path $brand "generate.mjs")
if ($LASTEXITCODE -ne 0) {
    throw "generate.mjs failed"
}
