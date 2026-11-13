using System.Runtime.InteropServices;
using System.Text;
using MelonLoader.Bootstrap.Logging;
#if ANDROID
using MelonLoader.Bootstrap.Proxy.Android;
using MelonLoader.Bootstrap.RuntimeHandlers.Il2Cpp;
#endif

namespace MelonLoader.Bootstrap.RuntimeHandlers.Dotnet;

internal static class DotnetHandler
{
    private const string _rootKey = "DOTNET_ROOT";
    private static DotnetLib? _module;
    private static Action? _startFunc;

#if ANDROID
    internal static ClrMonoLib? MonoLib { get; private set; }

    // Kept alive for as long as native code may call them
    private static readonly ClrMonoLib.CheckThreadFn MonoCheckThreadDelegate = Il2CppHandler.MonoCheckThread;
    private static readonly ClrMonoLib.UnhandledExceptionFn MonoUnhandledExceptionDelegate = MonoUnhandledException;
#endif
    
    public static void Initialize()
    {
        var managedDir = Path.Combine(LoaderConfig.Current.Loader.BaseDirectory, "MelonLoader", "net6");
        var runtimeConfigPath = Path.Combine(managedDir, "MelonLoader.runtimeconfig.json");
        var nativeHostPath = Path.Combine(managedDir, "MelonLoader.NativeHost.dll");

        if (!File.Exists(runtimeConfigPath))
        {
            Core.Logger.Error($"Runtime config not found at: '{runtimeConfigPath}'");
            return;
        }

        if (!File.Exists(nativeHostPath))
        {
            Core.Logger.Error($"NativeHost not found at: '{runtimeConfigPath}'");
            return;
        }
        
#if ANDROID
        // The installer bundles a .NET runtime for linux-bionic in the APK, which AndroidBootstrap extracts.
        // MelonLoader targets net6 while that runtime is newer, so allow rolling forward to it.
        LibcNative.Setenv("DOTNET_ROLL_FORWARD", "Major", true);
        // Il2CppInterop and the ICall injector let il2cpp call straight into JIT-compiled managed code, without the
        // transition wrappers Mono's cooperative/hybrid thread-state tracking expects; that aborts with "Cannot
        // transition thread ... from STATE_BLOCKING". Preemptive suspend doesn't track those states.
        LibcNative.Setenv("MONO_THREADS_SUSPEND", "preemptive", true);
        if (LoaderConfig.Current.Loader.DebugMode)
        {
            LibcNative.Setenv("COREHOST_TRACE", "1", true);
            LibcNative.Setenv("COREHOST_TRACE_VERBOSITY", "3", true);
        }

        // The runtime's Mono VM installs its own SIGSEGV/SIGBUS handlers. Android's Java runtime and Unity rely on
        // handling some of those signals themselves, so Mono must pass on the ones that aren't its own. This has to
        // happen before the runtime starts, which is why the library is loaded ahead of hostfxr.
        MonoLib = ClrMonoLib.TryLoad();
        if (MonoLib?.SetSignalChaining != null)
        {
            MonoLib.SetSignalChaining(1);
            MonoLib.SetCrashChaining?.Invoke(1);
            Core.Logger.Msg("Enabled Mono signal chaining");
        }
        else
        {
            Core.Logger.Warning("Could not enable Mono signal chaining; crashes in game code may be reported as Mono crashes");
        }

        MelonDebug.Log($"Attempting to load hostfxr using .NET runtime from: {AndroidBootstrap.DotnetDir}");
        if (ScanDirectory(AndroidBootstrap.DotnetDir)
            && InitializeDomain(runtimeConfigPath, nativeHostPath))
            return;

        Core.Logger.Error($"Failed to load the .NET runtime from '{AndroidBootstrap.DotnetDir}'");
        return;
#else
        // Try to use a portable runtime from config
        string portableDir = LoaderConfig.Current.Loader.HostFXRPathOverride;
        if (!string.IsNullOrEmpty(portableDir)
            && !string.IsNullOrWhiteSpace(portableDir))
        {
            if (File.Exists(portableDir))
                portableDir = Path.GetDirectoryName( // dotnet
                    Path.GetDirectoryName( // host
                        Path.GetDirectoryName( // fxr
                            Path.GetDirectoryName( // 6.x.x
                                portableDir))))!;

            MelonDebug.Log($"Attempting to load hostfxr using .NET runtime from: {portableDir}");
            if (ScanDirectory(portableDir)
                && InitializeDomain(runtimeConfigPath, nativeHostPath))
                return;
        }

        // Try to use a portable runtime from game directory
        portableDir = Path.Combine(Exports.ProcessDirectory, "dotnet");
        MelonDebug.Log($"Attempting to load hostfxr using .NET runtime from: {portableDir}");
        if (ScanDirectory(portableDir)
            && InitializeDomain(runtimeConfigPath, nativeHostPath))
            return;

        string basePortableDir = Path.Combine(LoaderConfig.Current.Loader.BaseDirectory, "dotnet");
        if (portableDir != basePortableDir)
        {
            portableDir = basePortableDir;
            MelonDebug.Log($"Attempting to load hostfxr using .NET runtime from: {portableDir}");
            if (ScanDirectory(portableDir)
                && InitializeDomain(runtimeConfigPath, nativeHostPath))
                return;
        }
        
        // Try to use a portable runtime from repository
        portableDir = Path.Combine(LoaderConfig.Current.Loader.BaseDirectory, "MelonLoader", "Dependencies", "dotnet");
        MelonDebug.Log($"Attempting to load hostfxr using .NET runtime from: {portableDir}");
        if (ScanDirectory(portableDir)
            && InitializeDomain(runtimeConfigPath, nativeHostPath))
            return;

        // Try the normal system detection/installation
        MelonDebug.Log("Attempting to load hostfxr from system");
        if (DotnetLib.GetHostFxrSystemPath(out var path)
            && !string.IsNullOrEmpty(path)
            && ((_module = DotnetLib.TryLoad(path)) != null))
        {
            Core.Logger.Msg($"Using .NET runtime: '{path}'");
            if (InitializeDomain(runtimeConfigPath, nativeHostPath))
                return;
        }

#if WINDOWS
        // Try to install runtime then attempt system detection/installation again
        MelonDebug.Log("Attempting to reinstall .NET runtime and load hostfxr from system");
        if (DotnetInstaller.AttemptInstall()
            && DotnetLib.GetHostFxrSystemPath(out path)
            && !string.IsNullOrEmpty(path)
            && ((_module = DotnetLib.TryLoad(path)) != null))
        {
            Core.Logger.Msg($"Using .NET runtime: '{path}'");
            if (InitializeDomain(runtimeConfigPath, nativeHostPath))
                return;
        }
#endif
        
#if X64 && (WINDOWS || OSX || LINUX)
        // Try to download portable runtime from repository then attempt to use it again
        MelonDebug.Log($"Attempting to download .NET runtime from repository and load hostfxr from: {portableDir}");
        if (DotnetPortable.AttemptInstall()
            && ScanDirectory(portableDir)
            && InitializeDomain(runtimeConfigPath, nativeHostPath))
            return;
#endif
        
        // Failure
        Core.Logger.Error("Failed to load Hostfxr");
#endif
    }

