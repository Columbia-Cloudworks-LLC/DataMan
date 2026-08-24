$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$required = @(
    ".vscode\tasks.json",
    ".vscode\launch.json",
    ".vscode\settings.json",
    ".vscode\extensions.json",
    ".editorconfig",
    ".cursor\hooks.json",
    ".cursor\hooks\after-file-edit.ps1",
    ".cursor\rules\csharp-winui.mdc",
    "scripts\dev-build.ps1",
    "scripts\check-edited-file.ps1",
    "scripts\verify-ship.ps1"
)

foreach ($rel in $required) {
    $path = Join-Path $root $rel
    if (-not (Test-Path -LiteralPath $path)) {
        throw "missing $rel"
    }
}

foreach ($rel in @(".vscode\tasks.json", ".vscode\launch.json", ".vscode\settings.json", ".vscode\extensions.json", ".cursor\hooks.json")) {
    $null = Get-Content -LiteralPath (Join-Path $root $rel) -Raw | ConvertFrom-Json
}

$tasks = Get-Content -LiteralPath (Join-Path $root ".vscode\tasks.json") -Raw
if ($tasks -notmatch "dev-build\.ps1") { throw "tasks.json does not call dev-build.ps1" }
if ($tasks -notmatch "verify-ship\.ps1") { throw "tasks.json does not call verify-ship.ps1" }

$launch = Get-Content -LiteralPath (Join-Path $root ".vscode\launch.json") -Raw
if ($launch -notmatch "net8\.0-windows10\.0\.19041\.0/DataMan\.exe") { throw "launch.json program path is wrong" }

$hooks = Get-Content -LiteralPath (Join-Path $root ".cursor\hooks.json") -Raw
if ($hooks -notmatch "after-file-edit\.ps1") { throw "hooks.json does not call after-file-edit.ps1" }

$skip = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "scripts\check-edited-file.ps1") -Path (Join-Path $root "README.md")
if ($LASTEXITCODE -ne 0) { throw "check-edited-file should skip README" }
if ($skip -notmatch "^skip:") { throw "check-edited-file README output should start with skip:" }

$hookInput = @{ path = (Join-Path $root "README.md") } | ConvertTo-Json -Compress
$hookOut = $hookInput | & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root ".cursor\hooks\after-file-edit.ps1")
$hookJson = $hookOut | ConvertFrom-Json
if (-not $hookJson.continue) { throw "hook should continue on README skip" }

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "scripts\check-edited-file.ps1") -Path (Join-Path $root "DataMan.Core\Ingestion\ContentHasher.cs")
if ($LASTEXITCODE -ne 0) { throw "check-edited-file failed on ContentHasher.cs" }

$exe = Join-Path $root "DataMan\bin\x64\Debug\net8.0-windows10.0.19041.0\DataMan.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    throw "unpackaged exe missing; run the build task"
}

Write-Output "cursor setup verified"
