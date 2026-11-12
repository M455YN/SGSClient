# Build the WinUI client as a Microsoft Store .msixbundle.
# Usage:
#   .\deploy\build-msixbundle.ps1
#   .\deploy\build-msixbundle.ps1 -Platforms "x64|arm64|x86"
#
# Output: artifacts\msix\SGSClient.UI.WinUI_<version>_<platforms>.msixbundle  (upload this to Partner Center)
# Partner Center signs the package itself, so nothing here is signed.
# Package identity (Name, Publisher, PublisherDisplayName) lives in src\Apps\SGSClient.UI.WinUI\Package.appxmanifest
# and must match Partner Center > Product identity. Version comes from Package.appxmanifest (x.y.z.0).

param(
    [string]$Platforms = "x64|arm64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\Apps\SGSClient.UI.WinUI\SGSClient.UI.WinUI.csproj"
$outDir = Join-Path $root "artifacts\msix\"

# Store builds read B2C sign-in settings from this untracked file; without it the Store app ships with login disabled.
if (-not (Test-Path (Join-Path $root "src\Apps\SGSClient.UI.WinUI\appsettings.local.json"))) {
    throw "Missing src\Apps\SGSClient.UI.WinUI\appsettings.local.json (production B2C settings)."
}

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\amd64\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild not found (install Visual Studio with the Windows App SDK / .NET desktop workload)." }

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
# Stale Upload\AppxManifest.xml from earlier VS builds gets patched instead of regenerated and breaks the bundle.
$projectDir = Split-Path -Parent $project
Get-ChildItem "$projectDir\bin\*\Release", "$projectDir\obj\*\Release" -Directory -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

$firstPlatform = $Platforms.Split("|")[0]
& $msbuild $project /restore /m /v:minimal `
    /p:Configuration=Release `
    /p:Platform=$firstPlatform `
    /p:GenerateAppxPackageOnBuild=true `
    /p:AppxBundle=Always `
    /p:AppxBundlePlatforms="$Platforms" `
    /p:UapAppxPackageBuildMode=StoreUpload `
    /p:AppxPackageSigningEnabled=false `
    /p:GenerateTemporaryStoreCertificate=false `
    /p:AppxPackageDir="$outDir"
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed ($LASTEXITCODE)." }

# Keep only the .msixbundle (the .msixupload just wraps the same bundle plus symbols).
$bundle = Get-ChildItem $outDir -Recurse -Filter *.msixbundle | Select-Object -First 1
if (-not $bundle) { throw "No .msixbundle in $outDir." }
Move-Item $bundle.FullName $outDir -Force
Get-ChildItem $outDir | Where-Object { $_.Extension -ne ".msixbundle" } | Remove-Item -Recurse -Force
Write-Host "Bundle: $(Join-Path $outDir $bundle.Name)"
