#if ANDROID
namespace MelonLoader.Bootstrap.Proxy.Android;

/// <summary>
/// A plain-text startup log in the Download folder (/storage/emulated/0/Download/LemonGX_[package].log), so startup
/// problems can be diagnosed without logcat. It's written before MelonLoader's own log exists, and is replaced on
/// every launch.
/// </summary>
internal static class BootLog
{
    private static readonly object Lock = new();
    private static string? _path;

    public static void Write(string text)
    {
        try
        {
            lock (Lock)
            {
                if (_path == null)
                {
                    var package = "unknown";
                    try
                    {
                        var cmdline = File.ReadAllText("/proc/self/cmdline");
                        var end = cmdline.IndexOfAny(['\0', ':']);
                        package = (end >= 0 ? cmdline[..end] : cmdline).Trim();
                    }
                    catch { }

                    _path = $"/storage/emulated/0/Download/LemonGX_{package}.log";
                    File.WriteAllText(_path, $"Lemon GX startup log ({DateTime.Now:yyyy-MM-dd HH:mm:ss})\n");
                }

                File.AppendAllText(_path, $"[{DateTime.Now:HH:mm:ss.fff}] {text}\n");
            }
        }
        catch
        {
            // No storage access yet (permission not granted); try again on the next message. Logcat has everything.
            _path = null;
        }
    }
}
#endif
