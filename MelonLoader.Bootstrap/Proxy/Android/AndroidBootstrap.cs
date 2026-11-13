#if ANDROID
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using MelonLoader.Java;
using MelonLoader.Utils;
using MelonLoader.Bootstrap.Utils;

namespace MelonLoader.Bootstrap.Proxy.Android;

public static class AndroidBootstrap
{
    /// <summary>The package name of the running game, e.g. com.company.game</summary>
    public static string PackageName { get; private set; } = string.Empty;

    /// <summary>MelonLoader's base directory on shared storage: /storage/emulated/0/MelonLoader/[package]</summary>
    public static string BaseDir { get; private set; } = string.Empty;

    /// <summary>The app's private data directory, where native code can be loaded from: /data/data/[package]</summary>
    public static string InternalDataDir { get; private set; } = string.Empty;

    /// <summary>The .NET runtime copied out of the APK: /data/data/[package]/dotnet</summary>
    public static string DotnetDir { get; private set; } = string.Empty;

    private static bool _started;

    [RequiresDynamicCode("Calls Core.Init")]
    public static void Start()
    {
        if (_started)
            return;
        _started = true;

        // linux-bionic .NET writes its own diagnostics to stdout/stderr, which Android discards. Mirror them
        // (and our console output) into logcat.
        StdRedirect.RedirectStdOut();
        StdRedirect.RedirectStdErr();

        PackageName = ReadPackageName();
        if (string.IsNullOrEmpty(PackageName))
        {
            AndroidProxy.LogError("Could not determine the package name, MelonLoader will not start");
            return;
        }

        if (IsBlocked(PackageName))
        {
            AndroidProxy.Log("MelonLoader is disabled for this game");
            return;
        }

        InternalDataDir = Path.Combine("/data/data", PackageName);
        DotnetDir = Path.Combine(InternalDataDir, "dotnet");
        BaseDir = Path.Combine(GetExternalStorageDirectory(), "MelonLoader", PackageName);

        EnsureStoragePermission();
        Directory.CreateDirectory(BaseDir);

        APKAssetManager.Initialize();
        AndroidProxy.Log($"Starting MelonLoader for {PackageName} in {BaseDir}");

        CopyMelonLoaderData(GetPatchDate());

        if (!NativeLibrary.TryLoad("libmain.so", out var selfHandle))
        {
            AndroidProxy.LogError("Could not get a handle to libmain.so");
            return;
        }

        if (!Exports.InitializeAndroid(selfHandle))
            return;

        Core.Init();
    }

