# Build a sideload (non-Store) x64 MSIX of the WinUI client for testing in a VM (e.g. Hyper-V with Windows 10).
# Usage:
#   .\deploy\build-msix-test.ps1
#   .\deploy\build-msix-test.ps1 -Platform arm64
#
# Output: artifacts\msix-test\<package folder>\  (.msix, Dependencies\, SGSClient-test.cer, install-msix-test.ps1)
# Copy that whole folder to the VM and run install-msix-test.ps1 there as Administrator.
# The package is signed with a self-signed cert whose subject equals Package.appxmanifest Publisher, so Windows accepts it
# once the .cer is trusted. Never ship this build: .NET and Windows App SDK are bundled, nothing here goes to the Store.
# SelfContained is forced here because the csproj only sets it for the Store build; without it the VM asks for .NET Desktop Runtime.

param(
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $root "src\Apps\SGSClient.UI.WinUI"
$project = Join-Path $projectDir "SGSClient.UI.WinUI.csproj"
$outDir = Join-Path $root "artifacts\msix-test\"

[xml]$manifest = Get-Content (Join-Path $projectDir "Package.appxmanifest")
$publisher = $manifest.Package.Identity.Publisher

# Reuse the test cert between runs so the VM only has to trust it once.
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $publisher -and $_.FriendlyName -eq "SGSClient test" } |
    Sort-Object NotAfter -Descending | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type Custom -Subject $publisher -FriendlyName "SGSClient test" `
        -KeyUsage DigitalSignature -KeyExportPolicy Exportable -NotAfter (Get-Date).AddYears(2) `
        -CertStoreLocation Cert:\CurrentUser\My `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
}

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\amd64\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild not found (install Visual Studio with the Windows App SDK / .NET desktop workload)." }

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
# Stale Upload\AppxManifest.xml from earlier VS builds gets patched instead of regenerated.
Get-ChildItem "$projectDir\bin\*\Release", "$projectDir\obj\*\Release" -Directory -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

& $msbuild $project /restore /m /v:minimal `
    /p:Configuration=Release `
    /p:Platform=$Platform `
    /p:RuntimeIdentifier=win-$Platform `
    /p:SelfContained=true `
    /p:GenerateAppxPackageOnBuild=true `
    /p:AppxBundle=Never `
    /p:UapAppxPackageBuildMode=SideloadOnly `
    /p:AppxPackageSigningEnabled=true `
    /p:PackageCertificateThumbprint=$($cert.Thumbprint) `
    /p:GenerateTemporaryStoreCertificate=false `
    /p:AppxPackageDir="$outDir"
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed ($LASTEXITCODE)." }

$msix = Get-ChildItem $outDir -Recurse -Filter *.msix | Select-Object -First 1
if (-not $msix) { throw "No .msix in $outDir." }

Export-Certificate -Cert $cert -FilePath (Join-Path $msix.DirectoryName "SGSClient-test.cer") | Out-Null
Copy-Item (Join-Path $PSScriptRoot "install-msix-test.ps1") $msix.DirectoryName
Write-Host "Package folder (copy to the VM): $($msix.DirectoryName)"
