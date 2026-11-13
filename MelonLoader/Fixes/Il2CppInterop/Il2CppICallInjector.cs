#if NET6_0_OR_GREATER

using MelonLoader.NativeUtils;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.HarmonySupport;
using HarmonyLib;
using MelonLoader.Utils;
using Mono.Cecil;
using MonoMod.Cil;
using MethodAttributes = System.Reflection.MethodAttributes;
using MethodImplAttributes = System.Reflection.MethodImplAttributes;

#pragma warning disable 0649

namespace MelonLoader.Fixes.Il2CppInterop
{
    internal static class Il2CppICallInjector
    {
        private static Dictionary<string, (object, DynamicMethodDefinition, MethodInfo, IntPtr)> _lookup = new();
#if ANDROID
        // Delegates whose native wrappers were handed to il2cpp; they must never be collected
        private static readonly List<Delegate> _keepAlive = new();
#endif

        private delegate IntPtr dil2cpp_resolve_icall(IntPtr signature);
        private static NativeHook<dil2cpp_resolve_icall> il2cpp_resolve_icall_hook;

        private delegate void dil2cpp_add_internal_call(IntPtr signature, IntPtr funcPtr);
        private static dil2cpp_add_internal_call il2cpp_add_internal_call;

        private static Type _il2CppDetourMethodPatcher;
        private static MethodInfo _generateNativeToManagedTrampoline;

        private static bool _extendedDebug;

        internal static unsafe void Install()
        {
            try
            {
                _il2CppDetourMethodPatcher = typeof(HarmonySupport).Assembly.GetType("Il2CppInterop.HarmonySupport.Il2CppDetourMethodPatcher");
                if (_il2CppDetourMethodPatcher == null)
                    throw new Exception("Failed to get Il2CppDetourMethodPatcher");

                _generateNativeToManagedTrampoline = _il2CppDetourMethodPatcher.GetMethod("GenerateNativeToManagedTrampoline", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_generateNativeToManagedTrampoline == null)
                    throw new Exception("Failed to get Il2CppDetourMethodPatcher.GenerateNativeToManagedTrampoline");

                NativeLibrary gameAssemblyLib = NativeLibrary.Load(MelonEnvironment.Il2CppGameAssemblyPath);
                if (gameAssemblyLib == null)
                    throw new Exception($"Failed to load {MelonEnvironment.Il2CppGameAssemblyPath}");

                IntPtr il2cpp_resolve_icall = gameAssemblyLib.GetExport(nameof(il2cpp_resolve_icall));
                if (il2cpp_resolve_icall == IntPtr.Zero)
                    throw new Exception($"Failed to get {nameof(il2cpp_resolve_icall)} Native Export");

                il2cpp_add_internal_call = gameAssemblyLib.GetExport<dil2cpp_add_internal_call>(nameof(il2cpp_add_internal_call));
                if (il2cpp_add_internal_call == null)
                    throw new Exception($"Failed to get {nameof(il2cpp_add_internal_call)} Native Export");

                MelonDebug.Msg("Patching il2cpp_resolve_icall...");
                IntPtr detourPtr = Marshal.GetFunctionPointerForDelegate((dil2cpp_resolve_icall)il2cpp_resolve_icall_Detour);
                il2cpp_resolve_icall_hook = new NativeHook<dil2cpp_resolve_icall>(il2cpp_resolve_icall, detourPtr);
                il2cpp_resolve_icall_hook.Attach();
            }
            catch (Exception e)
            {
                MelonLogger.Error(e);
            }
        }

        internal static void Shutdown()
        {
            if (il2cpp_resolve_icall_hook != null)
            {
                if (il2cpp_resolve_icall_hook.IsHooked)
                    il2cpp_resolve_icall_hook.Detach();
                il2cpp_resolve_icall_hook = null;
            }

            if (_lookup != null)
            {
                if (_lookup.Count > 0)
                    _lookup.Clear();
                _lookup = null;
            }
        }