    // /proc/self/cmdline holds the process name, which is the package name (optionally followed by ":service")
    private static string ReadPackageName()
    {
        try
        {
            var cmdline = File.ReadAllText("/proc/self/cmdline");
            var end = cmdline.IndexOfAny(['\0', ':']);
            return (end >= 0 ? cmdline[..end] : cmdline).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetExternalStorageDirectory()
    {
        try
        {
            JClass environment = JNI.FindClass("android/os/Environment");
            using var dir = JNI.CallStaticObjectMethod<JObject>(environment, JNI.GetStaticMethodID(environment, "getExternalStorageDirectory", "()Ljava/io/File;"));
            using var dirClass = JNI.GetObjectClass(dir);
            using var path = JNI.CallObjectMethod<JString>(dir, JNI.GetMethodID(dirClass, "getAbsolutePath", "()Ljava/lang/String;"));
            var result = path.GetString();
            if (!string.IsNullOrEmpty(result))
                return result;
        }
        catch (Exception ex)
        {
            AndroidProxy.LogError($"getExternalStorageDirectory failed: {ex.Message}");
        }

        if (JNI.ExceptionCheck())
            JNI.ExceptionClear();

        return "/storage/emulated/0";
    }

    private static int GetSdkInt()
    {
        try
        {
            JClass version = JNI.FindClass("android/os/Build$VERSION");
            return JNI.GetStaticField<int>(version, JNI.GetStaticFieldID(version, "SDK_INT", "I"));
        }
        catch
        {
            if (JNI.ExceptionCheck())
                JNI.ExceptionClear();
            return 0;
        }
    }

    // Mods, logs and configs live on shared storage so users can reach them. On Android 11+ that needs the
    // "All files access" permission; older versions rely on the storage permissions the installer adds to the manifest.
    private static void EnsureStoragePermission()
    {
        if (GetSdkInt() < 30)
            return;

        const int tries = 3;
        for (var i = 0; i < tries; i++)
        {
            try
            {
                JClass environment = JNI.FindClass("android/os/Environment");
                if (JNI.CallStaticMethod<bool>(environment, environment.GetStaticMethodID("isExternalStorageManager", "()Z")))
                    return;

                if (i == 0)
                    RequestAllFilesAccess();
            }
            catch (Exception ex)
            {
                if (JNI.ExceptionCheck())
                    JNI.ExceptionClear();
                AndroidProxy.LogError($"Failed to check storage permission: {ex.Message}");
                return;
            }

            // Give the user a moment to grant it in the settings screen we opened
            Thread.Sleep(5000);
        }

        AndroidProxy.LogError("All files access was not granted; MelonLoader may not be able to read mods or write logs");
    }

    private static void RequestAllFilesAccess()
    {
        var activity = APKAssetManager.GetContext();
        if (activity == null)
            return;

        JClass uri = JNI.FindClass("android/net/Uri");
        JClass intent = JNI.FindClass("android/content/Intent");

        using var action = JNI.NewString("android.settings.MANAGE_APP_ALL_FILES_ACCESS_PERMISSION");
        using var packageUriString = JNI.NewString($"package:{PackageName}");
        using var packageUri = JNI.CallStaticObjectMethod<JObject>(uri, uri.GetStaticMethodID("parse", "(Ljava/lang/String;)Landroid/net/Uri;"), packageUriString);
        using var request = JNI.NewObject<JObject>(intent, JNI.GetMethodID(intent, "<init>", "(Ljava/lang/String;Landroid/net/Uri;)V"), action, packageUri);

        // Needed when the context isn't an Activity
        const int FLAG_ACTIVITY_NEW_TASK = 0x10000000;
        using var withFlags = JNI.CallObjectMethod<JObject>(request, JNI.GetMethodID(intent, "addFlags", "(I)Landroid/content/Intent;"), new JValue(FLAG_ACTIVITY_NEW_TASK));

        using var contextClass = JNI.GetObjectClass(activity);
        JNI.CallVoidMethod(activity, JNI.GetMethodID(contextClass, "startActivity", "(Landroid/content/Intent;)V"), new JValue(request));

        if (JNI.ExceptionCheck())
            JNI.ExceptionClear();
    }

    // The installer writes the time it patched the APK into assets/lemon_patch_date.txt; extracted files older
    // than that are replaced.
    private static DateTimeOffset GetPatchDate()
    {
        try
        {
            var assetContent = Encoding.UTF8.GetString(APKAssetManager.GetAssetBytes("lemon_patch_date.txt")).Trim();
            if (DateTimeOffset.TryParse(assetContent, out var date))
                return date;
        }
        catch (Exception ex)
        {
            AndroidProxy.LogError($"Failed to read lemon_patch_date.txt: {ex.Message}");
        }

        // Unknown: always refresh
        return DateTimeOffset.MaxValue;
    }

    private static void CopyMelonLoaderData(DateTimeOffset patchDate)
    {
        var melonLoaderFolder = Path.Combine(BaseDir, "MelonLoader");
        if (IsOutdated(melonLoaderFolder, patchDate))
        {
            AndroidProxy.Log("Extracting MelonLoader files from the APK");
            APKAssetManager.SaveItemToDirectory("MelonLoader", BaseDir, true);
            Directory.SetLastWriteTimeUtc(melonLoaderFolder, DateTime.UtcNow);
        }

        if (IsOutdated(DotnetDir, patchDate))
        {
            AndroidProxy.Log("Extracting the .NET runtime from the APK");
            APKAssetManager.SaveItemToDirectory("dotnet", InternalDataDir, true);
            Directory.SetLastWriteTimeUtc(DotnetDir, DateTime.UtcNow);
        }
    }

    private static bool IsOutdated(string directory, DateTimeOffset patchDate)
    {
        if (!Directory.Exists(directory))
            return true;

        if (patchDate == DateTimeOffset.MaxValue)
            return true;

        return Directory.GetLastWriteTimeUtc(directory) <= patchDate.UtcDateTime;
    }

    // Packages LemonLoader refuses to run in (carried over from LemonLoader 0.6), stored as MD5 hashes.
    private static readonly HashSet<string> BlockedPackageHashes =
    [
        "95fb4cd16729627d013dc620a807c23c",
        "ffaf599e1b7e1175cd344b367e4a7ec4",
        "be1878f1900f48586eb7cab537f82f62",
        "196d46a42878aae4188839d35fdad747",
        "9b6f24bad02220abf7e12d7b4ad771f4",
        "a5595fbc343dbc2a468eb76533d345a5",
        "964c753427382e3bf56c1f7ee5a37f06",
        "e010d19cbf15c335d8f1852a1639c42c",
        "72cfa3439d21cc03ece7182cd494b75b",
        "0a4876540f4f7a11fd57a6ce54bbe0a7",
        "79aca3897e0c3e750a1f4b62776e8831",
        "f913df2dc82284a4689b8504bceb8241",
        "8239c5431b7656ab0e67ac78e6f807ff",
        "f810acd7cd40c97dfed703466476ceaa",
        "9396d377bfe52476013d5b007cfc19bf",
        "e56e5d1be5311620015cb070d11802ab",
        "6e8b0ebfaa80d548e3ee647281cbc627",
        "8e58816bb80589b154b77d84629f5a9e",
    ];

    private static bool IsBlocked(string packageName)
    {
        var hash = Convert.ToHexStringLower(Md5.Hash(Encoding.UTF8.GetBytes(packageName)));
        return BlockedPackageHashes.Contains(hash);
    }
}
#endif
