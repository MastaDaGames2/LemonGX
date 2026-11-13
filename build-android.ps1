<#
.SYNOPSIS
    Builds MelonLoader for Android (LemonLoader) and packages it as melon_data.zip for the LemonLoader installer.

.DESCRIPTION
    1. Builds LemonLoader's ARM64 forks of MonoMod, HarmonyX and Il2CppInterop (External/ submodules)
    2. Builds the bootstrap as libmain.so with NativeAOT + the Android NDK
    3. Builds MelonLoader's managed assemblies for Android
    4. Packages MelonLoader/, dotnet/ and native/ into Output/<Configuration>/android/melon_data.zip

    The .NET runtime (dotnet/) and OpenSSL (native/libssl.so, native/libcrypto.so) are taken from a LemonLoader
    melon_data.zip, because the runtime carries LemonLoader's garbage collector patch. By default the 0.6.5.1
    release's melon_data.zip is downloaded from the LemonLoader GitHub releases.

.PARAMETER NdkPath
    Android NDK folder (r26 or newer). Defaults to $env:ANDROID_NDK_ROOT.

.PARAMETER RuntimeZip
    A LemonLoader melon_data.zip to take dotnet/ and the OpenSSL libraries from. Downloaded if omitted.

.EXAMPLE
    ./build-android.ps1 -NdkPath C:\Android\android-ndk-r27c
#>
param(
    [string]$NdkPath = $env:ANDROID_NDK_ROOT,
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [string]$RuntimeZip,
    [string]$RuntimeZipUrl = 'https://github.com/LemonLoader/MelonLoader/releases/download/0.6.5.1/melon_data.zip',
    [switch]$SkipForks
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$BuildDir = Join-Path $Root '.android-build'
$Rid = 'linux-bionic-arm64'
New-Item -ItemType Directory -Force $BuildDir | Out-Null

function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

function Invoke-Checked {
    param([string]$Exe, [string[]]$Arguments, [string]$WorkingDirectory = $Root)
    Push-Location $WorkingDirectory
    # Windows PowerShell turns a native tool's stderr output into terminating errors under 'Stop'
    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Exe @Arguments 2>&1 | ForEach-Object { "$_" }
        $ErrorActionPreference = $oldPreference
        if ($LASTEXITCODE) { throw "'$Exe $($Arguments -join ' ')' failed with exit code $LASTEXITCODE" }
    }
    finally {
        $ErrorActionPreference = $oldPreference
        Pop-Location
    }
}

# --- Prerequisites ---------------------------------------------------------------------------------------------

Step 'Checking prerequisites'

if (-not $NdkPath -or -not (Test-Path (Join-Path $NdkPath 'source.properties'))) {
    throw 'The Android NDK is required. Pass -NdkPath or set ANDROID_NDK_ROOT (https://developer.android.com/ndk/downloads).'
}
$NdkPath = (Resolve-Path $NdkPath).Path
$hostTag = if ($IsLinux) { 'linux-x86_64' } elseif ($IsMacOS) { 'darwin-x86_64' } else { 'windows-x86_64' }
$NdkBin = Join-Path $NdkPath "toolchains/llvm/prebuilt/$hostTag/bin"
if (-not (Test-Path $NdkBin)) { throw "NDK toolchain not found at $NdkBin" }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'The .NET SDK (10 or newer) is required.' }

if (-not (Test-Path (Join-Path $Root 'External/MonoMod/src')) -or -not (Test-Path (Join-Path $Root 'External/MonoMod/external/iced/src'))) {
    Invoke-Checked git @('submodule', 'update', '--init', '--recursive')
}

# LemonLoader's MonoMod fork pins a .NET 8.0.3xx SDK in its global.json
$Dotnet8 = Join-Path $BuildDir 'dotnet8'
$Dotnet8Exe = Join-Path $Dotnet8 ($(if ($IsLinux -or $IsMacOS) { 'dotnet' } else { 'dotnet.exe' }))
if (-not $SkipForks -and -not (Test-Path $Dotnet8Exe)) {
    Step 'Installing .NET SDK 8.0.319 for the MonoMod fork (local to .android-build)'
    if ($IsLinux -or $IsMacOS) {
        $script = Join-Path $BuildDir 'dotnet-install.sh'
        Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.sh' -OutFile $script
        Invoke-Checked bash @($script, '--version', '8.0.319', '--install-dir', $Dotnet8, '--no-path')
    }
    else {
        $script = Join-Path $BuildDir 'dotnet-install.ps1'
        Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1' -OutFile $script
        & $script -Version 8.0.319 -InstallDir $Dotnet8 -NoPath
    }
}

# --- LemonLoader forks -----------------------------------------------------------------------------------------

