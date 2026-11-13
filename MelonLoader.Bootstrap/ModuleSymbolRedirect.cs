using MelonLoader.Bootstrap.RuntimeHandlers.Il2Cpp;
using MelonLoader.Bootstrap.RuntimeHandlers.Mono;
using MelonLoader.Bootstrap.Utils;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MelonLoader.Bootstrap
{
    internal static partial class ModuleSymbolRedirect
    {
        private static bool _runtimeInitialised;

#if LINUX || OSX || ANDROID
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
#endif
#if WINDOWS
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
#endif
        private delegate nint DetourFn(nint handle, nint symbol);
        private static readonly DetourFn DetourDelegate = SymbolDetour;

        internal static void Attach()
        {
            MelonDebug.Log("Attaching Symbol Redirect...");

            IntPtr detourPtr = Marshal.GetFunctionPointerForDelegate(DetourDelegate);

#if ANDROID
            // libunity.so isn't loaded yet; AndroidProxy calls AttachToUnity once it is, before Unity starts
            _androidAttachPending = true;
            return;
#elif OSX
            // plthook cannot patch UnityPlayer.dylib's GOT on modern macOS
            // (LC_DYLD_CHAINED_FIXUPS) -- it reports "no such function" and the
            // mono hook never installs. Interpose dlsym in native code instead
            // (osxentry.cpp), the same DYLD interpose mechanism used to trigger
            // Init via setrlimit.
            RegisterDlsymHook(Marshal.GetFunctionPointerForDelegate(DlsymDetourDelegate));
#elif LINUX
            PltHook.InstallHooks
            ([
                ("dlsym", detourPtr)
            ]);
#endif
#if WINDOWS
            PltHook.InstallHooks
            ([
                ("GetProcAddress", detourPtr)
            ]);
#endif

            MelonDebug.Log("Symbol Redirect Attached!");
        }

#if ANDROID
        private static bool _androidAttachPending;

        /// <summary>
        /// Redirects libunity.so's dlsym lookups of the il2cpp API, the same way MelonLoader does on desktop Linux.
        /// No code inside il2cpp is patched, which matters on Unity 6.3: its il2cpp exports are often only a few
        /// instructions long, too small to inline-hook without corrupting their neighbours.
        /// </summary>
        internal static void AttachToUnity(nint libUnityHandle)
        {
            if (!_androidAttachPending)
                return;
            _androidAttachPending = false;

            if (NativeLibrary.TryGetExport(libUnityHandle, "JNI_OnLoad", out var addressInUnity)
                && Proxy.Android.AndroidPltHook.Replace(addressInUnity, "dlsym", Marshal.GetFunctionPointerForDelegate(DetourDelegate)))
            {
                Core.Logger.Msg("Redirecting libunity.so's il2cpp lookups");
                Proxy.Android.BootLog.Write("Redirecting libunity.so's il2cpp lookups (no il2cpp code is patched)");
                return;
            }

            Core.Logger.Warning("Could not redirect libunity.so's dlsym; hooking il2cpp directly instead");
            Il2CppHandler.AttachAndroidHooks();
        }
#endif

        internal static nint GetSymbol(nint handle, string symbolName)
            => GetSymbol(handle, Marshal.StringToHGlobalAnsi(symbolName));
        private static nint GetSymbol(nint handle, nint symbolName)
        {
#if ANDROID
            // libunity also looks things up with RTLD_DEFAULT (0), which must keep working
            if (symbolName == nint.Zero)
                return nint.Zero;
#else
            if ((handle == nint.Zero)
                || (symbolName == nint.Zero))
                return nint.Zero;
#endif

#if WINDOWS
            return GetProcAddress(handle, symbolName);
#elif LINUX
            return dlsym(handle, symbolName);
#elif OSX
            // Use the real (un-interposed) dlsym so MelonLoader resolves the
            // genuine runtime exports, not its own dlsym detours.
            return RealDlsym(handle, symbolName);
#elif ANDROID
            return LibcNative.DlsymRaw(handle, symbolName);
#else
            return nint.Zero;
#endif
        }

        private static nint SymbolDetour(nint handle, nint symbol)
        {
            nint originalSymbolAddress = GetSymbol(handle, symbol);

            string? symbolName = Marshal.PtrToStringAnsi(symbol);
            if (string.IsNullOrEmpty(symbolName)
                || string.IsNullOrWhiteSpace(symbolName))
                return originalSymbolAddress;

            //MelonDebug.Log($"Looking for Symbol {symbolName}");
            if (!MonoHandler.SymbolRedirects.TryGetValue(symbolName, out var redirect)
                && !Il2CppHandler.SymbolRedirects.TryGetValue(symbolName, out redirect))
                return originalSymbolAddress;

            if (!_runtimeInitialised)
            {
                _runtimeInitialised = true;
                MelonDebug.Log("Initializing Runtime");
                redirect.InitMethod(handle);
                if (!LoaderConfig.Current.Loader.CapturePlayerLogs)
                    ConsoleHandler.ResetHandles();
            }

            MelonDebug.Log($"Redirecting {symbolName}");
            return redirect.detourPtr;
        }

#if OSX
        // macOS interpose detour. osxentry.cpp resolves the real address and
        // passes it in (calling dlsym from managed code re-enters the interpose
        // and recurses), so this only decides whether to redirect.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate nint DlsymDetourFn(nint handle, nint symbol, nint real);
        private static readonly DlsymDetourFn DlsymDetourDelegate = DlsymDetour;

        private static nint DlsymDetour(nint handle, nint symbol, nint real)
        {
            string? symbolName = Marshal.PtrToStringAnsi(symbol);
            if (string.IsNullOrEmpty(symbolName))
                return real;

            if (!MonoHandler.SymbolRedirects.TryGetValue(symbolName, out var redirect)
                && !Il2CppHandler.SymbolRedirects.TryGetValue(symbolName, out redirect))
                return real;

            if (!_runtimeInitialised)
            {
                _runtimeInitialised = true;
                MelonDebug.Log("Initializing Runtime");
                redirect.InitMethod(handle);
                if (!LoaderConfig.Current.Loader.CapturePlayerLogs)
                    ConsoleHandler.ResetHandles();
            }

            MelonDebug.Log($"Redirecting {symbolName}");
            return redirect.detourPtr;
        }
#endif

#if WINDOWS
        [DllImport("kernel32")]
        private static extern nint GetProcAddress(nint handle, nint symbol);
#elif LINUX
        [DllImport("libdl.so.2")]
        private static extern IntPtr dlsym(nint handle, nint symbol);
#elif OSX
        // Native (osxentry.cpp): RealDlsym is the un-interposed dlsym used by
        // GetSymbol; RegisterDlsymHook installs the managed interpose target.
        [LibraryImport("*", EntryPoint = "MLRealDlsym")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        private static partial nint RealDlsym(nint handle, nint symbol);

        [LibraryImport("*", EntryPoint = "MLRegisterDlsymHook")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        private static partial void RegisterDlsymHook(nint detour);
#endif
    }
}