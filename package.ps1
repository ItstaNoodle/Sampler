$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$pluginSource = Join-Path $root 'plugin\nobles.sampler.sdPlugin'
$serviceSource = Join-Path $root 'audio-service\publish'
$packagePath = Join-Path $root 'nobles.sampler.streamDeckPlugin'
$stagingRoot = Join-Path $env:TEMP ('NobleSampler-package-' + [guid]::NewGuid().ToString('N'))
$stagedPlugin = Join-Path $stagingRoot 'nobles.sampler.sdPlugin'

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
    foreach ($item in @('manifest.json', '.sdignore', 'bin', 'imgs', 'ui')) {
        Copy-Item (Join-Path $pluginSource $item) $stagedPlugin -Recurse -Force
    }
    Copy-Item $serviceSource (Join-Path $stagedPlugin 'service') -Recurse -Force

    $streamDeckCli = Get-Command 'streamdeck.cmd' -ErrorAction SilentlyContinue
    if (-not $streamDeckCli) {
        throw 'The Stream Deck CLI is required. Install it with: npm install -g @elgato/cli'
    }

    & $streamDeckCli.Source pack $stagedPlugin --output $stagingRoot --force --no-update-check
    if ($LASTEXITCODE -ne 0) {
        throw "Stream Deck package validation failed with exit code $LASTEXITCODE."
    }

    $packedPlugin = Get-ChildItem $stagingRoot -Filter '*.streamDeckPlugin' -File |
        Select-Object -First 1
    if (-not $packedPlugin) {
        throw 'Stream Deck CLI did not produce a .streamDeckPlugin file.'
    }
    Copy-Item $packedPlugin.FullName $packagePath -Force

    $sizeMb = [math]::Round((Get-Item $packagePath).Length / 1MB, 1)
    Write-Host "Created $packagePath ($sizeMb MB)" -ForegroundColor Green
}
finally {
    if (Test-Path $stagingRoot) {
        Remove-Item $stagingRoot -Recurse -Force
    }
}
