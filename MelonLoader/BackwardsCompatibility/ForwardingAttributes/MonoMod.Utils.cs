using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(IgnoresAccessChecksToAttribute))]
[assembly: TypeForwardedTo(typeof(MonoMod.ModInterop.ModExportNameAttribute))]
[assembly: TypeForwardedTo(typeof(MonoMod.ModInterop.ModImportNameAttribute))]
[assembly: TypeForwardedTo(typeof(MonoMod.ModInterop.ModInteropManager))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.DynData<>))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.Extensions))]
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.FastReflectionDelegate))]
#endif
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.FastReflectionHelper))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.GenericMethodInstantiationComparer))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.GenericTypeInstantiationComparer))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.LazyDisposable))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.LazyDisposable<>))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.DMDGenerator<>))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.DMDCecilGenerator))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.DMDEmitDynamicMethodGenerator))]
#if !NET6_0
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.DMDEmitMethodBuilderGenerator))]
#endif
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.DynamicMethodDefinition))]
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.DynamicMethodHelper))]
#endif
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.DynamicMethodReference))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.DynDll))]
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.DynDllImportAttribute))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.DynDllMapping))]
#endif
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.Relinker))]
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.GCListener))]
#endif
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.ICallSiteGenerator))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.MMReflectionImporter))]
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.Platform))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.PlatformHelper))]
#endif
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.ReflectionHelper))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.RelinkFailedException))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.RelinkTargetNotFoundException))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.WeakReferenceComparer))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.Cil.CecilILGenerator))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.Cil.ILGeneratorShim))]
[assembly: TypeForwardedTo(typeof(MonoMod.Utils.Cil.ILGeneratorShimExt))]
[assembly: TypeForwardedTo(typeof(MonoMod.Cil.ILContext))]
[assembly: TypeForwardedTo(typeof(MonoMod.Cil.MoveType))]
[assembly: TypeForwardedTo(typeof(MonoMod.Cil.SearchTarget))]
[assembly: TypeForwardedTo(typeof(MonoMod.Cil.ILCursor))]
[assembly: TypeForwardedTo(typeof(MonoMod.Cil.ILLabel))]
[assembly: TypeForwardedTo(typeof(MonoMod.Cil.ILPatternMatchingExt))]
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.Cil.IILReferenceBag))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.Cil.NopILReferenceBag))]
#endif
#if !ANDROID // not in the reorganized MonoMod/HarmonyX used on Android
[assembly: TypeForwardedTo(typeof(MonoMod.Cil.RuntimeILReferenceBag))]
#endif
