// Originally used for BoneLib
// This is a fork of the slightly modified version of BoneLibUpdater by Lakatrazz
// https://github.com/yowchap/BoneLib/blob/main/BoneLib/BoneLibUpdater/Main.cs

using System;
using System.IO;
using System.Reflection;
using MelonLoader;
using static MelonLoader.MelonLogger;

namespace ModioModNetworkerUpdater
{
    public struct ModioModNetworkerUpdaterVersion
    {
        public const byte versionMajor = 1;
        public const byte versionMinor = 0;
        public const short versionPatch = 0;
    }

    public class ModioModNetworkerUpdaterPlugin : MelonPlugin
    {
        public const string Name = "Mod Io Mod Networker Updater";
        public const string Author = "notnotnotswipez";
        public static readonly Version Version = new Version(ModioModNetworkerUpdaterVersion.versionMajor, ModioModNetworkerUpdaterVersion.versionMinor, ModioModNetworkerUpdaterVersion.versionPatch);

        public static ModioModNetworkerUpdaterPlugin Instance { get; private set; }
        public static Instance Logger { get; private set; }
        public static Assembly UpdaterAssembly { get; private set; }

        private MelonPreferences_Category _prefCategory = MelonPreferences.CreateCategory("ModIoModNetworkerUpdater");
        private MelonPreferences_Entry<bool> _offlineModePref;

        public bool IsOffline => _offlineModePref.Value;

        public const string ModName = "ModioModNetworker";
        public const string PluginName = "ModioModNetworkerUpdater";
        public const string FileExtension = ".dll";

        public static readonly string ModAssemblyPath = Path.Combine(MelonHandler.ModsDirectory, $"{ModName}{FileExtension}");
        public static readonly string PluginAssemblyPath = Path.Combine(MelonHandler.PluginsDirectory, $"{PluginName}{FileExtension}");

        public override void OnPreInitialization()
        {
            Instance = this;
            Logger = LoggerInstance;
            UpdaterAssembly = MelonAssembly.Assembly;

            _offlineModePref = _prefCategory.CreateEntry("OfflineMode", false);
            _prefCategory.SaveToFile(false);

            LoggerInstance.Msg(IsOffline ? ConsoleColor.Yellow : ConsoleColor.Green, IsOffline ? "Mod Io Mod Networker Auto-Updater is OFFLINE." : "Mod Io Mod Networker is ONLINE.");

            if (IsOffline) {
                if (!File.Exists(ModAssemblyPath)) {
                    LoggerInstance.Warning($"{ModName}{FileExtension} was not found in the Mods folder!");
                    LoggerInstance.Warning("Download it from the Github or switch to ONLINE mode.");
                    LoggerInstance.Warning("https://github.com/notnotnotswipez/ModioModNetworker/releases");
                }
            }
            else {
                Updater.UpdateMod();
            }
        }

        public override void OnApplicationQuit() {
            Updater.UpdatePlugin();
        }
    }
}