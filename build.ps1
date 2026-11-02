$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'release-v1.0'
dotnet publish "$PSScriptRoot\TVController.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $output
$exe = Join-Path $output 'CaaM.exe'
Write-Host "Fertig: $exe"
