#if ANDROID
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using MelonLoader.Java;
using MelonLoader.Utils;

namespace MelonLoader.Il2CppAssemblyGenerator
{
    /// <summary>
    /// Locates the game files Cpp2IL needs. On Android they live inside the APK: global-metadata.dat is an asset,
    /// and libil2cpp.so may be stored uncompressed in the APK (extractNativeLibs=false) rather than on disk.
    /// </summary>
    internal static class AndroidGameFiles
    {
        private const string MetadataAsset = "bin/Data/Managed/Metadata/global-metadata.dat";
        private const string LibIl2CppEntry = "lib/arm64-v8a/libil2cpp.so";

        internal static string ExtractMetadata(string destinationFolder)
        {
            Directory.CreateDirectory(destinationFolder);
            string path = Path.Combine(destinationFolder, "global-metadata.dat");

            using Stream asset = APKAssetManager.GetAssetStream(MetadataAsset);
            if (asset == null)
                return null;

            using (FileStream file = File.Create(path))
                asset.CopyTo(file, 81920);

            return path;
        }

        /// <summary>
        /// Returns a path to libil2cpp.so on disk, extracting it from the APK into <paramref name="cacheFolder"/> if needed.
        /// </summary>
        internal static string GetLibIl2CppPath(string cacheFolder)
        {
            string loaded = MelonEnvironment.Il2CppGameAssemblyPath;
            if (!string.IsNullOrEmpty(loaded) && File.Exists(loaded))
                return loaded;

            loaded = FindInProcessMaps("libil2cpp.so");
            if (!string.IsNullOrEmpty(loaded) && File.Exists(loaded))
                return loaded;

            foreach (string apk in GetApkPaths())
            {
                try
                {
                    using ZipArchive zip = ZipFile.OpenRead(apk);
                    ZipArchiveEntry entry = zip.GetEntry(LibIl2CppEntry);
                    if (entry == null)
                        continue;

                    Directory.CreateDirectory(cacheFolder);
                    string target = Path.Combine(cacheFolder, "libil2cpp.so");

                    // Only re-extract when the APK changed
                    if (!File.Exists(target) || new FileInfo(target).Length != entry.Length
                        || File.GetLastWriteTimeUtc(target) < File.GetLastWriteTimeUtc(apk))
                    {
                        Core.Logger.Msg($"Extracting libil2cpp.so from {Path.GetFileName(apk)}");
                        entry.ExtractToFile(target, true);
                    }

                    return target;
                }
                catch (Exception ex)
                {
                    Core.Logger.Warning($"Failed to read {apk}: {ex.Message}");
                }
            }

            return null;
        }

        private static string FindInProcessMaps(string libraryName)
        {
            try
            {
                foreach (string line in File.ReadLines("/proc/self/maps"))
                {
                    int pathStart = line.IndexOf('/');
                    if (pathStart < 0)
                        continue;

                    string path = line[pathStart..].Trim();
                    if (path.EndsWith("/" + libraryName, StringComparison.Ordinal))
                        return path;
                }
            }
            catch { }

            return null;
        }

        // The base APK plus any split APKs (the native libraries are often in split_config.arm64_v8a.apk)
        private static IEnumerable<string> GetApkPaths()
        {
            var paths = new List<string>();
            try
            {
                using JObject context = APKAssetManager.GetContext();
                if (context != null)
                {
                    using JClass contextClass = JNI.GetObjectClass(context);
                    using JObject appInfo = JNI.CallObjectMethod<JObject>(context, JNI.GetMethodID(contextClass, "getApplicationInfo", "()Landroid/content/pm/ApplicationInfo;"));
                    using JClass appInfoClass = JNI.GetObjectClass(appInfo);

                    using JString sourceDir = JNI.GetObjectField<JString>(appInfo, JNI.GetFieldID(appInfoClass, "sourceDir", "Ljava/lang/String;"));
                    if (sourceDir != null && sourceDir.Valid())
                        paths.Add(sourceDir.GetString());

                    using JObjectArray<JString> splits = JNI.GetObjectField<JObjectArray<JString>>(appInfo, JNI.GetFieldID(appInfoClass, "splitSourceDirs", "[Ljava/lang/String;"));
                    if (splits != null && splits.Valid())
                        paths.AddRange(splits.Select(s => s.GetString()));
                }
            }
            catch (Exception ex)
            {
                Core.Logger.Warning($"Could not list the game's APKs: {ex.Message}");
            }

            if (JNI.ExceptionCheck())
                JNI.ExceptionClear();

            // Fall back to anything APK-like mapped into the process
            if (paths.Count == 0)
            {
                try
                {
                    paths.AddRange(File.ReadLines("/proc/self/maps")
                        .Select(l => l.IndexOf('/') is var i and >= 0 ? l[i..].Trim() : null)
                        .Where(p => p != null && p.EndsWith(".apk", StringComparison.Ordinal) && p.Contains(MelonEnvironment.PackageName))
                        .Distinct());
                }
                catch { }
            }

            return paths.Where(File.Exists).Distinct();
        }
    }
}
#endif
