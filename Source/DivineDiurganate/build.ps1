param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

Remove-Item Env:\MSBuildSdksPath -ErrorAction SilentlyContinue

Write-Host "Building DivineDiurganate ($Configuration)..."
dotnet build "DivineDiurganate.sln" -c $Configuration
