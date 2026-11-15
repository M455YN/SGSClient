# Install the test MSIX built by build-msix-test.ps1. Run inside the test VM, as Administrator, from the package folder:
#   powershell -ExecutionPolicy Bypass -File .\install-msix-test.ps1
# Trusts SGSClient-test.cer (TrustedPeople, local machine) and installs the package with its dependencies.
# Remove again with:  Get-AppxPackage *SGSClient* | Remove-AppxPackage   (and delete the cert from TrustedPeople)

$ErrorActionPreference = "Stop"

$admin = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $admin.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script as Administrator (needed to trust the test certificate)."
}

$dir = $PSScriptRoot
$cer = Join-Path $dir "SGSClient-test.cer"
$msix = Get-ChildItem $dir -Filter *.msix | Select-Object -First 1
if (-not (Test-Path $cer) -or -not $msix) { throw "Expected SGSClient-test.cer and a .msix next to this script." }

Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null

$deps = @(Get-ChildItem (Join-Path $dir "Dependencies") -Recurse -Filter *.appx -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "\\(x64|x86|arm64)\\" } | ForEach-Object FullName)
$arch = if ([Environment]::Is64BitOperatingSystem) { "x64" } else { "x86" }
$deps = @($deps | Where-Object { $_ -match "\\$arch\\" })

if ($deps.Count -gt 0) {
    Add-AppxPackage -Path $msix.FullName -DependencyPath $deps -ForceUpdateFromAnyVersion
} else {
    Add-AppxPackage -Path $msix.FullName -ForceUpdateFromAnyVersion
}
Write-Host "Installed. Start 'SGSClient' from the Start menu."
