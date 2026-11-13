using MelonLoader.Bootstrap.Utils;
using System.Runtime.InteropServices;

namespace MelonLoader.Bootstrap.RuntimeHandlers.Il2Cpp;

internal class Il2CppLib(Il2CppLib.MethodGetNameFn methodGetName)
{
    public required nint Handle { get; init; }

    // Settable so they can be swapped for hook trampolines when the functions are detoured in place (Android)
    public required InitFn Init { get; set; }
    public required RuntimeInvokeFn RuntimeInvoke { get; set; }

#if ANDROID
    public GetAllAttachedThreadsFn? GetAllAttachedThreads { get; private init; }
    public ThreadCurrentFn? ThreadCurrent { get; private init; }
#endif

    public static Il2CppLib? TryLoad(nint hRuntime)
    {
        if (!NativeFunc.GetExport<InitFn>(hRuntime, "il2cpp_init", out var init)
            || !NativeFunc.GetExport<RuntimeInvokeFn>(hRuntime, "il2cpp_runtime_invoke", out var runtimeInvoke)
            || !NativeFunc.GetExport<MethodGetNameFn>(hRuntime, "il2cpp_method_get_name", out var methodGetName))
            return null;

#if ANDROID
        // Only needed for the Mono GC thread fix, so they're optional
        NativeFunc.GetExport<GetAllAttachedThreadsFn>(hRuntime, "il2cpp_thread_get_all_attached_threads", out var getAllAttachedThreads);
        NativeFunc.GetExport<ThreadCurrentFn>(hRuntime, "il2cpp_thread_current", out var threadCurrent);
#endif

        return new(methodGetName)
        {
            Handle = hRuntime,
            Init = init,
            RuntimeInvoke = runtimeInvoke,
#if ANDROID
            GetAllAttachedThreads = getAllAttachedThreads,
            ThreadCurrent = threadCurrent,
#endif
        };
    }

    public string? GetMethodName(nint method)
    {
        return method == 0 ? null : Marshal.PtrToStringAnsi(methodGetName(method));
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate nint InitFn(nint a);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate nint RuntimeInvokeFn(nint method, nint obj, nint args, nint exc);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate nint MethodGetNameFn(nint method);
#if ANDROID
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate nint GetAllAttachedThreadsFn(out nuint size);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate nint ThreadCurrentFn();
#endif
}
