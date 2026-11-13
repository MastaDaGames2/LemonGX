#if ANDROID
using System.Runtime.InteropServices;

namespace MelonLoader.Bootstrap.Proxy.Android;

/// <summary>Describes native addresses as "library+offset (symbol)", so crash addresses can be matched up later.</summary>
internal static class NativeInfo
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DlInfo
    {
        public nint FileName;
        public nint FileBase;
        public nint SymbolName;
        public nint SymbolAddress;
    }

    [DllImport("libdl", EntryPoint = "dladdr")]
    private static extern int Dladdr(nint address, out DlInfo info);

    public static string DescribeAddress(nint address)
    {
        if (address == 0)
            return "0x0";

        try
        {
            if (Dladdr(address, out var info) == 0 || info.FileName == 0)
                return $"0x{address:X}";

            var file = Path.GetFileName(Marshal.PtrToStringUTF8(info.FileName));
            var symbol = info.SymbolName != 0 ? $" ({Marshal.PtrToStringUTF8(info.SymbolName)})" : "";
            return $"{file}+0x{address - info.FileBase:X} [base 0x{info.FileBase:X}]{symbol}";
        }
        catch
        {
            return $"0x{address:X}";
        }
    }
}
#endif
