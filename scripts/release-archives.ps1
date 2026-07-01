param(
    [string]$Tag,
    [string]$ProjectPath = "Veeling.CLI/Veeling.CLI.csproj",
    [string]$OutputDir = "./artifacts/releases",
    [string[]]$Rids = @("win-x64", "linux-x64", "osx-x64", "osx-arm64"),
    [string]$NupkgDir = "./artifacts/nupkg",
    [ValidateSet("off", "optional", "required")]
    [string]$SigningMode = "optional",
    [ValidateSet("off", "optional", "required")]
    [string]$ProvenanceMode = "optional",
    [ValidateSet("off", "optional", "required")]
    [string]$SbomMode = "optional",
    [ValidateSet("auto", "syft")]
    [string]$SbomTool = "auto",
    [switch]$ValidateSigningConfig,
    [switch]$ValidateIntegrityConfig
)

$ErrorActionPreference = "Stop"

function Fail-OrWarn([string]$mode, [string]$message) {
    if ($mode -eq "required") {
        throw $message
    }

    if ($mode -eq "optional") {
        Write-Warning $message
    }
}

function Test-NugetSigningReady {
    if ([string]::IsNullOrWhiteSpace($env:VEELING_NUGET_SIGN_CERT_PATH)) {
        return $false
    }

    return Test-Path $env:VEELING_NUGET_SIGN_CERT_PATH
}

function Test-ArchiveSigningReady {
    if ([string]::IsNullOrWhiteSpace($env:VEELING_GPG_KEY_ID)) {
        return $false
    }

    $gpg = Get-Command gpg -ErrorAction SilentlyContinue
    return $null -ne $gpg
}

function Test-ProvenanceReady {
    git rev-parse --is-inside-work-tree *> $null
    return $LASTEXITCODE -eq 0
}

function Resolve-SbomTool {
    if ($SbomTool -eq "syft") {
        return "syft"
    }

    $syft = Get-Command syft -ErrorAction SilentlyContinue
    if ($null -ne $syft) {
        return "syft"
    }

    return ""
}

function Invoke-ValidateIntegrity {
    if ($SigningMode -eq "off") {
        Write-Host "[signing] Disabled by policy."
    }
    else {
        if (Test-NugetSigningReady) {
            Write-Host "[signing] NuGet signing configured."
        }
        else {
            Fail-OrWarn $SigningMode "NuGet signing not configured. Set VEELING_NUGET_SIGN_CERT_PATH to a .pfx certificate path."
        }

        if (Test-ArchiveSigningReady) {
            Write-Host "[signing] Archive signing configured (gpg key: $($env:VEELING_GPG_KEY_ID))."
        }
        else {
            Fail-OrWarn $SigningMode "Archive signing not configured. Install gpg and set VEELING_GPG_KEY_ID."
        }
    }

    if ($ProvenanceMode -eq "off") {
        Write-Host "[provenance] Disabled by policy."
    }
    elseif (Test-ProvenanceReady) {
        Write-Host "[provenance] Repository metadata available."
    }
    else {
        Fail-OrWarn $ProvenanceMode "Provenance generation requires git repository metadata."
    }

    if ($SbomMode -eq "off") {
        Write-Host "[sbom] Disabled by policy."
    }
    else {
        $tool = Resolve-SbomTool
        if ([string]::IsNullOrWhiteSpace($tool)) {
            Fail-OrWarn $SbomMode "No SBOM tool available (supported: syft)."
        }
        else {
            Write-Host "[sbom] Tool available: $tool"
        }
    }
}

if ($ValidateSigningConfig -or $ValidateIntegrityConfig) {
    Invoke-ValidateIntegrity
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Tag)) {
    throw "Tag is required unless validation mode is used."
}

if ($Tag -notmatch '^v(\d+)\.(\d+)\.(\d+)$') {
    throw "Tag '$Tag' is not SemVer-compliant. Expected format: vX.Y.Z"
}

$tagRef = "refs/tags/$Tag"
git rev-parse -q --verify $tagRef *> $null
if ($LASTEXITCODE -ne 0) {
    throw "Tag '$Tag' does not exist locally. Fetch tags or create it first."
}

$objectType = (git for-each-ref $tagRef --format='%(objecttype)').Trim()
if ($objectType -ne "tag") {
    throw "Tag '$Tag' is lightweight. Use annotated tags: git tag -a $Tag -m 'Release $Tag'"
}

