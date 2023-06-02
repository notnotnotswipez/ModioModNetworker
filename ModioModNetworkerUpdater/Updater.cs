// Originally used for BoneLib
// This is a fork of the slightly modified version of BoneLibUpdater by Lakatrazz
// https://github.com/yowchap/BoneLib/blob/main/BoneLib/BoneLibUpdater/Updater.cs

using MelonLoader;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ModioModNetworkerUpdater
{
    internal static class Updater
    {
        private static readonly string _dataDir = Path.Combine(MelonUtils.UserDataDirectory, $"{ModioModNetworkerUpdaterPlugin.PluginName}");
        private static readonly string _updaterAppName = "updater.exe";

        private static bool pluginNeedsUpdating = false;

        public static void UpdateMod()
        {
            // Check for local version of mod and read version if it exists
            Version localVersion = new Version(0, 0, 0);
            if (File.Exists(ModioModNetworkerUpdaterPlugin.ModAssemblyPath))
            {
                AssemblyName localAssemblyInfo = AssemblyName.GetAssemblyName(ModioModNetworkerUpdaterPlugin.ModAssemblyPath);
                localVersion = new Version(localAssemblyInfo.Version.Major, localAssemblyInfo.Version.Minor, localAssemblyInfo.Version.Build); // Remaking the object so there's no 4th number
                ModioModNetworkerUpdaterPlugin.Logger.Msg($"{ModioModNetworkerUpdaterPlugin.ModName}{ModioModNetworkerUpdaterPlugin.FileExtension} found in Mods folder. Version: {localVersion}");
            }

            try
            {
                Directory.CreateDirectory(_dataDir);
                string updaterScriptPath = Path.Combine(_dataDir, _updaterAppName);

                Assembly assembly = ModioModNetworkerUpdaterPlugin.UpdaterAssembly;
                string resourceName = assembly.GetManifestResourceNames().First(x => x.Contains(_updaterAppName));
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    using (FileStream fileStream = File.Create(updaterScriptPath))
                        stream.CopyTo(fileStream);
                }

                Process process = new Process();
                process.StartInfo.FileName = updaterScriptPath;
                process.StartInfo.Arguments = $"{localVersion} \"{ModioModNetworkerUpdaterPlugin.ModAssemblyPath}\" \"{ModioModNetworkerUpdaterPlugin.PluginAssemblyPath}\" \"false\"";
                process.Start();
                process.WaitForExit();
                ExitCode code = (ExitCode)process.ExitCode;

                switch (code)
                {
                    case ExitCode.Success:
                        ModioModNetworkerUpdaterPlugin.Instance.LoggerInstance.Msg($"{ModioModNetworkerUpdaterPlugin.ModName}{ModioModNetworkerUpdaterPlugin.FileExtension} updated successfully!");
                        pluginNeedsUpdating = true;
                        break;
                    case ExitCode.UpToDate:
                        ModioModNetworkerUpdaterPlugin.Instance.LoggerInstance.Msg($"{ModioModNetworkerUpdaterPlugin.ModName}{ModioModNetworkerUpdaterPlugin.FileExtension} is already up to date.");
                        break;
                    case ExitCode.Error:
                        ModioModNetworkerUpdaterPlugin.Instance.LoggerInstance.Error($"{ModioModNetworkerUpdaterPlugin.ModName}{ModioModNetworkerUpdaterPlugin.FileExtension} failed to update!");
                        break;
                }
            }
            catch (Exception e)
            {
                ModioModNetworkerUpdaterPlugin.Logger.Error($"Exception caught while running {ModioModNetworkerUpdaterPlugin.ModName} updater!");
                ModioModNetworkerUpdaterPlugin.Logger.Error(e.ToString());
            }
        }

        public static void UpdatePlugin()
        {
            if (pluginNeedsUpdating)
            {
                Directory.CreateDirectory(_dataDir);
                string updaterScriptPath = Path.Combine(_dataDir, _updaterAppName);

                Assembly assembly = Assembly.GetExecutingAssembly();
                string resourceName = assembly.GetManifestResourceNames().First(x => x.Contains(_updaterAppName));
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    using (FileStream fileStream = File.Create(updaterScriptPath))
                        stream.CopyTo(fileStream);
                }

                Process process = new Process();
                process.StartInfo.FileName = updaterScriptPath;
                process.StartInfo.Arguments = $"{new Version(0, 0, 0)} \"{ModioModNetworkerUpdaterPlugin.ModAssemblyPath}\" \"{ModioModNetworkerUpdaterPlugin.PluginAssemblyPath}\" true";
                process.Start();
            }
        }
    }

    enum ExitCode
    {
        Success = 0,
        UpToDate = 1,
        Error = 2
    }
}