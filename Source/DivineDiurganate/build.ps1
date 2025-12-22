# Find the latest installed .NET SDK and build the project.
try {
    Write-Host "Finding latest .NET SDK..."
    $sdkList = dotnet --list-sdks
    $latestSdk = $sdkList.Split([Environment]::NewLine) | Select-Object -Last 1
    
    if ($null -eq $latestSdk -or $latestSdk -eq "") {
        Write-Host "No .NET SDKs found. Please install the .NET SDK."
        exit 1
    }

    $sdkVersion = ($latestSdk.Split(' '))[0]
    Write-Host "Using .NET SDK version: $sdkVersion"

    # Since the project is .NET Framework 4.8, msbuild is more suitable.
    # We need to find msbuild.exe location.
    # A common path for Visual Studio's MSBuild:
    $vsWherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if(Test-Path $vsWherePath) {
        $vsPath = & $vsWherePath -latest -property installationPath
        $msBuildPath = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"
        if(Test-Path $msBuildPath) {
            Write-Host "Found MSBuild at: $msBuildPath"
            & $msBuildPath "ArachnaeSwarm.sln" /t:Rebuild /p:Configuration=Debug
        } else {
             Write-Host "MSBuild.exe not found at the expected path. Falling back to dotnet build."
             dotnet build "ArachnaeSwarm.sln"
        }
    } else {
        Write-Host "vswhere.exe not found. Falling back to dotnet build."
        dotnet build "ArachnaeSwarm.sln"
    }

} catch {
    Write-Host "An error occurred during the build process."
    Write-Host $_
    exit 1
}
