namespace MelonLoader.Bootstrap.Utils;

/// <summary>
/// Minimal MD5 (RFC 1321). System.Security.Cryptography needs OpenSSL on linux-bionic, which Android doesn't provide.
/// </summary>
internal static class Md5
{
    private static readonly int[] Shifts =
    [
        7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22,
        5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20,
        4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23,
        6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21
    ];

    private static readonly uint[] K = CreateK();

    private static uint[] CreateK()
    {
        var k = new uint[64];
        for (var i = 0; i < 64; i++)
            k[i] = (uint)(long)Math.Floor(Math.Abs(Math.Sin(i + 1)) * 4294967296.0);
        return k;
    }

    public static byte[] Hash(byte[] input)
    {
        var paddedLength = ((input.Length + 8) / 64 + 1) * 64;
        var message = new byte[paddedLength];
        Array.Copy(input, message, input.Length);
        message[input.Length] = 0x80;
        BitConverter.TryWriteBytes(message.AsSpan(paddedLength - 8), (ulong)input.Length * 8);

        uint a0 = 0x67452301, b0 = 0xefcdab89, c0 = 0x98badcfe, d0 = 0x10325476;
        var m = new uint[16];

        for (var chunk = 0; chunk < paddedLength; chunk += 64)
        {
            for (var i = 0; i < 16; i++)
                m[i] = BitConverter.ToUInt32(message, chunk + i * 4);

            uint a = a0, b = b0, c = c0, d = d0;
            for (var i = 0; i < 64; i++)
            {
                uint f;
                int g;
                if (i < 16) { f = (b & c) | (~b & d); g = i; }
                else if (i < 32) { f = (d & b) | (~d & c); g = (5 * i + 1) % 16; }
                else if (i < 48) { f = b ^ c ^ d; g = (3 * i + 5) % 16; }
                else { f = c ^ (b | ~d); g = (7 * i) % 16; }

                f = f + a + K[i] + m[g];
                a = d;
                d = c;
                c = b;
                b += (f << Shifts[i]) | (f >> (32 - Shifts[i]));
            }

            a0 += a; b0 += b; c0 += c; d0 += d;
        }

        var result = new byte[16];
        BitConverter.TryWriteBytes(result.AsSpan(0), a0);
        BitConverter.TryWriteBytes(result.AsSpan(4), b0);
        BitConverter.TryWriteBytes(result.AsSpan(8), c0);
        BitConverter.TryWriteBytes(result.AsSpan(12), d0);
        return result;
    }
}
