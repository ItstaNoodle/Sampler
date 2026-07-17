$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$pluginSource = Join-Path $root 'plugin\com.noble.sampler.sdPlugin'
$pluginTarget = Join-Path $env:APPDATA 'Elgato\StreamDeck\Plugins\com.noble.sampler.sdPlugin'

Write-Host 'Building the Windows audio service...'
& (Join-Path $root 'audio-service\publish.ps1')

Write-Host 'Installing the Stream Deck plugin...'
if (Test-Path $pluginTarget) { Remove-Item $pluginTarget -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Split-Path $pluginTarget) | Out-Null
Copy-Item $pluginSource $pluginTarget -Recurse -Force

$serviceExe = Join-Path $root 'audio-service\publish\NobleSampler.AudioService.exe'
$startup = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\Noble Sampler Audio Service.lnk'
$ws = New-Object -ComObject WScript.Shell
$shortcut = $ws.CreateShortcut($startup)
$shortcut.TargetPath = $serviceExe
$shortcut.WorkingDirectory = Split-Path $serviceExe
$shortcut.WindowStyle = 7
$shortcut.Save()

Start-Process $serviceExe
Write-Host ''
Write-Host 'Installed. Fully quit and reopen the Stream Deck application.' -ForegroundColor Green
