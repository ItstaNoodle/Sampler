$ErrorActionPreference = 'Stop'
Push-Location "$PSScriptRoot/NobleSampler.AudioService"
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$PSScriptRoot/publish"
Pop-Location
Write-Host "Published to $PSScriptRoot/publish"
