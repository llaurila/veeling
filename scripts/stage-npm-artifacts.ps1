param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$ArchiveInput = "./artifacts/releases",
    [string]$NpmRoot = "./npm",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

$releaseVersion = $Version.Trim()
if ($releaseVersion.StartsWith("v")) {
    $releaseVersion = $releaseVersion.Substring(1)
}

if ($releaseVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be SemVer (X.Y.Z or vX.Y.Z). Received: '$Version'"
}

$archiveInputPath = (Resolve-Path -LiteralPath $ArchiveInput).Path
$npmRootPath = (Resolve-Path -LiteralPath $NpmRoot).Path

$matrix = @(
    @{ Rid = "win-x64"; Package = "cli-win32-x64"; ArchiveExt = "zip"; Binary = "veeling.exe"; IsWindows = $true },
    @{ Rid = "linux-x64"; Package = "cli-linux-x64"; ArchiveExt = "tar.gz"; Binary = "veeling"; IsWindows = $false },
    @{ Rid = "osx-x64"; Package = "cli-darwin-x64"; ArchiveExt = "tar.gz"; Binary = "veeling"; IsWindows = $false },
    @{ Rid = "osx-arm64"; Package = "cli-darwin-arm64"; ArchiveExt = "tar.gz"; Binary = "veeling"; IsWindows = $false }
)

foreach ($item in $matrix) {
    $packageRoot = Join-Path $npmRootPath "packages/$($item.Package)"
    $binDir = Join-Path $packageRoot "bin"
    $archiveName = "veeling-$releaseVersion-$($item.Rid).$($item.ArchiveExt)"
    $archivePath = Join-Path $archiveInputPath $archiveName

    if (-not (Test-Path -LiteralPath $archivePath)) {
        throw "Expected archive not found: $archivePath"
    }

    if (-not (Test-Path -LiteralPath $packageRoot)) {
        throw "Expected npm package directory not found: $packageRoot"
    }

    if ($Clean -and (Test-Path -LiteralPath $binDir)) {
        Remove-Item -LiteralPath $binDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $binDir -Force | Out-Null

    if ($item.IsWindows) {
        $extractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("veeling-npm-stage-" + [System.Guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null
        try {
            Expand-Archive -Path $archivePath -DestinationPath $extractRoot -Force
            $binaryPath = Join-Path $extractRoot "veeling-$releaseVersion-$($item.Rid)/$($item.Binary)"
            if (-not (Test-Path -LiteralPath $binaryPath)) {
                throw "Expected binary not found in archive: $binaryPath"
            }
            Copy-Item -LiteralPath $binaryPath -Destination (Join-Path $binDir $item.Binary) -Force
        }
        finally {
            if (Test-Path -LiteralPath $extractRoot) {
                Remove-Item -LiteralPath $extractRoot -Recurse -Force
            }
        }
    }
    else {
        $tar = Get-Command tar -ErrorAction SilentlyContinue
        if ($null -eq $tar) {
            throw "tar command is required to stage non-Windows artifacts."
        }

        $extractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("veeling-npm-stage-" + [System.Guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null
        try {
            & $tar.Path -xzf $archivePath -C $extractRoot
            if ($LASTEXITCODE -ne 0) {
                throw "tar extraction failed for $archivePath"
            }

            $binaryPath = Join-Path $extractRoot "veeling-$releaseVersion-$($item.Rid)/$($item.Binary)"
            if (-not (Test-Path -LiteralPath $binaryPath)) {
                throw "Expected binary not found in archive: $binaryPath"
            }

            $targetBinary = Join-Path $binDir $item.Binary
            Copy-Item -LiteralPath $binaryPath -Destination $targetBinary -Force
        }
        finally {
            if (Test-Path -LiteralPath $extractRoot) {
                Remove-Item -LiteralPath $extractRoot -Recurse -Force
            }
        }
    }

    $packageJsonPath = Join-Path $packageRoot "package.json"
    $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
    $packageJson.version = $releaseVersion
    ($packageJson | ConvertTo-Json -Depth 20) + "`n" | Set-Content -LiteralPath $packageJsonPath
}

$metaPackagePath = Join-Path $npmRootPath "packages/cli/package.json"
$metaPackage = Get-Content -LiteralPath $metaPackagePath -Raw | ConvertFrom-Json
$metaPackage.version = $releaseVersion

foreach ($name in @(
    "@veeling/cli-win32-x64",
    "@veeling/cli-linux-x64",
    "@veeling/cli-darwin-x64",
    "@veeling/cli-darwin-arm64"
)) {
    $metaPackage.optionalDependencies.$name = $releaseVersion
}

($metaPackage | ConvertTo-Json -Depth 20) + "`n" | Set-Content -LiteralPath $metaPackagePath

Write-Host "Staged npm platform artifacts for version $releaseVersion from $archiveInputPath"
