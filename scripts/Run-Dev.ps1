$ErrorActionPreference = 'Stop'
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 32
$rng.GetBytes($bytes)
$env:Studio__ApiKey = [Convert]::ToBase64String($bytes)
$rng.GetBytes($bytes)
$env:Studio__TokenSecret = [Convert]::ToBase64String($bytes)
$rng.Dispose()
$env:Studio__DataDirectory = Join-Path $env:LOCALAPPDATA 'CampaignStudio-Development'
$env:Studio__EnableLiveSending = 'false'
Set-Clipboard $env:Studio__ApiKey
Write-Host 'Development access key copied to clipboard. Paste it at http://127.0.0.1:5088'
Write-Host 'Local test capture only. Keep this window open.'
dotnet run --project (Join-Path (Split-Path $PSScriptRoot -Parent) 'src/CampaignStudio.Api') -- --urls http://127.0.0.1:5088
