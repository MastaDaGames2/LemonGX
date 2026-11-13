#if ANDROID
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MelonLoader.Bootstrap.Proxy.Android;

/// <summary>
/// Replaces a library's imported function by rewriting its GOT entry (plthook), without patching any code.
/// </summary>
internal static partial class AndroidPltHook
{
    // plthook_open_by_handle relies on dlinfo(RTLD_DI_LINKMAP), which bionic doesn't support; open by an address
    // inside the library instead
    [LibraryImport("*", EntryPoint = "plthook_open_by_address")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int PlthookOpenByAddress(out nint plthook, nint address);

    [LibraryImport("*", EntryPoint = "plthook_replace", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int PlthookReplace(nint plthook, string funcName, nint funcAddr, out nint oldFunc);

    [LibraryImport("*", EntryPoint = "plthook_close")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void PlthookClose(nint plthook);

    [LibraryImport("*", EntryPoint = "plthook_error")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial nint PlthookError();

    /// <param name="addressInLibrary">Any address inside the library, e.g. one of its exports</param>
    public static bool Replace(nint addressInLibrary, string function, nint replacement)
    {
        if (PlthookOpenByAddress(out var plthook, addressInLibrary) != 0)
        {
            Core.Logger.Error($"plthook_open_by_address failed: {Marshal.PtrToStringUTF8(PlthookError())}");
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
