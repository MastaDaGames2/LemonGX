#if ANDROID
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MelonLoader.Bootstrap.Proxy.Android;

/// <summary>
/// Replaces a library's imported function by rewriting its GOT entry (plthook), without patching any code.
/// </summary>
internal static partial class AndroidPltHook
{
    [LibraryImport("*", EntryPoint = "plthook_open_by_handle")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int PlthookOpenByHandle(out nint plthook, nint handle);

    [LibraryImport("*", EntryPoint = "plthook_replace", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int PlthookReplace(nint plthook, string funcName, nint funcAddr, out nint oldFunc);

    [LibraryImport("*", EntryPoint = "plthook_close")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void PlthookClose(nint plthook);

    [LibraryImport("*", EntryPoint = "plthook_error")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial nint PlthookError();

    public static bool Replace(nint libraryHandle, string function, nint replacement)
    {
        if (PlthookOpenByHandle(out var plthook, libraryHandle) != 0)
        {
            Core.Logger.Error($"plthook_open_by_handle failed: {Marshal.PtrToStringUTF8(PlthookError())}");
            return false;
        }

        try
        {
            if (PlthookReplace(plthook, function, replacement, out _) != 0)
            {
                Core.Logger.Error($"plthook_replace({function}) failed: {Marshal.PtrToStringUTF8(PlthookError())}");
                return false;
            }

            return true;
        }
        finally
        {
            PlthookClose(plthook);
        }
    }
}
#endif