        private static IntPtr il2cpp_resolve_icall_Detour(IntPtr signature)
        {
            // Convert Pointer to String
            string signatureStr = Marshal.PtrToStringAnsi(signature);
            if (string.IsNullOrEmpty(signatureStr))
                return IntPtr.Zero;

            // Check Cache
            if (_lookup.TryGetValue(signatureStr, out var result))
            {
                if (_extendedDebug)
                    MelonDebug.Msg($"Resolved {signatureStr} to ICall in Cache");
                return result.Item4;
            }

            // Run Original
            IntPtr originalResult = il2cpp_resolve_icall_hook.Trampoline(signature);
            if (originalResult != IntPtr.Zero)
            {
                // Cache Original Result
                if (_extendedDebug)
                    MelonDebug.Msg($"Resolved {signatureStr} to Unity ICall");
                _lookup[signatureStr] = (null, null, null, originalResult);
                return originalResult;
            }

            // Check if Injection is Needed
            if (!ShouldInject(ref signatureStr, out MethodInfo unityShimMethod))
            {
                if (_extendedDebug)
                    MelonDebug.Error($"Unable to find suitable method to inject for {signatureStr}");
                return IntPtr.Zero;
            }

            // Create Injected Function and Cache Return
            MelonDebug.Msg($"Generating Trampoline for {signatureStr}");
            var pair = GenerateTrampoline(unityShimMethod);
            if (pair.Item4 == IntPtr.Zero)
            {
                if (_extendedDebug)
                    MelonDebug.Error($"Failed to generate trampoline for {signatureStr}");
                return IntPtr.Zero;
            }

            // Add New ICall to Il2Cpp Domain
            _lookup[signatureStr] = pair;
            il2cpp_add_internal_call(signature, pair.Item4);
            MelonLogger.Msg($"Registered mono icall {signatureStr} in il2cpp domain");

            // Return New Function Pointer
            return pair.Item4;
        }

        private static Type FindType(string typeFullName)
        {
            if (string.IsNullOrEmpty(typeFullName))
                return null;

            Type result = null;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a == null)
                    continue;

                result = a.GetValidType($"Il2Cpp.{typeFullName}");
                if (result == null)
                    result = a.GetValidType($"Il2Cpp{typeFullName}");
                if (result == null)
                    result = a.GetValidType(typeFullName);

                if (result != null)
                    break;
            }