    private static bool InitializeDomain(string runtimeConfigPath, string nativeHostPath)
    {
        MelonDebug.Log("Initializing domain");
        if (!_module!.InitializeForRuntimeConfig(runtimeConfigPath, out var context))
        {
            Core.Logger.Error($"Failed to initialize a .NET domain");
            return false;
        }

        MelonDebug.Log("Loading NativeHost assembly");
        var initialize = _module!.LoadAssemblyAndGetFunctionUco<InitializeFn>(context, nativeHostPath, "MelonLoader.NativeHost.NativeEntryPoint, MelonLoader.NativeHost", "NativeEntry");
        if (initialize == null)
        {
            Core.Logger.Error($"Failed to load assembly from: '{nativeHostPath}'");
            return false;
        }

        var startFuncPtr = Exports.LibraryHandle;

#if ANDROID
        ApplyMonoPatches();
#endif

        MelonDebug.Log("Invoking NativeHost entry");
        initialize(ref startFuncPtr);

        if (startFuncPtr == 0 || startFuncPtr == Exports.LibraryHandle)
        {
            Core.Logger.Error($"Managed did not return the initial function pointer");
            return false;
        }

        _startFunc = Marshal.GetDelegateForFunctionPointer<Action>(startFuncPtr);
        return true;
    }
    
#if ANDROID
    private static void ApplyMonoPatches()
    {
        MonoLib ??= ClrMonoLib.TryLoad();
        if (MonoLib == null)
        {
            Core.Logger.Warning("Could not load the .NET runtime library to apply Mono patches");
            return;
        }

        MonoLib.TraceSetLevelString?.Invoke("warning");
        MonoLib.TraceSetMaskString?.Invoke("all");

        MonoLib.InstallUnhandledExceptionHook?.Invoke(Marshal.GetFunctionPointerForDelegate(MonoUnhandledExceptionDelegate), 0);

        if (MonoLib.SetThreadChecker != null)
        {
            MonoLib.SetThreadChecker(Marshal.GetFunctionPointerForDelegate(MonoCheckThreadDelegate));
            MelonDebug.Log("Installed Mono GC thread checker");
        }
        else
        {
            Core.Logger.Warning("This .NET runtime has no LemonLoader GC patch; the game may freeze during garbage collection");
        }
    }

