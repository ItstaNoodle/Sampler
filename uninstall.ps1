$pluginTarget = Join-Path $env:APPDATA 'Elgato\StreamDeck\Plugins\com.noble.sampler.sdPlugin'
$startup = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup\Noble Sampler Audio Service.lnk'
Get-Process 'NobleSampler.AudioService' -ErrorAction SilentlyContinue | Stop-Process -Force
if (Test-Path $pluginTarget) { Remove-Item $pluginTarget -Recurse -Force }
if (Test-Path $startup) { Remove-Item $startup -Force }
Write-Host 'Noble Sampler has been uninstalled. Saved clips remain in LocalAppData\NobleSampler.'
