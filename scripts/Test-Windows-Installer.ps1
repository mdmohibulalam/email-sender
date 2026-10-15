$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$installer = Join-Path $root 'artifacts/installer/CampaignStudio-Setup-0.2.0-win-x64.exe'
$target = Join-Path $env:TEMP ('Studio-Install-' + [Guid]::NewGuid().ToString('N'))
try {
    $process = Start-Process -FilePath $installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="' + $target + '"')) -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw ('Installer failed: ' + $process.ExitCode) }
    $binary = Join-Path $target 'CampaignStudio.Desktop.exe'
    if (-not (Test-Path $binary)) { throw 'Installed native executable is missing.' }
    & (Join-Path $PSScriptRoot 'Test-Windows-Native.ps1') -BinaryPath $binary
    $process = Start-Process -FilePath (Join-Path $target 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -PassThru -Wait
    if ($process.ExitCode -ne 0 -or (Test-Path $binary)) { throw 'Uninstall did not remove the installed application.' }
    Write-Host 'Installer, installed native workspace startup and uninstall passed.'
} finally {
    if (Test-Path $target) { Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue }
}
