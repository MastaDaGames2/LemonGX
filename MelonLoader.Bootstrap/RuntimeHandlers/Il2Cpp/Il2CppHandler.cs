using MelonLoader.Bootstrap.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MelonLoader.Bootstrap.RuntimeHandlers.Dotnet;

namespace MelonLoader.Bootstrap.RuntimeHandlers.Il2Cpp;

internal static class Il2CppHandler
{
    private static Il2CppLib il2cpp = null!;
    private static bool il2cppInitDone;
    private static bool invokeStarted;

    private static readonly Il2CppLib.InitFn Il2CPPInitDetourFn = InitDetour;
    private static readonly Il2CppLib.RuntimeInvokeFn InvokeDetourFn = InvokeDetour;
#if ANDROID
    private static readonly Il2CppLib.ThreadAttachFn ThreadAttachDetourFn = ThreadAttachDetour;
    private static readonly Il2CppLib.ThreadDetachFn ThreadDetachDetourFn = ThreadDetachDetour;
#endif
    internal static readonly Dictionary<string, (Action<nint> InitMethod, IntPtr detourPtr)> SymbolRedirects = new()
    {
        { "il2cpp_init", (Initialize, Marshal.GetFunctionPointerForDelegate(Il2CPPInitDetourFn))},
        { "il2cpp_runtime_invoke", (Initialize, Marshal.GetFunctionPointerForDelegate(InvokeDetourFn))},
#if ANDROID
        // Only to learn which threads belong to il2cpp (see MonoCheckThread)
        { "il2cpp_thread_attach", (Initialize, Marshal.GetFunctionPointerForDelegate(ThreadAttachDetourFn))},
        { "il2cpp_thread_detach", (Initialize, Marshal.GetFunctionPointerForDelegate(ThreadDetachDetourFn))},
#endif
    };

    public static void Initialize(nint handle)
    {
        var il2cppLib = Il2CppLib.TryLoad(handle);
        if (il2cppLib is null)
        {
            Core.Logger.Error("Could not load il2cpp");
            return;
        }

        il2cpp = il2cppLib;
    }

    internal static nint InitDetour(nint a)
    {
        if (il2cppInitDone)
            return il2cpp.Init(a);

        ConsoleHandler.ResetHandles();
        MelonDebug.Log("In init detour");

        var domain = il2cpp.Init(a);

#if ANDROID
        // il2cpp_init attached this thread, so it can be used to learn where il2cpp keeps native thread ids
        FindThreadIdOffset();
        RecordIl2CppThread();
#endif

        DotnetHandler.Initialize();
        il2cppInitDone = true;

        return domain;
    }

    internal static nint InvokeDetour(nint method, nint obj, nint args, nint exc)
    {
#if ANDROID
        RecordIl2CppThread();
#endif
        var result = il2cpp.RuntimeInvoke(method, obj, args, exc);
        if (invokeStarted)
            return result;

        var name = il2cpp.GetMethodName(method);
        if (name == null || !name.Contains("Internal_ActiveSceneChanged"))
            return result;

        invokeStarted = true;
        MelonDebug.Log("Invoke hijacked");

#if ANDROID
        // Threads il2cpp created since startup must be re-checked before the GC can suspend them
        DotnetHandler.MonoLib?.ThreadSuspendReload?.Invoke();
#endif

        DotnetHandler.Start();

        return result;
    }

#if ANDROID
    /// <summary>
    /// On Android libil2cpp.so is loaded before libunity and its exports are detoured in place, which works no matter
    /// how libunity resolves them.
    /// </summary>
    internal static bool AttachAndroidHooks()
    {
        if (!NativeLibrary.TryLoad("libil2cpp.so", out var handle))
        {
            Core.Logger.Error("Could not load libil2cpp.so. Only il2cpp games are supported on Android");
            return false;
        }

        Initialize(handle);
        if (il2cpp == null)
            return false;

        try
        {
            var initPtr = NativeLibrary.GetExport(handle, "il2cpp_init");
            var invokePtr = NativeLibrary.GetExport(handle, "il2cpp_runtime_invoke");

            var initOriginal = Dobby.HookAttach(initPtr, Marshal.GetFunctionPointerForDelegate(Il2CPPInitDetourFn));
            il2cpp.Init = Marshal.GetDelegateForFunctionPointer<Il2CppLib.InitFn>(initOriginal);

            var invokeOriginal = Dobby.HookAttach(invokePtr, Marshal.GetFunctionPointerForDelegate(InvokeDetourFn));
            il2cpp.RuntimeInvoke = Marshal.GetDelegateForFunctionPointer<Il2CppLib.RuntimeInvokeFn>(invokeOriginal);
        }
        catch (Exception ex)
        {
            Core.Logger.Error($"Failed to hook il2cpp: {ex}");
            return false;
        }

        MelonDebug.Log("Hooked il2cpp_init and il2cpp_runtime_invoke");
        return true;
    }

