$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$pluginSource = Join-Path $root 'plugin\com.noble.sampler.sdPlugin'
$serviceSource = Join-Path $root 'audio-service\publish'
$packagePath = Join-Path $root 'com.noble.sampler.streamDeckPlugin'
$stagingRoot = Join-Path $env:TEMP ('NobleSampler-package-' + [guid]::NewGuid().ToString('N'))
$stagedPlugin = Join-Path $stagingRoot 'com.noble.sampler.sdPlugin'
$zipPath = Join-Path $stagingRoot 'com.noble.sampler.zip'

try {
    if (-not (Test-Path (Join-Path $serviceSource 'NobleSampler.AudioService.exe'))) {
        Write-Host 'Publishing the Windows audio service...'
        & (Join-Path $root 'audio-service\publish.ps1')
        if ($LASTEXITCODE -ne 0) {
            throw "Audio service publish failed with exit code $LASTEXITCODE."
        }
    }

    if (-not (Test-Path (Join-Path $serviceSource 'NobleSampler.AudioService.exe'))) {
        throw 'Audio service publish did not produce NobleSampler.AudioService.exe.'
    }

    New-Item -ItemType Directory -Force -Path $stagedPlugin | Out-Null
    foreach ($item in @('manifest.json', 'bin', 'imgs', 'ui')) {
        Copy-Item (Join-Path $pluginSource $item) $stagedPlugin -Recurse -Force
    }
    Copy-Item $serviceSource (Join-Path $stagedPlugin 'service') -Recurse -Force

    Compress-Archive -Path $stagedPlugin -DestinationPath $zipPath -CompressionLevel Optimal
    Copy-Item $zipPath $packagePath -Force

    $sizeMb = [math]::Round((Get-Item $packagePath).Length / 1MB, 1)
    Write-Host "Created $packagePath ($sizeMb MB)" -ForegroundColor Green
}
finally {
    if (Test-Path $stagingRoot) {
        Remove-Item $stagingRoot -Recurse -Force
    }
}