            return result;
        }

        private static bool ShouldInject(ref string requestedSignature, out MethodInfo unityShimMethod)
        {
            unityShimMethod = null;

            // Split the Signature
            string[] split = requestedSignature.Split("::");
            string typeName = split[0];
            string methodDesc = split[1];
            if (methodDesc.EndsWith("()"))
                methodDesc = methodDesc.Substring(0, methodDesc.Length - 2);

            // Find Managed Type
            Type newType = FindType(typeName);
            if (newType == null)
                return false;

            // Find Managed Method
            MethodInfo targetMethod = null;
            MethodInfo[] allMethods = newType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            foreach (var method in allMethods)
            {
                try
                {
                    // Validate Method
                    if (method == null)
                        continue;

                    // Check for Generic Method since ICalls can't be Generic
                    if (method.IsGenericMethod)
                        continue;

                    // Check for PInvoke to prevent Recursion
                    if (method.Attributes.HasFlag(MethodAttributes.PinvokeImpl))
                        continue;

                    // Check for Extern to prevent Recursion
                    var methodImpl = method.GetMethodImplementationFlags();
                    if (methodImpl.HasFlag(MethodImplAttributes.InternalCall)
                        || methodImpl.HasFlag(MethodImplAttributes.Native)
                        || methodImpl.HasFlag(MethodImplAttributes.Unmanaged))
                        continue;

                    // Check if Method has no Body
                    if (!method.HasMethodBody())
                        continue;

                    // Validate the Method Signature
                    string methodSignature = GetMethodSignature(method);
                    if ((method.Name != methodDesc)
                        && (methodSignature != methodDesc))
                        continue;

                    DynamicMethodDefinition dynmethod = method.ToNewDynamicMethodDefinition();
                    ILContext ilcontext = new(dynmethod.Definition);
                    ILCursor ilcursor = new(ilcontext);

                    // Ignore methods that are just shims for Exception throws
                    if ((ilcursor.Instrs.Count == 2)
                        && (ilcursor.Instrs[1].OpCode.Code == Mono.Cecil.Cil.Code.Throw))
                    {
                        ilcontext.Dispose();
                        dynmethod.Dispose();
                        continue;
                    }

                    // Temporarily ignore methods that use ReadOnlySpan.GetPinnableReference
                    // ReadOnlySpan.GetPinnableReference throws MissingMethodException
                    bool IsValid = true;
                    foreach (var inst in ilcursor.Instrs)
                    {
                        if (((inst.OpCode.Code == Mono.Cecil.Cil.Code.Call) ||
                             (inst.OpCode.Code == Mono.Cecil.Cil.Code.Callvirt))
                            && inst.Operand is MethodReference methodRef
                            && methodRef.Name.Contains("GetPinnableReference")
                            && methodRef.DeclaringType.Name.Contains("ReadOnlySpan"))
                        {
                            IsValid = false;
                            break;
                        }
                    }

                    if (!IsValid)
                    {
                        ilcontext.Dispose();
                        dynmethod.Dispose();
                        continue;
                    }

                    // Found Shim
                    unityShimMethod = method;
                    requestedSignature = $"{typeName}::{methodSignature}";
                    ilcontext.Dispose();
                    dynmethod.Dispose();
                    return true;
                }
                catch {  }
            }
            
            return false;
        }

        private static (object, DynamicMethodDefinition, MethodInfo, IntPtr) GenerateTrampoline(MethodInfo unityShimMethod)
        {
            // Create Patcher
            object patcher = Activator.CreateInstance(_il2CppDetourMethodPatcher, [ unityShimMethod ]);
            if (patcher == null)
                return (null, null, null, IntPtr.Zero);

            // Create New Injected ICall Method
            DynamicMethodDefinition trampoline = (DynamicMethodDefinition)_generateNativeToManagedTrampoline.Invoke(patcher, [ unityShimMethod ]);
            if (trampoline == null)
                return (null, null, null, IntPtr.Zero);
           
            // Return the New Method
#if ANDROID
            MethodInfo newMethod = trampoline.Generate();
            // il2cpp calls this from native code, so it needs a native entry point that switches the thread into
            // managed mode (a reverse P/Invoke wrapper). The method's raw code address skips that, which the Mono
            // runtime used on Android aborts on ("Cannot transition thread ... from STATE_BLOCKING"). This is the
            // same approach Il2CppInterop's Harmony support uses.
            // Use Il2CppInterop's own delegate type for this icall when there is one: Il2CppInterop turns the pointer
            // back into a delegate with GetDelegateForFunctionPointer<ThatType>, and Mono hands back the original
            // delegate for its own wrappers, so the types must match.
            Delegate nativeCallable = CreateIl2CppInteropICallDelegate(unityShimMethod, newMethod);
            if (nativeCallable != null)
            {
                _keepAlive.Add(nativeCallable);
                return (patcher, trampoline, newMethod, Marshal.GetFunctionPointerForDelegate(nativeCallable));
            }

            // No known delegate type (e.g. the caller declares its own). Put a native jump stub in front of the
            // wrapper so Mono doesn't recognise the pointer and hands every caller a delegate of its own type.
            var factoryType = DelegateTypeFactory.instance.CreateDelegateType(newMethod, CallingConvention.Cdecl);
            var genericCallable = newMethod.CreateDelegate(factoryType);
            _keepAlive.Add(genericCallable);
            IntPtr wrapper = Marshal.GetFunctionPointerForDelegate(genericCallable);
            IntPtr thunk = MelonLoader.InternalUtils.BootstrapInterop.Library.CreateNativeThunk(wrapper);
            return (patcher, trampoline, newMethod, thunk != IntPtr.Zero ? thunk : wrapper);
#else
            MethodInfo newMethod = trampoline.Generate().Pin();
            return (patcher, trampoline, newMethod, newMethod.GetNativeStart());
#endif
        }
        
