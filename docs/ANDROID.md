# Lemon GX: MelonLoader 0.7.4 on Android

This branch ports [LemonLoader](https://github.com/LemonLoader/MelonLoader)'s Android support onto MelonLoader 0.7.4,
so il2cpp Unity games on 64-bit ARM Android can be modded with the current MelonLoader, including games built
with Unity 2023.2 and Unity 6 (6000.x).

## What changed compared to LemonLoader 0.6

| Area | LemonLoader 0.6 | This port |
| --- | --- | --- |
| Bootstrap | Rust (`libBootstrap.so`) + Rust `libmain.so` proxy | MelonLoader 0.7.4's C# NativeAOT bootstrap, built for `linux-bionic-arm64` as a single `libmain.so` |
| Starting point | `NativeLoader.load` proxy loads the bootstrap, then libunity | Same idea: `JNI_OnLoad` registers `NativeLoader.load`, starts MelonLoader, then loads libunity |
| il2cpp hooks | Dobby on `il2cpp_init` | libunity's `dlsym` is redirected (plthook), so `il2cpp_init`, `il2cpp_runtime_invoke` and the thread attach/detach exports are swapped without patching code; Dobby is only a fallback |
| Mono GC thread fix | Hardcoded `Il2CppInternalThread` layout | Not needed: il2cpp code enters managed code through reverse P/Invoke wrappers, so Mono's default hybrid suspend handles it (the old checker can still be enabled with `LEMONGX_MONO_THREAD_CHECKER=1`) |
| Cpp2IL | 2022.1.0-pre-release.16 (fails on Unity 2023.2 / 6000.0 metadata) | 2022.1.0-pre-release.21, run in-process |
| Il2CppInterop / HarmonyX / MonoMod | LemonLoader forks | Same forks, as submodules in `External/` |
| MelonLoader | 0.6.6 | 0.7.4 |

Everything Android-specific is behind `#if ANDROID` (or `IsAndroidBuild` in MSBuild), so desktop builds are unchanged.

## Building

Requirements:

- Windows, Linux or macOS with the .NET 10 SDK (or newer)
- [Android NDK](https://developer.android.com/ndk/downloads) r26 or newer
- Git (the forks are submodules: `git submodule update --init --recursive`)

```powershell
./build-android.ps1 -NdkPath C:\path\to\android-ndk-r27c
```

The script:

1. Installs a local .NET SDK 8.0.319 into `.android-build/` (the MonoMod fork pins that SDK) and builds the
   MonoMod, HarmonyX and Il2CppInterop forks
2. Builds `libmain.so` (NativeAOT, linked with the NDK)
3. Builds the managed assemblies with `-p:ForceRID=linux-bionic-arm64`
4. Writes `Output/Release/android/melon_data.zip`

The .NET runtime (`dotnet/`) and OpenSSL (`native/libssl.so`, `native/libcrypto.so`) come from LemonLoader's
0.6.5.1 `melon_data.zip`, which is downloaded automatically; that runtime contains LemonLoader's patch that keeps
the Mono garbage collector from suspending il2cpp threads. Pass `-RuntimeZip <path>` to use another one.

## Installing into a game

`melon_data.zip` has the same layout the LemonLoader installer uses:

```
MelonLoader/   -> APK assets/MelonLoader/
dotnet/        -> APK assets/dotnet/
native/*.so    -> APK lib/arm64-v8a/ (libmain.so replaces Unity's)
```

The [LemonLoader installer](https://github.com/LemonLoader/MelonLoaderInstaller) uses a local `melon_data.zip`
instead of downloading its own when it finds one here:

- Windows installer: `Desktop\melon_data\melon_data.zip`
- Android installer: `/sdcard/Download/melon_data.zip`

Put the built zip there and patch the game as usual. The installer also patches the manifest (storage permissions),
adds `assets/lemon_patch_date.txt`, and re-signs the APK. It keeps the game's own `libunity.so`. Uninstall the
original game first (the patched APK has a different signature), and remove any previously patched copy.

On first launch MelonLoader asks for "All files access" (Android 11+), extracts itself and the runtime, and generates
the il2cpp interop assemblies. Files live in `/storage/emulated/0/MelonLoader/<package>/` (`Mods/`, `Plugins/`,
`UserData/`, `MelonLoader/Latest.log`). Everything is also written to logcat under the `MelonLoader` tag:

```
adb logcat -s MelonLoader
```

## Known limitations

- Only 64-bit ARM (`arm64-v8a`) il2cpp games. No Mono games, no 32-bit ARM.
- Tested on a game using Unity 6000.3.8f1, with a mod loaded.
- LemonLoader's prebuilt .NET runtime is aligned for 4 KB pages; devices running with 16 KB pages won't load it.
- Mods compiled against legacy MonoMod types (`MonoMod.RuntimeDetour.Detour`, `NativeDetour`, ...) won't load,
  because the ARM64 fork is based on the reorganized MonoMod API. Harmony patches work as usual.
- The package blocklist from LemonLoader 0.6 is kept.
