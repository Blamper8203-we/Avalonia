param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [switch]$NoRestore
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
Push-Location $repoRoot

try {
    if (-not $NoRestore) {
        Invoke-Step -Title "Restore" -Action {
            & dotnet restore "Avalonia.sln"
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet restore failed."
            }
        }
    }

    Invoke-Step -Title "Build Solution" -Action {
        $buildArgs = @("build", "Avalonia.sln", "-c", $Configuration)
        if ($NoRestore) {
            $buildArgs += "--no-restore"
        }

        & dotnet @buildArgs
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed."
        }
    }

    Invoke-Step -Title "Run Tests" -Action {
        & dotnet test "Tests\Avalonia.Tests.csproj" "-c" $Configuration "--no-build" "--no-restore" "-v" "minimal"
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet test failed."
        }
    }

    Invoke-Step -Title "Verify Artifacts" -Action {
        $appDll = Join-Path $repoRoot ("bin\{0}\net10.0\DINBoard.dll" -f $Configuration)
        $testDll = Join-Path $repoRoot ("Tests\bin\{0}\net10.0\Avalonia.Tests.dll" -f $Configuration)

        Assert-PathExists -Path $appDll -Description "main app artifact"
        Assert-PathExists -Path $testDll -Description "test artifact"
    }

    Write-Host ""
    Write-Host "Release validation passed." -ForegroundColor Green
    Write-Host "Next step: run the manual checklist in RELEASE_CHECKLIST.md" -ForegroundColor Yellow
}
finally {
    Pop-Location
}