#if ANDROID
        // Il2CppInterop generates a nested "<MethodName>Delegate" in the declaring type for every icall it resolves.
        // The trampoline has the shape of an il2cpp method, which carries an extra trailing MethodInfo* argument
        // that icalls don't have; when that's the only difference, an adapter with the icall's exact signature
        // forwards to the trampoline with a null MethodInfo*.
        private static Delegate CreateIl2CppInteropICallDelegate(MethodInfo unityShimMethod, MethodInfo trampoline)
        {
            Type owner = unityShimMethod.DeclaringType;
            if (owner == null)
                return null;

            Type[] trampolineParams = trampoline.GetParameters().Select(p => p.ParameterType).ToArray();
            Type match = null;
            bool needsAdapter = false;
            bool passMethodInfo = false;
            foreach (Type nested in owner.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!typeof(Delegate).IsAssignableFrom(nested)
                    || !nested.Name.StartsWith(unityShimMethod.Name, StringComparison.Ordinal))
                    continue;

                MethodInfo invoke = nested.GetMethod("Invoke");
                if (invoke == null || !SameNativeShape(invoke.ReturnType, trampoline.ReturnType))
                    continue;

                Type[] invokeParams = invoke.GetParameters().Select(p => p.ParameterType).ToArray();
                bool exact = invoke.ReturnType == trampoline.ReturnType && invokeParams.SequenceEqual(trampolineParams);
                bool sameArgs = invokeParams.Length == trampolineParams.Length
                                && invokeParams.Zip(trampolineParams).All(t => SameNativeShape(t.First, t.Second));
                bool withoutMethodInfo = trampolineParams.Length == invokeParams.Length + 1
                                         && IsPointerLike(trampolineParams[^1])
                                         && invokeParams.Zip(trampolineParams).All(t => SameNativeShape(t.First, t.Second));
                if (!exact && !sameArgs && !withoutMethodInfo)
                    continue;

                bool preferred = nested.Name == unityShimMethod.Name + "Delegate";
                if (match == null || preferred)
                {
                    match = nested;
                    needsAdapter = !exact;
                    passMethodInfo = withoutMethodInfo;
                }
                if (preferred)
                    break;
            }

            if (match == null)
                return null;

            if (!needsAdapter)
                return trampoline.CreateDelegate(match);

            // The adapter is bound to a delegate of the trampoline (its first argument), which works whatever kind of
            // method MonoMod generated
            Type trampolineDelegateType = DelegateTypeFactory.instance.CreateDelegateType(trampoline, CallingConvention.Cdecl);
            Delegate trampolineDelegate = trampoline.CreateDelegate(trampolineDelegateType);
            MethodInfo trampolineInvoke = trampolineDelegateType.GetMethod("Invoke");

            Type[] adapterParams = match.GetMethod("Invoke").GetParameters().Select(p => p.ParameterType).ToArray();
            var adapter = new System.Reflection.Emit.DynamicMethod($"ICallAdapter_{unityShimMethod.Name}", match.GetMethod("Invoke").ReturnType,
                new[] { trampolineDelegateType }.Concat(adapterParams).ToArray(), typeof(Il2CppICallInjector).Module, true);
            var il = adapter.GetILGenerator();
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            // Arguments are passed through as-is: they only differ in how they're typed (IntPtr vs T&, bool vs byte)
            for (int i = 0; i < adapterParams.Length; i++)
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg, i + 1);
            if (passMethodInfo)
            {
                il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_0);
                il.Emit(System.Reflection.Emit.OpCodes.Conv_I);
            }
            il.Emit(System.Reflection.Emit.OpCodes.Callvirt, trampolineInvoke);
            il.Emit(System.Reflection.Emit.OpCodes.Ret);
            _keepAlive.Add(trampolineDelegate);
            return adapter.CreateDelegate(match, trampolineDelegate);
        }

        private static bool IsPointerLike(Type t)
            => t == typeof(IntPtr) || t == typeof(UIntPtr) || t.IsPointer || t.IsByRef;

        // Whether two types are passed the same way in native code
        private static bool SameNativeShape(Type a, Type b)
        {
            if (a == b)
                return true;
            if (IsPointerLike(a) && IsPointerLike(b))
                return true;
            if (a.IsEnum)
                a = Enum.GetUnderlyingType(a);
            if (b.IsEnum)
                b = Enum.GetUnderlyingType(b);
            if (a == b)
                return true;
            bool OneByte(Type t) => t == typeof(bool) || t == typeof(byte) || t == typeof(sbyte);
            return OneByte(a) && OneByte(b);
        }
#endif

        private static string GetMethodSignature(MethodBase method)
        {
            var parameters = method.GetParameters();
            if (parameters.Length <= 0)
                return method.Name;
            
            string str = parameters.Join((p => $"{p.ParameterType.FullDescription().Replace("Il2Cpp.", string.Empty).Replace("Il2Cpp", string.Empty)}"), ",");
            return ($"{method.Name}({str})");
        }
    }
}

#endif