$tagCommit = (git rev-list -n 1 $Tag).Trim()
$headCommit = (git rev-parse HEAD).Trim()
if ($tagCommit -ne $headCommit) {
    throw "Tag '$Tag' points to $tagCommit, but HEAD is $headCommit. Tag the release commit."
}

$version = (dotnet msbuild $ProjectPath -nologo -getProperty:Version).Trim()
$tagVersion = $Tag.Substring(1)
if ($version -ne $tagVersion) {
    throw "Tag version '$tagVersion' does not match canonical version '$version'."
}

if ($Rids.Count -eq 1 -and $Rids[0].Contains(",")) {
    $splitOptions = [System.StringSplitOptions]::RemoveEmptyEntries -bor [System.StringSplitOptions]::TrimEntries
    $Rids = $Rids[0].Split(",", $splitOptions)
}

if ($Rids.Count -eq 0) {
    throw "At least one RID must be provided."
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$OutputDir = (Resolve-Path $OutputDir).Path

$tmpRoot = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString("N"))
$null = New-Item -ItemType Directory -Path $tmpRoot -Force

$archivePaths = [System.Collections.Generic.List[string]]::new()
$allChecksumTargets = [System.Collections.Generic.List[string]]::new()
$signatureTargets = [System.Collections.Generic.List[string]]::new()
$sbomTargets = [System.Collections.Generic.List[string]]::new()

