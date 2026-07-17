$ErrorActionPreference = 'Stop'
Push-Location "$PSScriptRoot/NobleSampler.AudioService"
dotnet restore
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$PSScriptRoot/publish"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
Pop-Location
Write-Host "Published to $PSScriptRoot/publish"