    private static int threadIdOffset = -1;

    // Il2CppThread { Il2CppObject obj (2 pointers); Il2CppInternalThread* internal_thread; ... }
    private const int InternalThreadFieldOffset = 2 * 8;

    // Il2CppInternalThread's layout changes between Unity versions, so instead of hardcoding where its `tid`
    // (the pthread_t of the thread) lives, find the field holding the current thread's pthread_t.
    private static unsafe void FindThreadIdOffset()
    {
        if (il2cpp.ThreadCurrent == null)
            return;

        try
        {
            var thread = il2cpp.ThreadCurrent();
            if (thread == 0)
                return;

            var internalThread = *(nint*)(thread + InternalThreadFieldOffset);
            if (internalThread == 0)
                return;

            var self = (ulong)LibcNative.PthreadSelf();
            for (var offset = 2 * 8; offset < 0x100; offset += 8)
            {
                if (*(ulong*)(internalThread + offset) != self)
                    continue;

                threadIdOffset = offset;
                MelonDebug.Log($"Il2CppInternalThread tid offset: 0x{offset:X}");
                return;
            }
        }
        catch (Exception ex)
        {
            Core.Logger.Warning($"Could not locate il2cpp thread ids: {ex.Message}");
        }

        Core.Logger.Warning("Could not locate il2cpp thread ids; the GC may suspend il2cpp threads");
    }

    // Native ids (pthread_t) of threads attached to il2cpp, for runtimes whose il2cpp can't list them
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, byte> Il2CppThreads = new();

    [ThreadStatic]
    private static bool _threadRecorded;

    private static void RecordIl2CppThread()
    {
        if (_threadRecorded)
            return;
        _threadRecorded = true;
        Il2CppThreads.TryAdd((ulong)LibcNative.PthreadSelf(), 0);
    }

    private static nint ThreadAttachDetour(nint domain)
    {
        var thread = il2cpp.ThreadAttach!(domain);
        Il2CppThreads.TryAdd((ulong)LibcNative.PthreadSelf(), 0);
        return thread;
    }

    private static void ThreadDetachDetour(nint thread)
    {
        Il2CppThreads.TryRemove((ulong)LibcNative.PthreadSelf(), out _);
        _threadRecorded = false;
        il2cpp.ThreadDetach!(thread);
    }

    /// <summary>
    /// Called by the Mono GC before it suspends a thread. Threads attached to il2cpp must keep running, or both
    /// runtimes can deadlock. Returns 0 to skip the thread.
    /// </summary>
    internal static unsafe int MonoCheckThread(ulong threadId)
    {
        if (Il2CppThreads.ContainsKey(threadId))
            return 0;

        // Unity 6.3+ no longer exports il2cpp_thread_get_all_attached_threads; the threads recorded above cover it
        if (threadIdOffset < 0 || il2cpp.GetAllAttachedThreads == null)
            return 1;

        var threads = il2cpp.GetAllAttachedThreads(out var size);
        if (threads == 0)
            return 1;

        for (nuint i = 0; i < size; i++)
        {
            var thread = ((nint*)threads)[i];
            if (thread == 0)
                continue;

            var internalThread = *(nint*)(thread + InternalThreadFieldOffset);
            if (internalThread == 0)
                continue;

            if (*(ulong*)(internalThread + threadIdOffset) == threadId)
                return 0;
        }

        return 1;
    }
#endif
}
