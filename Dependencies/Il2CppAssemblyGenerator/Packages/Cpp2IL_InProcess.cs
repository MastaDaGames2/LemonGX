#if ANDROID
using System;
using System.IO;
using System.Linq;
using Cpp2IL.Core;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Extensions;
using MelonLoader.InternalUtils;
using MelonLoader.Utils;
using CppLogger = Cpp2IL.Core.Logging.Logger;
using UnityVersion = AssetRipper.Primitives.UnityVersion;

namespace MelonLoader.Il2CppAssemblyGenerator.Packages
{
    /// <summary>
    /// Cpp2IL has no Android executable, so on Android it runs inside the game process from the Cpp2IL.Core library
    /// bundled with the assembly generator. Its version is whatever was built in (2022.1.0-pre-release.21 or newer is
    /// required for Unity 2023.2 / 6000.x metadata).
    /// </summary>
    internal class Cpp2IL_InProcess : Models.ExecutablePackage
    {
        private static bool _loggersAttached;

        internal Cpp2IL_InProcess()
        {
            Name = nameof(Cpp2IL);
            Version = typeof(Cpp2IlApi).Assembly.CustomAttributes
                .Where(x => x.AttributeType.Name == "AssemblyInformationalVersionAttribute")
                .Select(x => x.ConstructorArguments[0].Value?.ToString()?.Split('+')[0])
                .FirstOrDefault() ?? typeof(Cpp2IlApi).Assembly.GetName().Version?.ToString();

            Destination = Path.Combine(Core.BasePath, Name);
            OutputFolder = Path.Combine(Destination, "cpp2il_out");
        }

        // Nothing to download
        internal override bool ShouldSetup() => false;

        internal override bool Execute()
        {
            Cleanup();
            Directory.CreateDirectory(OutputFolder);

            AttachLoggers();

            string metadataPath = AndroidGameFiles.ExtractMetadata(Destination);
            if (metadataPath == null)
            {
                Core.Logger.Error("Could not find global-metadata.dat in the APK");
                return false;
            }

            try
            {
                var unityVersion = UnityVersion.Parse(UnityInformationHandler.EngineVersion.ToString());
                Core.Logger.Msg($"Running Cpp2IL {Version} in-process (Unity {unityVersion})...");

                var start = DateTime.Now;

                Cpp2IlApi.Init();
                Cpp2IlApi.ConfigureLib(false);
                Cpp2IlApi.InitializeLibCpp2Il(Core.GameAssemblyPath, metadataPath, unityVersion);

                var context = Cpp2IlApi.CurrentAppContext!;

                var layers = new[] { "attributeanalyzer", "attributeinjector" }
                    .Select(ProcessingLayerRegistry.GetById)
                    .ToList();
                if (LoaderConfig.Current.UnityEngine.EnableCpp2ILCallAnalyzer)
                    layers.Add(ProcessingLayerRegistry.GetById("callanalyzer"));
                if (LoaderConfig.Current.UnityEngine.EnableCpp2ILNativeMethodDetector)
                    layers.Add(ProcessingLayerRegistry.GetById("nativemethoddetector"));

                foreach (var layer in layers.ToList())
                    layer.PreProcess(context, layers);

                foreach (var layer in layers)
                {
                    Core.Logger.Msg($"    {layer.Name}...");
                    layer.Process(context);
                }

                var output = OutputFormatRegistry.GetFormat("dummydll");
                output.OnOutputFormatSelected();
                Core.Logger.Msg($"Writing dummy assemblies to {OutputFolder}...");
                output.DoOutput(context, OutputFolder);

                Cpp2IlPluginManager.CallOnFinish();
                Cpp2IlApi.ResetInternalState();

                Core.Logger.Msg($"Cpp2IL finished in {(DateTime.Now - start).TotalSeconds:0.0}s");
                return true;
            }
            catch (Exception ex)
            {
                Core.Logger.Error($"Cpp2IL failed: {ex}");
                return false;
            }
            finally
            {
                try { File.Delete(metadataPath); } catch { }
                GC.Collect();
            }
        }

        private static void AttachLoggers()
        {
            if (_loggersAttached)
                return;
            _loggersAttached = true;

            CppLogger.InfoLog += (msg, source) => Core.Logger.Msg($"[{source}] {msg.TrimEnd('\n')}");
            CppLogger.WarningLog += (msg, source) => Core.Logger.Warning($"[{source}] {msg.TrimEnd('\n')}");
            CppLogger.ErrorLog += (msg, source) => Core.Logger.Error($"[{source}] {msg.TrimEnd('\n')}");
            CppLogger.VerboseLog += (msg, source) =>
            {
                if (MelonDebug.IsEnabled())
                    Core.Logger.Msg($"[{source}] {msg.TrimEnd('\n')}");
            };
        }
    }
}
#endif
