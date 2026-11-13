#if ANDROID
using MelonLoader.Bootstrap.Proxy.Android;
using MelonLoader.Bootstrap.Utils;
using System.Runtime.InteropServices;

namespace MelonLoader.Bootstrap.RuntimeHandlers.Il2Cpp;

/// <summary>
/// .NET for linux-bionic runs on the Mono VM, shipped as libcoreclr.so. LemonLoader's build of it adds
/// <c>mono_melonloader_*</c> exports so its garbage collector can be told not to suspend threads owned by il2cpp.
/// </summary>
internal class ClrMonoLib
{
    public required nint Handle { get; init; }

    public SetThreadCheckerFn? SetThreadChecker { get; private init; }
    public ActionFn? ThreadSuspendReload { get; private init; }
    public InstallUnhandledExceptionHookFn? InstallUnhandledExceptionHook { get; private init; }
    public PrintUnhandledExceptionFn? PrintUnhandledException { get; private init; }
    public SetStringFn? TraceSetLevelString { get; private init; }
    public SetStringFn? TraceSetMaskString { get; private init; }

    public static ClrMonoLib? TryLoad()
    {
        var runtimesDir = Path.Combine(AndroidBootstrap.DotnetDir, "shared", "Microsoft.NETCore.App");
        if (!Directory.Exists(runtimesDir))
            return null;

        // Prefer the newest runtime if more than one is present
        var corePath = Directory.GetDirectories(runtimesDir)
            .OrderByDescending(d => Version.TryParse(Path.GetFileName(d).Split('-')[0], out var v) ? v : new Version())
            .Select(d => Path.Combine(d, "libcoreclr.so"))
            .FirstOrDefault(File.Exists);

        if (corePath == null || !NativeLibrary.TryLoad(corePath, out var hRuntime))
            return null;

        NativeFunc.GetExport<SetThreadCheckerFn>(hRuntime, "mono_melonloader_set_thread_checker", out var setThreadChecker);
        NativeFunc.GetExport<ActionFn>(hRuntime, "mono_melonloader_thread_suspend_reload", out var threadSuspendReload);
        NativeFunc.GetExport<InstallUnhandledExceptionHookFn>(hRuntime, "mono_install_unhandled_exception_hook", out var installUnhandledExceptionHook);
        NativeFunc.GetExport<PrintUnhandledExceptionFn>(hRuntime, "mono_print_unhandled_exception", out var printUnhandledException);
        NativeFunc.GetExport<SetStringFn>(hRuntime, "mono_trace_set_level_string", out var setLevelString);
        NativeFunc.GetExport<SetStringFn>(hRuntime, "mono_trace_set_mask_string", out var setMaskString);

        return new()
        {
            Handle = hRuntime,
            SetThreadChecker = setThreadChecker,
            ThreadSuspendReload = threadSuspendReload,
            InstallUnhandledExceptionHook = installUnhandledExceptionHook,
            PrintUnhandledException = printUnhandledException,
            TraceSetLevelString = setLevelString,
            TraceSetMaskString = setMaskString
        };
    }

    // Returns non-zero if the GC may suspend the thread
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int CheckThreadFn(ulong threadId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void UnhandledExceptionFn(nint exc, nint userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SetThreadCheckerFn(nint checker);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ActionFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void InstallUnhandledExceptionHookFn(nint callback, nint userData);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void PrintUnhandledExceptionFn(nint exc);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SetStringFn([MarshalAs(UnmanagedType.LPUTF8Str)] string str);
}
#endif
