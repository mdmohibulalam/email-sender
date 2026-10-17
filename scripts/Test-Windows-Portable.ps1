$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$testDir = Join-Path $env:TEMP ('Studio-Portable-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $testDir | Out-Null
$readyFile = Join-Path $testDir 'ready.json'
$env:Studio__ApiKey = 'windows-smoke-api-key-for-isolated-test-only'
$env:Studio__TokenSecret = 'windows-smoke-token-secret-for-isolated-test-only'
$env:Studio__DataDirectory = $testDir
$env:Studio__ReadyFile = $readyFile
$env:Studio__EnableLiveSending = 'false'
Remove-Item Env:Studio__Postgres -ErrorAction SilentlyContinue
$process = Start-Process -FilePath (Join-Path $root 'artifacts/windows-portable/CampaignStudio.Api.exe') -ArgumentList '--urls','http://127.0.0.1:0' -PassThru -NoNewWindow -RedirectStandardOutput (Join-Path $testDir 'stdout.txt') -RedirectStandardError (Join-Path $testDir 'stderr.txt')
try {
    $ready = $false
    for ($i=0;$i -lt 120;$i++) {
        if ($process.HasExited) { throw (Get-Content -Raw (Join-Path $testDir 'stderr.txt')) }
        if (Test-Path $readyFile) {
            $launch = Get-Content -Raw $readyFile | ConvertFrom-Json
            if ($launch.pid -eq $process.Id -and $launch.version -eq '0.2.1') {
                $health = Invoke-RestMethod ($launch.url + '/health') -TimeoutSec 3
                $settings = Invoke-RestMethod ($launch.url + '/api/settings') -Headers @{Authorization=('Bearer ' + $env:Studio__ApiKey)} -TimeoutSec 3
                if ($health.version -eq '0.2.1' -and -not $settings.liveReady) { $ready=$true;break }
            }
        }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw 'Portable API startup timed out.' }
    Write-Host 'Windows portable startup, migrations, ready handshake and authenticated API passed.'
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() }
    foreach ($name in 'Studio__ApiKey','Studio__TokenSecret','Studio__DataDirectory','Studio__ReadyFile','Studio__EnableLiveSending') { Remove-Item ('Env:'+$name) -ErrorAction SilentlyContinue }
}
