using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.DetourConfig))]
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Detour))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Detour<>))]
#endif
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.DetourContext))]
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.DetourModManager))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.HarmonyDetourBridge))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.HookConfig))]
#endif
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Hook))]
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Hook<>))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Hook<,>))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.IDetour))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.ISortableDetour))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.ILHookConfig))]
#endif
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.ILHook))]
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.NativeDetourConfig))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.NativeDetour))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.DetourHelper))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.IDetourNativePlatform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.IDetourRuntimePlatform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.OnMethodCompiledEvent))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.NativeDetourData))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Platforms.DetourNativeARMPlatform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Platforms.DetourNativeLibcPlatform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Platforms.DetourNativeMonoPlatform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Platforms.DetourNativeMonoPosixPlatform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Platforms.DetourNativeWindowsPlatform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Platforms.DetourNativeX86Platform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Platforms.DetourRuntimeILPlatform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Platforms.DetourRuntimeMonoPlatform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Platforms.DetourRuntimeNET50Platform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Platforms.DetourRuntimeNET60Platform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Platforms.DetourRuntimeNETCore30Platform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Platforms.DetourRuntimeNETCorePlatform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.Platforms.DetourRuntimeNETPlatform))]
#endif
[assembly: TypeForwardedTo(typeof(MonoMod.RuntimeDetour.HookGen.HookEndpointManager))]
