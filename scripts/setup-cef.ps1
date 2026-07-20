$ErrorActionPreference = "Stop"

$RepositoryRoot = Split-Path -Parent $PSScriptRoot
$PackageCache = Join-Path $RepositoryRoot "moorestech_client/Library/PackageCache"

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Error "git is required. Install Git and run this script again."
}

& git lfs version *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Error "git-lfs is required. Install Git LFS and run this script again."
}

Write-Host "Configuring the Git LFS smudge filter for UPM git dependencies..."
& git lfs install
if ($LASTEXITCODE -ne 0) {
    Write-Error "git lfs install failed with exit code $LASTEXITCODE."
}

if (Test-Path $PackageCache) {
    Write-Host "Removing cached jp.juha.cefunity packages..."
    Get-ChildItem -Path $PackageCache -Directory -Filter "jp.juha.cefunity@*" |
        Remove-Item -Recurse -Force
}

Write-Host "CEF setup is ready. Open moorestech_client in Unity to let UPM resolve the package again."
