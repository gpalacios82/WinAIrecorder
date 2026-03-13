# VoiceType build script
# Requires .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0

param(
    [string]$Configuration = "Release",
    [switch]$Publish
)

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectDir

Write-Host "Building VoiceType..." -ForegroundColor Cyan

if ($Publish) {
    dotnet publish -c Release -r win-x64 --self-contained true
    Write-Host "Published to: $projectDir\bin\Release\net8.0-windows\win-x64\publish\" -ForegroundColor Green
} else {
    dotnet build -c $Configuration
}
