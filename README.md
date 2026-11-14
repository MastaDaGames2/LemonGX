<p align="center">
  <img src="docs/images/lemongx.png" width="200" alt="Lemon GX icon">
</p>

<h1 align="center">Lemon GX</h1>

<p align="center">
  Mod loader for Unity games on Android, including Unity 6.
</p>

<p align="center">
  <a href="https://github.com/MastaDaGames2/LemonGX-Installer/releases/latest"><img src="https://img.shields.io/github/v/release/MastaDaGames2/LemonGX-Installer?label=download&style=for-the-badge&color=yellow"></a>
</p>

---

Lemon GX is a fork of [LemonLoader](https://github.com/LemonLoader/MelonLoader), the Android port of
[MelonLoader](https://github.com/LavaGang/MelonLoader). LemonLoader is based on MelonLoader 0.6 and can't load
games made with newer Unity versions. Lemon GX moves the Android support to MelonLoader 0.7.4, so mods work on
current games too.

## Features

- **Newer Unity versions:** games built with Unity 2023.2 and Unity 6 (6000.x) work, as well as older ones.
- **MelonLoader 0.7.4:** mods made for current MelonLoader work.
- **Keeps the game's own Unity engine:** the installer no longer swaps in a downloaded `libunity.so`. This fixes
  crashes in games that ship a stripped engine.
- **Works offline:** the installer has everything it needs built in.

## Installing

1. Download **Lemon GX.apk** from the [latest release](https://github.com/MastaDaGames2/LemonGX-Installer/releases/latest)
   and install it.
2. Open Lemon GX, pick a game, and tap **Patch**.
3. When asked, uninstall the original game and install the patched copy. Your game data is removed along with
   the original, so back up any saves you care about first.
4. Start the game. The first launch asks for **All files access**, then takes a minute or two to set up while
   Lemon GX builds the game's interop files. Later launches are fast.

## Adding mods

Mods go in the game's own folder on your phone's storage:

```
/storage/emulated/0/MelonLoader/<game package name>/Mods/
```

That folder also contains `Plugins/`, `UserData/`, and the log at `MelonLoader/Latest.log`. If something goes
wrong, that log is the first place to look.

## Requirements and limitations

- 64-bit ARM phones (`arm64-v8a`) and **IL2CPP** Unity games. Mono games and 32-bit games aren't supported.
- Mods that use the old MonoMod detour API (`MonoMod.RuntimeDetour.Detour`, `NativeDetour`) don't load. Harmony
  patches work normally.
- Tested on: *My Dystopian Robot Girlfriend* (Unity 6000.3.8f1), Samsung Galaxy S25 Ultra, Android 17.
  Other games should work; please report the ones that don't.

## Building

See [docs/ANDROID.md](docs/ANDROID.md) for building the loader (`melon_data.zip`), and the
[Lemon GX Installer](https://github.com/MastaDaGames2/LemonGX-Installer) repo for the app.

## Credits

- [MelonLoader](https://github.com/LavaGang/MelonLoader) by LavaGang, the mod loader this is built on
- [LemonLoader](https://github.com/LemonLoader) for the original Android port, its .NET runtime, and the
  HarmonyX, MonoMod and Il2CppInterop forks
- [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) by Samboy, which reads Unity 6 games

## Licensing

Lemon GX, like MelonLoader, is licensed under the Apache License, Version 2.0. See [LICENSE.md](LICENSE.md).

Third-party libraries used as source code and/or bundled in binary form:
- [Dobby](https://github.com/LavaGang/Dobby) is licensed under the Apache License, Version 2.0. See [LICENSE](https://github.com/LavaGang/Dobby/blob/master/LICENSE) for the full License.
- [plthook](https://github.com/LavaGang/plthook) is licensed under the 2-Clause BSD License. See [LICENSE](https://github.com/LavaGang/plthook/blob/master/LICENSE.md) for the full License.
- [Mono](https://github.com/Unity-Technologies/mono) is licensed under multiple licenses. See [LICENSE](https://github.com/Unity-Technologies/mono/blob/unity-master/LICENSE) for full details.
- [HarmonyX](https://github.com/BepInEx/HarmonyX) is licensed under the MIT License. See [LICENSE](https://github.com/BepInEx/HarmonyX/blob/master/LICENSE) for the full License.
- [MonoMod](https://github.com/MonoMod/MonoMod) is licensed under the MIT License. See [LICENSE](https://github.com/MonoMod/MonoMod/blob/master/LICENSE) for the full License.
- [Mono.Cecil](https://github.com/jbevain/cecil) is licensed under the MIT License. See [LICENSE](https://github.com/jbevain/cecil/blob/master/LICENSE.txt) for the full License.
- [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) is licensed under the MIT License. See [LICENSE](https://github.com/JamesNK/Newtonsoft.Json/blob/master/LICENSE.md) for the full License.
- [TinyJSON](https://github.com/pbhogan/TinyJSON) is licensed under the MIT License. See [LICENSE](https://github.com/LavaGang/MelonLoader/blob/master/MelonLoader/TinyJSON/LICENSE.md) for the full License.
- [Tomlet](https://github.com/SamboyCoding/Tomlet) is licensed under the MIT License. See [LICENSE](https://github.com/SamboyCoding/Tomlet/blob/master/LICENSE) for the full License.
- [AsmResolver](https://github.com/Washi1337/AsmResolver) is licensed under the MIT License. See [LICENSE](https://github.com/Washi1337/AsmResolver/blob/master/LICENSE.md) for the full License.
- [SharpZipLib](https://github.com/icsharpcode/SharpZipLib) is licensed under the MIT License. See [LICENSE](MelonLoader/BackwardsCompatibility/ICSharpCode/SharpZipLib/LICENSE.txt) for the full License.
- [Semver](https://github.com/maxhauser/semver) is licensed under the MIT License. See [LICENSE](https://github.com/maxhauser/semver/blob/master/License.txt) for the full License.
- [Illusion Plugin Architecture](https://github.com/Eusth/IPA) is licensed under the MIT License. See [LICENSE](https://github.com/Eusth/IPA/blob/master/LICENSE) for the full License.
- [MuseDashModLoader](https://github.com/mo10/MuseDashModLoader) is licensed under the MIT License. See [LICENSE](https://github.com/mo10/MuseDashModLoader/blob/master/LICENSE) for the full License.
- [mgGif](https://github.com/gwaredd/mgGif) is licensed under the MIT License. See [LICENSE](https://github.com/gwaredd/mgGif/blob/main/LICENSE) for the full License.
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) is licensed under the MIT License. See [LICENSE](https://github.com/nesrak1/AssetsTools.NET/blob/master/LICENSE) for the full License.
- [AssetRipper.VersionUtilities](https://github.com/AssetRipper/VersionUtilities) is licensed under the MIT License. See [LICENSE](https://github.com/AssetRipper/VersionUtilities/blob/master/License.md) for the full License.
- Steam Library, VDF, and ACF Parsing from [SteamFinder.cs](https://github.com/Umbranoxio/BeatSaberModInstaller/blob/master/BeatSaberModManager/Dependencies/SteamFinder.cs) by [Umbranoxio](https://github.com/Umbranoxio) and [Dalet](https://github.com/Dalet).
- [bHapticsLib](https://github.com/HerpDerpinstine/bHapticsLib) is licensed under the MIT License. See [LICENSE](https://github.com/HerpDerpinstine/bHapticsLib/blob/master/LICENSE.md) for the full License.
- [IndexRange](https://github.com/bgrainger/IndexRange) is licensed under the MIT License. See [LICENSE](https://github.com/bgrainger/IndexRange/blob/master/LICENSE) for the full License.
- [ValueTupleBridge](https://github.com/OrangeCube/MinimumAsyncBridge) is licensed under the MIT License. See [LICENSE](https://github.com/OrangeCube/MinimumAsyncBridge/blob/master/LICENSE) for the full License.
- [WebSocketDotNet](https://github.com/SamboyCoding/WebSocketDotNet) is licensed under the MIT License. See [LICENSE](https://github.com/SamboyCoding/WebSocketDotNet/blob/master/LICENSE) for the full License.
- [Pastel](https://github.com/silkfire/Pastel) is licensed under the MIT License. See [LICENSE](https://github.com/silkfire/Pastel/blob/master/LICENSE) for the full License.
- [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop) is licensed under the LGPLv3 License. See [LICENSE](https://github.com/BepInEx/Il2CppInterop/blob/master/LICENSE) for the full License.

External libraries and tools that are used at runtime:
- [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) is licensed under the MIT License. See [LICENSE](https://github.com/SamboyCoding/Cpp2IL/blob/master/LICENSE) for the full License.
- Unity Runtime Libraries from [MelonLoader.UnityDependencies](https://github.com/LavaGang/MelonLoader.UnityDependencies) are part of Unity Software.
Their usage is subject to [Unity Terms of Service](https://unity3d.com/legal/terms-of-service), including [Unity Software Additional Terms](https://unity3d.com/legal/terms-of-service/software).
- [.NET Runtime](https://github.com/dotnet/runtime) is licensed under the MIT License. See [LICENSE](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) for the full License.

Lemon GX is not affiliated with or endorsed by LavaGang, LemonLoader, or Unity Technologies.
"Unity" is a trademark or a registered trademark of Unity Technologies or its affiliates in the U.S. and elsewhere.
