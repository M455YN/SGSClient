# Build linux/amd64 API image and optionally push to GHCR.
# Usage:
#   .\deploy\build-api-image.ps1
#   .\deploy\build-api-image.ps1 -Tag 2026-08-14 -Push
#
# First-time GHCR login (PAT with write:packages):
#   docker login ghcr.io -u M455YN

param(
    [string]$Tag = "latest",
    [switch]$Push,
    [switch]$ExportTar,
    [string]$RegistryImage = "ghcr.io/m455yn/sgsclient-api"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$local = "sgsclient-api:$Tag"
$remote = "${RegistryImage}:$Tag"

Write-Host "Building $local (linux/amd64)..."
docker build --platform linux/amd64 -t $local -t $remote -f src/Apps/SGSClient.Api/Dockerfile .

if ($ExportTar) {
    $tar = Join-Path $root "sgsclient-api-linux-amd64.tar"
    Write-Host "Saving $tar ..."
    docker save $local -o $tar
}

if ($Push) {
    Write-Host "Pushing $remote ..."
    docker push $remote
    if ($Tag -ne "latest") {
        docker tag $local "${RegistryImage}:latest"
        docker push "${RegistryImage}:latest"
    }
}

Write-Host "Done. Portainer env SGS_API_IMAGE=$remote"
