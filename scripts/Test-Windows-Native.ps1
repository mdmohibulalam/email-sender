param([string]$BinaryPath = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $BinaryPath) { $BinaryPath = Join-Path $root 'artifacts/windows-native/CampaignStudio.Desktop.exe' }
$env:CAMPAIGN_STUDIO_DATA_DIRECTORY = Join-Path $env:TEMP ('Studio-Native-' + [Guid]::NewGuid().ToString('N'))
$env:CAMPAIGN_STUDIO_SMOKE_RESULT = Join-Path $env:TEMP ('Studio-Smoke-' + [Guid]::NewGuid().ToString('N') + '.txt')
$env:Studio__EnableLiveSending = 'false'
Remove-Item Env:CAMPAIGN_STUDIO_CLOUD_URL -ErrorAction SilentlyContinue
Remove-Item Env:CAMPAIGN_STUDIO_CLOUD_KEY -ErrorAction SilentlyContinue
$process = Start-Process -FilePath $BinaryPath -ArgumentList '--smoke-test' -PassThru
try {
    if (-not $process.WaitForExit(90000)) { Stop-Process -Id $process.Id; throw 'Native window startup timed out.' }
    if ($process.ExitCode -ne 0) {
        Get-WinEvent -FilterHashtable @{LogName='Application';StartTime=(Get-Date).AddMinutes(-5)} -ErrorAction SilentlyContinue |
            Where-Object { $_.ProviderName -in @('Application Error','.NET Runtime','Windows Error Reporting') } |
            Select-Object -First 5 TimeCreated,ProviderName,Message | Format-List | Out-Host
        $detail = if (Test-Path $env:CAMPAIGN_STUDIO_SMOKE_RESULT) { Get-Content $env:CAMPAIGN_STUDIO_SMOKE_RESULT -Raw } else { 'No diagnostic result was written.' }; throw ('Native window/WebView startup failed: ' + $process.ExitCode + ' ' + $detail) }
    Write-Host 'Native WinUI, local API, DPAPI, WebView authentication and rendered workspace passed.'
} finally {
    Remove-Item Env:CAMPAIGN_STUDIO_DATA_DIRECTORY -ErrorAction SilentlyContinue
    Remove-Item Env:CAMPAIGN_STUDIO_SMOKE_RESULT -ErrorAction SilentlyContinue
    Remove-Item Env:Studio__EnableLiveSending -ErrorAction SilentlyContinue
}
