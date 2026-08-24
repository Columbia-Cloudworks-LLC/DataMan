param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$full = [System.IO.Path]::GetFullPath($Path)

if (-not $full.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Output "skip: outside workspace"
    exit 0
}

$relative = $full.Substring($root.Length).TrimStart("\", "/")
if ($relative -match '(^|\\|/)(bin|obj|\.vs|\.git)(\\|/)') {
    Write-Output "skip: generated path"
    exit 0
}

$ext = [System.IO.Path]::GetExtension($full).ToLowerInvariant()
if ($ext -notin ".cs", ".xaml", ".csproj") {
    Write-Output "skip: not a C# or XAML file"
    exit 0
}

if (-not (Test-Path -LiteralPath $full)) {
    Write-Output "skip: file missing"
    exit 0
}

$project = $null
$extra = @()
switch -Regex ($relative) {
    '^DataMan\.Contracts(\\|/)' {
        $project = Join-Path $root "DataMan.Contracts\DataMan.Contracts.csproj"
    }
    '^DataMan\.Core(\\|/)' {
        $project = Join-Path $root "DataMan.Core\DataMan.Core.csproj"
    }
    '^DataMan\.Tests(\\|/)' {
        $project = Join-Path $root "DataMan.Tests\DataMan.Tests.csproj"
    }
    '^DataMan(\\|/)' {
        $project = Join-Path $root "DataMan\DataMan.csproj"
        $extra = @("-p:Platform=x64")
    }
    default {
        Write-Output "skip: no project mapping"
        exit 0
    }
}

$include = $relative.Replace("\", "/")
& dotnet format $project --include $include --no-restore
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$buildArgs = @("build", $project, "-c", "Debug", "--nologo") + $extra
& dotnet @buildArgs
exit $LASTEXITCODE
