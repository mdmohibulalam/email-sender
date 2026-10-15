$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    dotnet run --project tests/CampaignStudio.Tests
    if ($LASTEXITCODE -ne 0) { throw 'Functional checks failed.' }
    dotnet publish src/CampaignStudio.Desktop -c Release -r win-x64 -p:Platform=x64 --self-contained true -o artifacts/windows-native
    if ($LASTEXITCODE -ne 0) { throw 'Native Windows build failed.' }
    dotnet publish src/CampaignStudio.Api -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -o artifacts/windows-portable
    if ($LASTEXITCODE -ne 0) { throw 'Portable Windows build failed.' }
    Copy-Item scripts/Start-CampaignStudio.cmd,scripts/Start-CampaignStudio.ps1 artifacts/windows-portable/
    & ./scripts/Test-Windows-Portable.ps1
    & ./scripts/Test-Windows-Native.ps1
    Write-Host 'Windows previews built and smoke checks passed. Publisher signing is not configured.'
} finally { Pop-Location }
