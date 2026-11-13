#if ANDROID
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using MelonLoader.Java;
using MelonLoader.Bootstrap.Utils;

namespace MelonLoader.Bootstrap.Proxy.Android;

/// <summary>
/// The bootstrap is shipped as the game's <c>libmain.so</c>. Unity's Java side loads libmain.so and then calls
/// <c>com.unity3d.player.NativeLoader.load(String)</c>, which the real libmain implements by loading libunity.so
/// and calling its JNI_OnLoad. We register our own implementation of those natives so we can start MelonLoader
/// right before Unity is loaded.
/// </summary>
public static class AndroidProxy
{
    private const int AndroidLogInfo = 4;
    private const int AndroidLogError = 6;

    [DllImport("liblog", EntryPoint = "__android_log_write")]
    private static extern int AndroidLogWrite(int prio, [MarshalAs(UnmanagedType.LPUTF8Str)] string tag, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    public static void Log(string text) => AndroidLogWrite(AndroidLogInfo, "MelonLoader", text);
    public static void LogError(string text) => AndroidLogWrite(AndroidLogError, "MelonLoader", text);

    private static bool _unityLoaded;

    [UnmanagedCallersOnly(EntryPoint = "JNI_OnLoad")]
    [RequiresDynamicCode("Starts the bootstrap, which calls InitConfig")]
    public static unsafe int JNI_OnLoad(IntPtr vm, void* reserved)
    {
        try
        {
            JNI.Initialize(vm);

            var nativeLoader = JNI.TryFindClass("com/unity3d/player/NativeLoader");
            if (nativeLoader == null)
            {
                // No NativeLoader means the game doesn't boot Unity through libmain.so's natives (e.g. a custom
                // launcher that loads us directly). Start right away; libunity will be loaded by the game itself.
                Log("NativeLoader class not found, starting MelonLoader from JNI_OnLoad");
                AndroidBootstrap.Start();
                return (int)JNI.Version.V1_6;
            }

            var methods = stackalloc JNINativeMethod[2];
            methods[0] = new JNINativeMethod
            {
                Name = Utf8StringMarshaller.ConvertToUnmanaged("load"),
                Signature = Utf8StringMarshaller.ConvertToUnmanaged("(Ljava/lang/String;)Z"),
                FnPtr = (delegate* unmanaged[Cdecl]<nint, nint, nint, byte>)&Load
            };
            methods[1] = new JNINativeMethod
            {
                Name = Utf8StringMarshaller.ConvertToUnmanaged("unload"),
                Signature = Utf8StringMarshaller.ConvertToUnmanaged("()Z"),
                FnPtr = (delegate* unmanaged[Cdecl]<nint, nint, byte>)&Unload
            };

            if (JNI.Env->Functions->RegisterNatives(JNI.Env, nativeLoader.Handle, (IntPtr)methods, 2) != 0)
            {
                if (JNI.ExceptionCheck())
                    JNI.ExceptionClear();

                LogError("Failed to register NativeLoader natives, starting MelonLoader from JNI_OnLoad");
                AndroidBootstrap.Start();
            }
        }
        catch (Exception ex)
        {
            LogError($"JNI_OnLoad failed: {ex}");
        }

        return (int)JNI.Version.V1_6;
    }

    // NativeLoader.load(String nativeLibraryDir)
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    [RequiresDynamicCode("Starts the bootstrap, which calls InitConfig")]
    private static byte Load(nint env, nint clazz, nint nativeLibraryDir)
    {
        try
        {
            // Must happen before libunity runs, so our il2cpp hooks are in place when Unity calls il2cpp_init
            AndroidBootstrap.Start();
        }
        catch (Exception ex)
        {
            LogError($"Failed to start MelonLoader: {ex}");
        }

        return LoadUnity() ? (byte)1 : (byte)0;
    }

    // NativeLoader.unload()
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte Unload(nint env, nint clazz)
    {
        return 1;
    }

    private static unsafe bool LoadUnity()
    {
        if (_unityLoaded)
            return true;

        if (!NativeLibrary.TryLoad("libunity.so", out var libUnity))
        {
            LogError("Failed to load libunity.so");
            return false;
        }

        if (!NativeFunc.GetExport<JNI_OnLoadFunc>(libUnity, "JNI_OnLoad", out var jniOnLoad))
        {
            LogError("libunity.so has no JNI_OnLoad export");
            return false;
        }

        jniOnLoad((IntPtr)JNI.VM, IntPtr.Zero);
        _unityLoaded = true;
        return true;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int JNI_OnLoadFunc(IntPtr vm, IntPtr reserved);

    private unsafe struct JNINativeMethod
    {
        public byte* Name;
        public byte* Signature;
        public void* FnPtr;
    }
}
#endif
