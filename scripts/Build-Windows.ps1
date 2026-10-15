$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    dotnet run --project tests/CampaignStudio.Tests
    if ($LASTEXITCODE -ne 0) { throw 'Functional checks failed.' }
    dotnet publish src/CampaignStudio.Desktop -c Release -r win-x64 -p:Platform=x64 --self-contained true -o artifacts/windows-native
    if ($LASTEXITCODE -ne 0) { throw 'Native Windows build failed.' }
    Write-Host 'Built artifacts/windows-native/CampaignStudio.Desktop.exe'
    Write-Host 'This is an unsigned preview. Complete the Windows verification checklist before release.'
} finally { Pop-Location }
