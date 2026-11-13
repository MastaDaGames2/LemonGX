#if ANDROID
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MelonLoader.Bootstrap.Proxy.Android;

/// <summary>
/// Creates tiny native jump stubs ("ldr x16, #8; br x16; .quad target"). Handing native code a stub instead of a
/// Mono delegate wrapper keeps Mono from recognising the pointer as its own: GetDelegateForFunctionPointer then
/// builds a fresh delegate of whatever type the caller asks for, instead of returning the original delegate (which
/// fails with InvalidCastException when the types differ).
/// </summary>
internal static unsafe partial class NativeThunks
{
    private const int PageSize = 4096;
    private const int ThunkSize = 16;

    private const int ProtRead = 1, ProtWrite = 2, ProtExec = 4;
    private const int MapPrivate = 0x02, MapAnonymous = 0x20;

    [LibraryImport("libc", EntryPoint = "mmap")]
    private static partial nint Mmap(nint addr, nuint length, int prot, int flags, int fd, nint offset);

    // Statically linked (from Dobby / compiler-rt): cleans the data cache and invalidates the instruction cache
    [LibraryImport("*", EntryPoint = "__clear_cache")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void ClearCache(nint start, nint end);

    private static readonly object Lock = new();
    private static nint _page;
    private static int _used = PageSize;

    public static nint Create(nint target)
    {
        if (target == 0)
            return 0;

        lock (Lock)
        {
            if (_used + ThunkSize > PageSize)
            {
                // Read-write-execute so new stubs can be added while existing ones on the page are in use
                var page = Mmap(0, PageSize, ProtRead | ProtWrite | ProtExec, MapPrivate | MapAnonymous, -1, 0);
                if (page == -1 || page == 0)
                {
                    Core.Logger.Error("Failed to allocate memory for native thunks");
                    return 0;
                }

                _page = page;
                _used = 0;
            }

            var thunk = _page + _used;
            var code = (uint*)thunk;
            code[0] = 0x58000050; // ldr x16, #8
            code[1] = 0xD61F0200; // br x16
            *(nint*)(thunk + 8) = target;
            ClearCache(thunk, thunk + ThunkSize);

            _used += ThunkSize;
            return thunk;
        }
    }
}
#endif
