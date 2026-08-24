param(
    [switch]$RequireAutomation
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$failed = $false

function Write-Check {
    param(
        [string]$Name,
        [ValidateSet("PASS", "FAIL", "SKIP", "WARN")]
        [string]$Status,
        [string]$Detail = ""
    )
    if ($Detail) {
        Write-Output "$Status $Name $Detail"
    } else {
        Write-Output "$Status $Name"
    }
}

function Invoke-RequiredScript {
    param(
        [string]$Name,
        [string]$RelPath
    )
    $script = Join-Path $root $RelPath
    $psExe = (Get-Process -Id $PID).Path
    & $psExe -NoProfile -ExecutionPolicy Bypass -File $script
    if ($LASTEXITCODE -ne 0) {
        Write-Check -Name $Name -Status "FAIL" -Detail "exit $LASTEXITCODE"
        $script:failed = $true
        return
    }
    Write-Check -Name $Name -Status "PASS"
}

function Write-OptionalIssue {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Detail = ""
    )
    if ($RequireAutomation) {
        Write-Check -Name $Name -Status "FAIL" -Detail $Detail
        $script:failed = $true
        return
    }
    Write-Check -Name $Name -Status $Status -Detail $Detail
}

Invoke-RequiredScript -Name "mvp" -RelPath "scripts\verify-mvp.ps1"
Invoke-RequiredScript -Name "cursor" -RelPath "scripts\verify-cursor-setup.ps1"

$gt = Get-Command gt -ErrorAction SilentlyContinue
if ($null -eq $gt) {
    Write-OptionalIssue -Name "graphite-cli" -Status "SKIP" -Detail "gt not on PATH"
} else {
    $gtVersion = & gt --version
    Write-Check -Name "graphite-cli" -Status "PASS" -Detail $gtVersion.Trim()

    $gitCommonDir = (git -C $root rev-parse --git-common-dir).Trim()
    if (-not [IO.Path]::IsPathRooted($gitCommonDir)) {
        $gitCommonDir = Join-Path $root $gitCommonDir
    }
    $repoConfigPath = Join-Path $gitCommonDir ".graphite_repo_config"
    if (Test-Path -LiteralPath $repoConfigPath) {
        $repoConfig = Get-Content -LiteralPath $repoConfigPath -Raw | ConvertFrom-Json
        if ($repoConfig.trunk -eq "main") {
            Write-Check -Name "graphite-trunk" -Status "PASS" -Detail "main"
        } else {
            Write-OptionalIssue -Name "graphite-trunk" -Status "WARN" -Detail "trunk is '$($repoConfig.trunk)'"
        }
    } else {
        Write-OptionalIssue -Name "graphite-trunk" -Status "SKIP" -Detail "run gt init --trunk main"
    }

    $userConfigPath = Join-Path $env:USERPROFILE ".config\graphite\user_config"
    $authed = $false
    if (Test-Path -LiteralPath $userConfigPath) {
        $userConfig = Get-Content -LiteralPath $userConfigPath -Raw | ConvertFrom-Json
        $authed = [bool]$userConfig.authToken
    }
    if ($authed) {
        Write-Check -Name "graphite-auth" -Status "PASS"
    } else {
        Write-OptionalIssue -Name "graphite-auth" -Status "SKIP" -Detail "https://app.graphite.com/activate"
    }
}

$gh = Get-Command gh -ErrorAction SilentlyContinue
if ($null -eq $gh) {
    Write-OptionalIssue -Name "actions-create-pr" -Status "SKIP" -Detail "gh not on PATH"
} else {
    $origin = git -C $root remote get-url origin
    if ($origin -notmatch "github\.com[:/]([^/]+)/([^/.]+)") {
        Write-OptionalIssue -Name "actions-create-pr" -Status "SKIP" -Detail "origin is not GitHub"
    } else {
        $slug = "$($Matches[1])/$($Matches[2])"
        $permsJson = gh api "repos/$slug/actions/permissions/workflow" 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-OptionalIssue -Name "actions-create-pr" -Status "WARN" -Detail "gh api failed"
        } else {
            try {
              $perms = $permsJson | ConvertFrom-Json -ErrorAction Stop
          } catch {
              Write-OptionalIssue -Name "actions-create-pr" -Status "WARN" -Detail "gh api returned invalid JSON"
              $perms = $null
          }
            $canCreate = ($null -ne $perms) -and ($perms.default_workflow_permissions -eq "write") -and $perms.can_approve_pull_request_reviews
            if ($canCreate) {
                Write-Check -Name "actions-create-pr" -Status "PASS"
            } else {
                Write-OptionalIssue -Name "actions-create-pr" -Status "WARN" -Detail "org policy keeps workflow permissions at $($perms.default_workflow_permissions)"
            }
        }
    }
}

if ($failed) {
    exit 1
}
Write-Output "ship verified"
exit 0
