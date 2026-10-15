$ErrorActionPreference = 'Stop'
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 32
$rng.GetBytes($bytes)
$env:Studio__ApiKey = [Convert]::ToBase64String($bytes)
$dataDir = Join-Path $env:LOCALAPPDATA 'CampaignStudio-Development'
New-Item -ItemType Directory -Force $dataDir | Out-Null
$secretFile = Join-Path $dataDir 'unsubscribe-secret.txt'
if (-not (Test-Path $secretFile)) {
    $rng.GetBytes($bytes)
    ConvertTo-SecureString ([Convert]::ToBase64String($bytes)) -AsPlainText -Force | ConvertFrom-SecureString | Set-Content $secretFile
}
$secure = ConvertTo-SecureString (Get-Content -Raw $secretFile)
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try { $env:Studio__TokenSecret = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
$rng.Dispose()
$env:Studio__DataDirectory = Join-Path $env:LOCALAPPDATA 'CampaignStudio-Development'
$env:Studio__EnableLiveSending = 'false'
Set-Clipboard $env:Studio__ApiKey
Write-Host 'Development access key copied to clipboard. Paste it at http://127.0.0.1:5088'
Write-Host 'Local test capture only. Keep this window open.'
dotnet run --project (Join-Path (Split-Path $PSScriptRoot -Parent) 'src/CampaignStudio.Api') -- --urls http://127.0.0.1:5088