    private static void MonoUnhandledException(nint exc, nint userData)
    {
        if (exc != 0)
            MonoLib?.PrintUnhandledException?.Invoke(exc);
    }
#endif

    public static void Start()
    {
        _startFunc?.Invoke();
    }
    
    public static bool ScanDirectory(string dotnetPath)
    {
        string pathKey = "PATH";
        string? hostfxrPath = null;
        try
        {
            if (!Directory.Exists(dotnetPath))
                return false;
            if (!FindHostFxrInDirectory(dotnetPath, out hostfxrPath))
                return false;
        }
        catch (Exception ex)
        {
            if (LoaderConfig.Current.Loader.DebugMode)
                Core.Logger.Error(ex.ToString());
            return false;
        }

        string? originalRoot = null;
        string? originalPath = null;
        try
        {
            // Get Original Root and Path
            originalRoot = Environment.GetEnvironmentVariable(_rootKey);
            originalPath = Environment.GetEnvironmentVariable(pathKey);
        }
        catch (Exception ex)
        {
            if (LoaderConfig.Current.Loader.DebugMode)
                Core.Logger.Error(ex.ToString());
            return false;
        }

        try
        {
            // Temporarily Apply New Root
            Environment.SetEnvironmentVariable(_rootKey, dotnetPath);
#if ANDROID
            // Environment.SetEnvironmentVariable doesn't reach native getenv on Unix, which hostfxr uses
            LibcNative.Setenv(_rootKey, dotnetPath, true);
#endif

            // Temporarily Apply New Path
            string path = originalPath ?? string.Empty;
            var pathEntries = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (!pathEntries.Any(p => string.Equals(p, dotnetPath, StringComparison.OrdinalIgnoreCase)))
                Environment.SetEnvironmentVariable(pathKey, dotnetPath + Path.PathSeparator + path);
            
            // Attempt to load HostFXR
            if (((_module = DotnetLib.TryLoad(hostfxrPath!)) != null))
            {
                Core.Logger.Msg($"Using .NET runtime: '{dotnetPath}'");
                return true;
            }
        }
        catch (Exception ex)
        {
            if (LoaderConfig.Current.Loader.DebugMode)
                Core.Logger.Error(ex.ToString());
        }
        
        // Restore Original Root and Path
        try
        {
            Environment.SetEnvironmentVariable(_rootKey, originalRoot);
            Environment.SetEnvironmentVariable(pathKey, originalPath);
        }
        catch (Exception ex)
        {
            if (LoaderConfig.Current.Loader.DebugMode)
                Core.Logger.Error(ex.ToString());
        }

        return false;
    }

    private static bool FindHostFxrInDirectory(string? path, out string? hostfxrPath)
    {
        hostfxrPath = null;
        if (string.IsNullOrEmpty(path))
            return false;
        
        string filename = $"hostfxr.{Exports.LibExtension}";
#if !WINDOWS
        filename = $"lib{filename}";
#endif

        string[] foundFiles = Directory.GetFiles(path, filename, SearchOption.AllDirectories);
        hostfxrPath = foundFiles.FirstOrDefault();
        return (hostfxrPath != null);
    }
    
    // Requires the bootstrap handle to be passed first
    private delegate void InitializeFn(ref nint startFunc);
}
