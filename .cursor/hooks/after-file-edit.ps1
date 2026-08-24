$inputJson = [Console]::In.ReadToEnd()
$data = $inputJson | ConvertFrom-Json
$filePath = $data.path

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$checkScript = Join-Path $repoRoot "scripts\check-edited-file.ps1"

$output = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $checkScript -Path $filePath 2>&1
$code = $LASTEXITCODE
$text = ($output | Out-String).Trim()

if ($code -eq 0 -and $text.StartsWith("skip:")) {
    Write-Output (@{ "continue" = $true } | ConvertTo-Json -Compress)
    exit 0
}

if ($code -ne 0) {
    Write-Output (@{ "continue" = $false; agent_message = $text } | ConvertTo-Json -Compress)
    exit 1
}

Write-Output (@{ "continue" = $true } | ConvertTo-Json -Compress)
exit 0
