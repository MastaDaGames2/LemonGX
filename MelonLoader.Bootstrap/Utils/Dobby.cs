using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MelonLoader.Bootstrap.Utils;

public static partial class Dobby
{
    [LibraryImport("*", EntryPoint = "DobbyHook")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int Hook(nint target, nint detour, ref nint original);

    [LibraryImport("*", EntryPoint = "DobbyDestroy")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int Destroy(nint target);

    public static nint HookAttach(nint target, nint detour)
    {
#if ANDROID
        target = ResolveBranchThunk(target);
#endif
        nint original = 0;
        if (Hook(target, detour, ref original) != 0)
        {
            throw new AccessViolationException($"Could not prepare patch to target {target:X}");
        }
        return original;
    }

#if ANDROID
    /// <summary>
    /// Newer Unity builds export il2cpp's API as a table of single-instruction stubs (<c>b real_function</c>) packed
    /// next to each other. Dobby overwrites 16 bytes at the target, which would clobber the neighbouring stubs, so hook
    /// the function the stub jumps to instead.
    /// </summary>
    internal static unsafe nint ResolveBranchThunk(nint address)
    {
        for (var i = 0; i < 4 && address != 0; i++)
        {
            var instruction = *(uint*)address;

            // B imm26 (unconditional branch, no link)
            if ((instruction & 0xFC000000) != 0x14000000)
                break;

            long offset = instruction & 0x03FFFFFF;
            if ((offset & 0x02000000) != 0)
                offset -= 0x04000000;

            address += (nint)(offset * 4);
        }

        return address;
    }
#endif

    public static void HookDetach(nint target)
    {
        var result = Destroy(target);
        if (result is not 0 and not -1)
        {
            throw new AccessViolationException($"Could not destroy patch for target {target:X}");
        }
    }
}