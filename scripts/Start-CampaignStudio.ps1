$ErrorActionPreference = 'Stop'
$binary = Join-Path $PSScriptRoot 'CampaignStudio.Api.exe'
if (-not (Test-Path $binary)) { throw 'Extract the complete portable preview ZIP first. Do not run this file from inside the ZIP.' }
$dataDir = Join-Path $env:LOCALAPPDATA 'CampaignStudio-Portable'
New-Item -ItemType Directory -Force $dataDir | Out-Null
$secretFile = Join-Path $dataDir 'secrets.json'
if (Test-Path $secretFile) {
    $stored = Get-Content -Raw $secretFile | ConvertFrom-Json
    $apiSecure = ConvertTo-SecureString $stored.api
    $tokenSecure = ConvertTo-SecureString $stored.token
} else {
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] 32
    $rng.GetBytes($bytes)
    $apiSecure = ConvertTo-SecureString ([Convert]::ToBase64String($bytes)) -AsPlainText -Force
    $rng.GetBytes($bytes)
    $tokenSecure = ConvertTo-SecureString ([Convert]::ToBase64String($bytes)) -AsPlainText -Force
    $rng.Dispose()
    @{api = (ConvertFrom-SecureString $apiSecure); token = (ConvertFrom-SecureString $tokenSecure)} | ConvertTo-Json | Set-Content -Encoding UTF8 $secretFile
}
function Reveal($secure) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}
$env:Studio__ApiKey = Reveal $apiSecure
$env:Studio__TokenSecret = Reveal $tokenSecure
$env:Studio__DataDirectory = $dataDir
$env:Studio__EnableLiveSending = 'false'
Remove-Item Env:Studio__Postgres -ErrorAction SilentlyContinue
$env:Studio__PublicBaseUrl = ''
$env:Logging__LogLevel__Default = 'Warning'
# Let Windows allocate an unused loopback port. Never bind this preview to the network.
$listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$url = "http://127.0.0.1:$port"
$process = Start-Process -FilePath $binary -ArgumentList '--urls', $url -WorkingDirectory $PSScriptRoot -PassThru -NoNewWindow
try {
    $ready = $false
    for ($i = 0; $i -lt 80; $i++) {
        if ($process.HasExited) { throw 'The workspace server exited. Check the error above.' }
        try { $health = Invoke-RestMethod "$url/health" -TimeoutSec 1; if ($health.status -eq 'ok') { $ready = $true; break } } catch {}
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw 'The workspace server did not start in time.' }
    Start-Process ($url + '/#connect=' + [Uri]::EscapeDataString($env:Studio__ApiKey))
    Write-Host 'Campaign Studio is open in your browser.'
    Write-Host 'Keep this window open while a local campaign runs.'
    Write-Host 'Test capture mode saves emails. No emails are sent.'
    Write-Host 'Close this window to stop the local workspace.'
    $process.WaitForExit()
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
    Remove-Item Env:Studio__ApiKey -ErrorAction SilentlyContinue
    Remove-Item Env:Studio__TokenSecret -ErrorAction SilentlyContinue
}
