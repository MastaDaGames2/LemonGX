#if ANDROID
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MelonLoader.Bootstrap.Proxy.Android;

/// <summary>
/// Android throws away anything written to stdout/stderr. This pipes both into logcat, which also makes
/// Console output from the bootstrap and diagnostics from the .NET runtime visible there.
/// </summary>
internal static class StdRedirect
{
    private const int StdoutFileno = 1;
    private const int StderrFileno = 2;
    private const int AndroidLogDebug = 3;
    private const int AndroidLogWarn = 5;

    [DllImport("libc", SetLastError = true)]
    private static extern unsafe int pipe(int* pipefd);

    [DllImport("libc", SetLastError = true)]
    private static extern int dup2(int oldfd, int newfd);

    [DllImport("liblog", EntryPoint = "__android_log_write")]
    private static extern int AndroidLogWrite(int prio, [MarshalAs(UnmanagedType.LPUTF8Str)] string tag, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    public static void RedirectStdOut() => RedirectStream(StdoutFileno, AndroidLogDebug);

    public static void RedirectStdErr() => RedirectStream(StderrFileno, AndroidLogWarn);

    private static unsafe void RedirectStream(int fileno, int priority)
    {
        var fds = stackalloc int[2];
        if (pipe(fds) != 0)
        {
            AndroidProxy.LogError($"Failed to create a pipe for fd {fileno}");
            return;
        }

        if (dup2(fds[1], fileno) < 0)
        {
            AndroidProxy.LogError($"Failed to redirect fd {fileno}");
            return;
        }

        var readFd = fds[0];
        var thread = new Thread(() =>
        {
            using var stream = new FileStream(new SafeFileHandle(readFd, true), FileAccess.Read, 1);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
                AndroidLogWrite(priority, "MelonLoader", line);
        })
        {
            IsBackground = true,
            Name = $"MelonLoader fd{fileno} logcat"
        };
        thread.Start();
    }
}
#endif
