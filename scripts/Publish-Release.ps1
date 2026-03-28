param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [string]$RuntimeIdentifier = "win-x64",

    [switch]$SelfContained,

    [switch]$SkipValidation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Title,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host ("== {0} ==" -f $Title) -ForegroundColor Cyan
    & $Action
}

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw ("Missing {0}: {1}" -f $Description, $Path)
    }

    Write-Host ("OK: {0}" -f $Path) -ForegroundColor Green
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$validationScript = Join-Path $PSScriptRoot "Validate-Release.ps1"
$publishRoot = Join-Path $repoRoot "publish"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$deploymentKind = if ($SelfContained) { "self-contained" } else { "framework-dependent" }
$publishFolderName = "DINBoard-$Configuration-$RuntimeIdentifier-$deploymentKind-$timestamp"
$publishDirectory = Join-Path $publishRoot $publishFolderName
$zipPath = Join-Path $publishRoot ("{0}.zip" -f $publishFolderName)

Push-Location $repoRoot

try {
    if (-not $SkipValidation) {
        Invoke-Step -Title "Validate Release" -Action {
            & powershell -ExecutionPolicy Bypass -File $validationScript -Configuration $Configuration
            if ($LASTEXITCODE -ne 0) {
                throw "Release validation failed."
            }
        }
    }

    if (-not (Test-Path -LiteralPath $publishRoot)) {
        New-Item -ItemType Directory -Path $publishRoot | Out-Null
    }

    Invoke-Step -Title "Publish Application" -Action {
        $publishArgs = @(
            "publish",
            "DINBoard.csproj",
            "-c", $Configuration,
            "-r", $RuntimeIdentifier,
            "--self-contained", $(if ($SelfContained) { "true" } else { "false" }),
            "-o", $publishDirectory,
            "-p:PublishSingleFile=false"
        )

        & dotnet @publishArgs
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed."
        }
    }

    Invoke-Step -Title "Verify Publish Output" -Action {
        $exePath = Join-Path $publishDirectory "DINBoard.exe"
        $dllPath = Join-Path $publishDirectory "DINBoard.dll"

        Assert-PathExists -Path $publishDirectory -Description "publish directory"
        Assert-PathExists -Path $exePath -Description "published executable"
        Assert-PathExists -Path $dllPath -Description "published assembly"
    }

    Invoke-Step -Title "Create Zip Package" -Action {
        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }

        Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $zipPath -CompressionLevel Optimal
        Assert-PathExists -Path $zipPath -Description "zip package"
    }

    Write-Host ""
    Write-Host "Publish completed." -ForegroundColor Green
    Write-Host ("Folder: {0}" -f $publishDirectory) -ForegroundColor Yellow
    Write-Host ("Zip:    {0}" -f $zipPath) -ForegroundColor Yellow
}
finally {
    Pop-Location
}