try {
    foreach ($rid in $Rids) {
        Write-Host "Publishing RID: $rid"

        $publishDir = Join-Path $tmpRoot "publish/$rid"
        $stageRoot = Join-Path $tmpRoot "stage"
        $stageDir = Join-Path $stageRoot "veeling-$version-$rid"

        dotnet publish $ProjectPath -c Release -r $rid --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o $publishDir

        New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
        Copy-Item -Path (Join-Path $publishDir "*") -Destination $stageDir -Recurse -Force

        if ($rid.StartsWith("win-")) {
            $archivePath = Join-Path $OutputDir "veeling-$version-$rid.zip"
            if (Test-Path $archivePath) {
                Remove-Item $archivePath -Force
            }

            Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $archivePath -Force
        }
        else {
            $archivePath = Join-Path $OutputDir "veeling-$version-$rid.tar.gz"
            if (Test-Path $archivePath) {
                Remove-Item $archivePath -Force
            }

            $windowsTar = Join-Path $env:WINDIR "System32/tar.exe"
            if (-not (Test-Path $windowsTar)) {
                throw "Windows tar.exe not found at $windowsTar"
            }

            & $windowsTar -C $stageRoot -czf $archivePath "veeling-$version-$rid"
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path $archivePath)) {
                throw "Failed to create archive: $archivePath"
            }
        }

        $archivePaths.Add($archivePath)
        $allChecksumTargets.Add($archivePath)
        Write-Host "Created: $archivePath"
    }

    New-Item -ItemType Directory -Path $NupkgDir -Force | Out-Null
    $NupkgDir = (Resolve-Path $NupkgDir).Path

    dotnet pack $ProjectPath -c Release -o $NupkgDir

    $nupkgPattern = Join-Path $NupkgDir "veeling.$version.nupkg"
    $nupkgPath = Get-ChildItem -Path $nupkgPattern -File | Select-Object -First 1
    if ($null -eq $nupkgPath) {
        throw "NuGet package not found at expected path pattern: $nupkgPattern"
    }

    $allChecksumTargets.Add($nupkgPath.FullName)
    Write-Host "Created/verified package: $($nupkgPath.FullName)"

    if ($SigningMode -ne "off") {
        if (Test-NugetSigningReady) {
            $nugetSignArgs = @(
                "nuget", "sign", $nupkgPath.FullName,
                "--certificate-path", $env:VEELING_NUGET_SIGN_CERT_PATH,
                "--hash-algorithm", "SHA256"
            )

            if (-not [string]::IsNullOrWhiteSpace($env:VEELING_NUGET_SIGN_CERT_PASSWORD)) {
                $nugetSignArgs += @("--certificate-password", $env:VEELING_NUGET_SIGN_CERT_PASSWORD)
            }

            if (-not [string]::IsNullOrWhiteSpace($env:VEELING_NUGET_SIGN_TIMESTAMP_URL)) {
                $nugetSignArgs += @("--timestamper", $env:VEELING_NUGET_SIGN_TIMESTAMP_URL)
            }

            & dotnet @nugetSignArgs
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet nuget sign failed for $($nupkgPath.FullName)"
            }

            Write-Host "Signed NuGet package: $($nupkgPath.FullName)"
        }
        else {
            Fail-OrWarn $SigningMode "Skipping NuGet signing: VEELING_NUGET_SIGN_CERT_PATH missing or file does not exist."
        }

        if (Test-ArchiveSigningReady) {
            foreach ($archive in $archivePaths) {
                $sigPath = "$archive.asc"
                & gpg --batch --yes --armor --detach-sign --local-user $env:VEELING_GPG_KEY_ID --output $sigPath $archive
                if ($LASTEXITCODE -ne 0 -or -not (Test-Path $sigPath)) {
                    throw "gpg signing failed for $archive"
                }

                $signatureTargets.Add($sigPath)
                Write-Host "Signed archive: $sigPath"
            }
        }
        else {
            Fail-OrWarn $SigningMode "Skipping archive signing: gpg/VEELING_GPG_KEY_ID unavailable."
        }
    }

    foreach ($sig in $signatureTargets) {
        $allChecksumTargets.Add($sig)
    }

    if ($SbomMode -ne "off") {
        $resolvedSbomTool = Resolve-SbomTool
        if ([string]::IsNullOrWhiteSpace($resolvedSbomTool)) {
            Fail-OrWarn $SbomMode "Skipping SBOM generation: syft not available."
        }
        else {
            $sbomFile = Join-Path $OutputDir "sbom.$version.cdx.json"
            & syft $nupkgPath.FullName -o cyclonedx-json | Out-File -FilePath $sbomFile -Encoding utf8
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path $sbomFile)) {
                throw "SBOM generation failed."
            }

            $sbomTargets.Add($sbomFile)
            $allChecksumTargets.Add($sbomFile)
            Write-Host "Generated SBOM: $sbomFile"
        }
    }

    if ($ProvenanceMode -ne "off") {
        if (Test-ProvenanceReady) {
            $provenanceFile = Join-Path $OutputDir "provenance.intoto.json"
            $commitSha = (git rev-parse HEAD).Trim()
            $repoUrl = if ([string]::IsNullOrWhiteSpace($env:VEELING_REPOSITORY_URL)) { "https://github.com/llaurila/veeling" } else { $env:VEELING_REPOSITORY_URL }
            $createdAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

            $subject = @()
            foreach ($target in ($allChecksumTargets | Sort-Object)) {
                $hash = (Get-FileHash -Path $target -Algorithm SHA256).Hash.ToLowerInvariant()
                $name = [System.IO.Path]::GetFileName($target)
                $subject += @{
                    name = $name
                    digest = @{ sha256 = $hash }
                }
            }

            $predicate = @{
                _type = "https://in-toto.io/Statement/v1"
                subject = $subject
                predicateType = "https://slsa.dev/provenance/v1"
                predicate = @{
                    buildDefinition = @{
                        buildType = "https://veeling.dev/release-archives-script/v1"
                        externalParameters = @{
                            tag = $Tag
                            version = $version
                            signing_mode = $SigningMode
                            sbom_mode = $SbomMode
                        }
                    }
                    runDetails = @{
                        builder = @{ id = "veeling/scripts/release-archives.ps1" }
                        metadata = @{
                            invocationId = $commitSha
                            startedOn = $createdAt
                            finishedOn = $createdAt
                        }
                    }
                    materials = @(
                        @{
                            uri = $repoUrl
                            digest = @{ sha1 = $commitSha }
                        }
                    )
                }
            }

            $predicate | ConvertTo-Json -Depth 10 | Out-File -FilePath $provenanceFile -Encoding utf8
            $allChecksumTargets.Add($provenanceFile)
            Write-Host "Generated provenance attestation: $provenanceFile"
        }
        else {
            Fail-OrWarn $ProvenanceMode "Skipping provenance generation: git metadata unavailable."
        }
    }

    $checksumFile = Join-Path $OutputDir "SHA256SUMS"
    if (Test-Path $checksumFile) {
        Remove-Item $checksumFile -Force
    }

    $orderedTargets = $allChecksumTargets | Sort-Object

    foreach ($target in $orderedTargets) {
        $hash = (Get-FileHash -Path $target -Algorithm SHA256).Hash.ToLowerInvariant()
        $name = [System.IO.Path]::GetFileName($target)
        Add-Content -Path $checksumFile -Value "$hash  $name"
    }

    Write-Host "Created: $checksumFile"
}
finally {
    if (Test-Path $tmpRoot) {
        Remove-Item $tmpRoot -Recurse -Force
    }
}