if (-not $SkipForks) {
    $oldRoot = $env:DOTNET_ROOT
    $env:DOTNET_ROOT = $Dotnet8
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    try {
        Step 'Building the MonoMod fork'
        Invoke-Checked $Dotnet8Exe @('build', 'src/MonoMod.RuntimeDetour/MonoMod.RuntimeDetour.csproj', '-c', 'Release', '-v', 'q', '-nologo') (Join-Path $Root 'External/MonoMod')

        Step 'Building the HarmonyX fork'
        Invoke-Checked $Dotnet8Exe @('build', 'Harmony/Harmony.csproj', '-c', 'Release', '-f', 'netstandard2.0', '-v', 'q', '-nologo') (Join-Path $Root 'External/HarmonyX')

        Step 'Building the Il2CppInterop fork'
        foreach ($p in 'Il2CppInterop.HarmonySupport', 'Il2CppInterop.Generator') {
            Invoke-Checked $Dotnet8Exe @('build', "$p/$p.csproj", '-c', 'Release', '-v', 'q', '-nologo') (Join-Path $Root 'External/Il2CppInterop')
        }
    }
    finally {
        $env:DOTNET_ROOT = $oldRoot
    }
}

# --- MelonLoader -----------------------------------------------------------------------------------------------

$OutDir = Join-Path $Root "Output/$Configuration/$Rid"
if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }

$common = @("-c", $Configuration, "-p:ForceRID=$Rid", "-p:Platform=arm64", "-p:SolutionDir=$Root$([IO.Path]::DirectorySeparatorChar)", '-v', 'q', '-nologo')

Step 'Building the bootstrap (libmain.so)'
$oldPath = $env:PATH
$env:ANDROID_NDK_ROOT = $NdkPath
$env:PATH = "$NdkBin$([IO.Path]::PathSeparator)$env:PATH"
try {
    Invoke-Checked dotnet (@('publish', 'MelonLoader.Bootstrap/MelonLoader.Bootstrap.csproj') + $common)
}
finally {
    $env:PATH = $oldPath
}

Step 'Building the managed assemblies'
$projects = @(
    'MelonLoader.NativeHost/MelonLoader.NativeHost.csproj',
    'Dependencies/SupportModules/Il2Cpp/Il2Cpp.csproj',
    'Dependencies/Il2CppAssemblyGenerator/Il2CppAssemblyGenerator.csproj',
    'Dependencies/CompatibilityLayers/Stress_Level_Zero_Il2Cpp/Stress_Level_Zero_Il2Cpp.csproj',
    'UnityUtilities/UnityEngine.Il2CppAssetBundleManager/UnityEngine.Il2CppAssetBundleManager.csproj',
    'UnityUtilities/UnityEngine.Il2CppImageConversionManager/UnityEngine.Il2CppImageConversionManager.csproj'
)
foreach ($p in $projects) {
    Invoke-Checked dotnet (@('build', $p) + $common)
}

# --- Package ---------------------------------------------------------------------------------------------------

Step 'Packaging melon_data.zip'

if (-not $RuntimeZip) {
    $RuntimeZip = Join-Path $BuildDir 'lemon-runtime-melon_data.zip'
    if (-not (Test-Path $RuntimeZip)) {
        Write-Host "Downloading the LemonLoader .NET runtime from $RuntimeZipUrl"
        Invoke-WebRequest -UseBasicParsing $RuntimeZipUrl -OutFile $RuntimeZip
    }
}

$PackageDir = Join-Path $Root "Output/$Configuration/android"
$Staging = Join-Path $PackageDir 'melon_data'
if (Test-Path $PackageDir) { Remove-Item -Recurse -Force $PackageDir }
New-Item -ItemType Directory -Force (Join-Path $Staging 'native') | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem
$runtime = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $RuntimeZip).Path)
try {
    foreach ($entry in $runtime.Entries) {
        $take = $entry.FullName.StartsWith('dotnet/') -or $entry.FullName -in @('native/libssl.so', 'native/libcrypto.so')
        if (-not $take -or $entry.FullName.EndsWith('/')) { continue }
        $target = Join-Path $Staging $entry.FullName
        New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
    }
}
finally { $runtime.Dispose() }

if (-not (Test-Path (Join-Path $Staging 'dotnet/host/fxr'))) { throw "$RuntimeZip has no dotnet/ runtime" }

Copy-Item -Recurse (Join-Path $OutDir 'MelonLoader') (Join-Path $Staging 'MelonLoader')
Copy-Item (Join-Path $OutDir 'libmain.so') (Join-Path $Staging 'native/libmain.so')

# Desktop-Mono-only files that the bootstrap project always copies
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue (Join-Path $Staging 'MelonLoader/Dependencies/NetStandardPatches'), (Join-Path $Staging 'MelonLoader/Dependencies/MonoBleedingEdgePatches')

$zip = Join-Path $PackageDir 'melon_data.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($Staging, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)

Write-Host "`nDone: $zip" -ForegroundColor Green
Write-Host 'Use it with the LemonLoader installer (custom melon_data), or inject its folders into an APK:'
Write-Host '  MelonLoader/ and dotnet/ -> assets/, native/*.so -> lib/arm64-v8a/ (replacing libmain.so)'
