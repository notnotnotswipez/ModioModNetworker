using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Web.WebPages;
using LabFusion.Network;
using LabFusion.Representation;
using LabFusion.SDK.Modules;
using LabFusion.Utilities;
using MelonLoader;
using MelonLoader.ICSharpCode.SharpZipLib.Zip;
using ModioModNetworker.Data;
using SLZ.Marrow.SceneStreaming;
using SLZ.Marrow.Warehouse;
using UnityEngine;
using ZipFile = System.IO.Compression.ZipFile;

namespace ModioModNetworker
{
    public class MainClass : MelonMod
    {

        private static string MODIO_MODNETWORKER_DIRECTORY = MelonUtils.GameDirectory + "/ModIoModNetworker";
        private static string MODIO_AUTH_TXT_DIRECTORY = MODIO_MODNETWORKER_DIRECTORY+"/auth.txt";

        public static List<string> subscribedModIoIds = new List<string>();
        private static List<string> toRemoveSubscribedModIoIds = new List<string>();
        public static List<ModInfo> installedMods = new List<ModInfo>();
        public static List<InstalledModInfo> InstalledModInfos = new List<InstalledModInfo>();

        public static bool warehouseReloadRequested = false;
        public static string warehousePalletReloadTarget = "";
        public static string warehouseTargetFolder = "";
        
        public static bool subsChanged = false;
        public static bool refreshInstalledModsRequested = false;
        public static bool menuRefreshRequested = false;

        public static string subscriptionThreadString = "";
        public static bool subsRefreshing = false;
        private static int desiredSubs = 0;

        private bool addedCallback = false;
        
        private MelonPreferences_Category melonPreferencesCategory;
        private MelonPreferences_Entry<string> modsDirectory;

        public override void OnInitializeMelon()
        {
            melonPreferencesCategory = MelonPreferences.CreateCategory("ModioModNetworker");
            melonPreferencesCategory.SetFilePath(MelonUtils.UserDataDirectory+"/ModioModNetworker.cfg");
            modsDirectory =
                melonPreferencesCategory.CreateEntry<string>("ModDirectoryPath",
                    Application.persistentDataPath + "/Mods");
            ModFileManager.MOD_FOLDER_PATH = modsDirectory.Value;
            PrepareModFiles();
            string auth = ReadAuthKey();
            if (auth.IsEmpty())
            {
                MelonLogger.Error("---------------- IMPORTANT ERROR ----------------");
                MelonLogger.Error("AUTH KEY NOT FOUND IN auth.txt.");
                MelonLogger.Error("MODIONETWORKER WILL NOT RUN! PLEASE FOLLOW THE INSTRUCTIONS LOCATED IN auth.txt!");
                MelonLogger.Error("You can find the auth.txt file in the ModIoModNetworker folder in your game directory.");
                MelonLogger.Error("-------------------------------------------------");
                return;
            }
            
            ModFileManager.OAUTH_KEY = auth;
            MelonLogger.Msg("Registered on mod.io with auth key!");

            MelonLogger.Msg("Loading internal module...");
            ModuleHandler.LoadModule(System.Reflection.Assembly.GetExecutingAssembly());
            ModFileManager.Initialize();
            ModlistMenu.Initialize();
            MultiplayerHooking.OnPlayerJoin += OnPlayerJoin;
            MultiplayerHooking.OnDisconnect += OnDisconnect;
            MultiplayerHooking.OnStartServer += OnStartServer;
            MelonLogger.Msg("Populating currently installed mods via this mod.");
            installedMods.Clear();
            InstalledModInfos.Clear();
            PopulateInstalledMods(ModFileManager.MOD_FOLDER_PATH);
            MelonLogger.Msg("Checking mod.io account subscriptions");
            ModFileManager.QueueSubscriptions();
            
            melonPreferencesCategory.SaveToFile();
        }

        public override void OnUpdate()
        {
            if (!addedCallback)
            {
                if (AssetWarehouse.Instance != null)
                {
                    AssetWarehouse.Instance.OnCrateAdded += new Action<string>((s =>
                    {
                        foreach (var playerRep in PlayerRepManager.PlayerReps)
                        {
                            // Use reflection to get the _isAvatarDirty bool from the playerRep
                            // This is so we can force the playerRep to update the avatar, just incase they had an avatar that we didnt previously have.
                            var isAvatarDirty = playerRep.GetType().GetField("_isAvatarDirty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            isAvatarDirty.SetValue(playerRep, true);
                        }
                    }));
                    addedCallback = true;
                }
            }

            if (subsRefreshing)
            {
                if (subscribedModIoIds.Count >= desiredSubs)
                {
                    // Remove all invalid modids from the list
                    foreach (var modioId in toRemoveSubscribedModIoIds)
                    {
                        subscribedModIoIds.Remove(modioId);
                    }
                    toRemoveSubscribedModIoIds.Clear();
                    ModlistMenu.Refresh(true);
                    subsRefreshing = false;
                    ModFileManager.fetchingSubscriptions = false;
                    FusionNotifier.Send(new FusionNotification()
                    {
                        title = "Mod.io Subscriptions Refreshed!",
                        showTitleOnPopup = true,
                        popupLength = 1f,
                        isMenuItem = false,
                        isPopup = true,
                    });
                }
            }

            if (warehouseReloadRequested && AssetWarehouse.Instance != null)
            {
                bool update = false;
                if (!warehousePalletReloadTarget.IsEmpty())
                {
                    AssetWarehouse.Instance.ReloadPallet(warehousePalletReloadTarget);
                    warehouseTargetFolder = "";
                    warehousePalletReloadTarget = "";
                    update = true;
                }
                else if (!warehouseTargetFolder.IsEmpty())
                {
                    AssetWarehouse.Instance.LoadPalletFromFolderAsync(warehouseTargetFolder, true);
                    warehouseTargetFolder = "";
                    warehousePalletReloadTarget = "";
                }

                string reference = "Downloaded!";
                string subtitle = "This mod has been loaded into the game.";
                if (update)
                {
                    reference = "Updated!";
                    subtitle = "This mod has been updated and reloaded.";
                }

                FusionNotifier.Send(new FusionNotification()
                {
                    title = $"{ModlistMenu.activeDownloadModInfo.modId} {reference}",
                    showTitleOnPopup = true,
                    message = subtitle,
                    popupLength = 3f,
                    isMenuItem = false,
                    isPopup = true,
                });
                
                warehouseReloadRequested = false;
                ModFileManager.isDownloading = false;
                ModlistMenu.activeDownloadModInfo = null;
            }
            
            ModInfo.HandleQueue();
            ModFileManager.CheckQueue();

            if (subsChanged)
            {
                subsChanged = false;
                if (NetworkInfo.HasServer && NetworkInfo.IsServer)
                {
                    SendAllMods();
                }
            }

            if (menuRefreshRequested)
            {
                ModlistMenu.Refresh(true);
                menuRefreshRequested = false;
            }

            if (refreshInstalledModsRequested)
            {
                installedMods.Clear();
                InstalledModInfos.Clear();
                PopulateInstalledMods(ModFileManager.MOD_FOLDER_PATH);
                ModlistMenu.Refresh(true);
                refreshInstalledModsRequested = false;
            }

            if (!subscriptionThreadString.IsEmpty())
            {
                PopulateSubscriptions();
            }
        }

        public static void ReceiveSubModInfo(ModInfo modInfo)
        {
            if (!modInfo.isValidMod)
            {
                toRemoveSubscribedModIoIds.Add(modInfo.modId);
            }

            ModFileManager.AddToQueue(modInfo);
            subscribedModIoIds.Add(modInfo.modId);
        }

        public static void PopulateSubscriptions()
        {
            subscribedModIoIds.Clear();

            string json = subscriptionThreadString;
            subscriptionThreadString = "";
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            var subs = serializer.Deserialize<dynamic>(json);
            int count = 0;
            foreach (var sub in subs["data"])
            {
                if (sub["game_id"] == 3809)
                {
                    count++;
                }
            }
            
            desiredSubs = count;
            subsRefreshing = true;
            
            while (ModInfo.modInfoThreadRequests.TryDequeue(out var useless))
            {
            }
            ModInfo.requestSize = count;
            foreach (var sub in subs["data"])
            {
                // Make sure the sub is a mod from Bonelab
                if (sub["game_id"] == 3809)
                {
                    string modUrl = sub["profile_url"];
                    string[] split = modUrl.Split('/');
                    string name = split[split.Length - 1];
                    ModInfo.RequestModInfo(name, "sub");
                }
            }
        }

        public void PopulateInstalledMods(string directory)
        {
            foreach (var subDirectory in Directory.GetDirectories(directory))
            {
                PopulateInstalledMods(subDirectory);
            }
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string palletId = "";
            bool validMod = false;
            foreach (var file in Directory.GetFiles(directory))
            {
                if (file.EndsWith("pallet.json"))
                {
                    string fileContents = File.ReadAllText(file);
                    var jsonData = serializer.Deserialize<dynamic>(fileContents);
                    try
                    {
                        palletId = jsonData["objects"]["o:1"]["barcode"];
                        validMod = true;
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Warning($"Failed to parse pallet.json for mod {directory}: {e}");
                    }
                }
            }

            if (validMod)
            {
                foreach (var file in Directory.GetFiles(directory))
                {
                    if (file.EndsWith("modinfo.json"))
                    {
                        var modInfo = serializer.Deserialize<ModInfo>(File.ReadAllText(file));
                        installedMods.Add(modInfo);
                        InstalledModInfos.Add(new InstalledModInfo(){palletJsonPath = file, modId = modInfo.modId, palletBarcode = palletId});
                    }
                }
            }
        }

        public void OnStartServer()
        {
            ModlistMenu.Refresh(false);
        }

        public void OnDisconnect()
        {
            ModlistMenu.Clear();
        }

        public void OnPlayerJoin(PlayerId playerId)
        {
            if (NetworkInfo.HasServer && NetworkInfo.IsServer)
            {
                SendAllMods();
            }
        }

        private void SendAllMods()
        {
            int index = 0;
            foreach (string id in subscribedModIoIds)
            {
                bool final = index == subscribedModIoIds.Count - 1;
                ModlistData modListData = ModlistData.Create(final, id);
                using (var writer = FusionWriter.Create()) {
                    using (var data = modListData) {
                        writer.Write(data);
                        using (var message = FusionMessage.ModuleCreate<ModlistMessage>(writer))
                        {
                            MessageSender.BroadcastMessageExceptSelf(NetworkChannel.Reliable, message);
                        }
                    }
                }
                index++;
            }
        }

        private void PrepareModFiles()
        {
            if (!Directory.Exists(MODIO_MODNETWORKER_DIRECTORY))
            {
                Directory.CreateDirectory(MODIO_MODNETWORKER_DIRECTORY);
            }

            if (!File.Exists(MODIO_AUTH_TXT_DIRECTORY))
            {
                CreateDefaultAuthText(MODIO_AUTH_TXT_DIRECTORY);
            }
        }

        private void CreateDefaultAuthText(string directory)
        {
            using (StreamWriter sw = File.CreateText(directory))    
            {    
                sw.WriteLine("#                       ----- WELCOME TO THE MOD.IO AUTH TXT! -----");
                sw.WriteLine("#");
                sw.WriteLine("# Put your mod.io OAuth key in this file, and it will be used to download mods from the mod.io network.");
                sw.WriteLine("# Your OAuth key can be found here: https://mod.io/me/access");
                sw.WriteLine("# At the bottom, you should see a section called 'OAuth Access'");
                sw.WriteLine("# Create a key, call it whatever you'd like, this doesnt matter.");
                sw.WriteLine("# Then create a token, call it whatever you'd like, this doesnt matter.");
                sw.WriteLine("# Once you've created the token, copy it, and paste it in this file, replacing the text below.");
                sw.WriteLine("AuthToken=REPLACE_THIS_TEXT_WITH_YOUR_TOKEN");
            }   
        }

        private string ReadAuthKey()
        {
            // Read the file and get the AuthToken= line
            string[] lines = File.ReadAllLines(MODIO_AUTH_TXT_DIRECTORY);
            string builtString = "";
            foreach (string line in lines)
            {
                if (!line.StartsWith("#"))
                {
                    builtString += line;
                }
            }
            
            return builtString.Replace("AuthToken=", "").Replace("REPLACE_THIS_TEXT_WITH_YOUR_TOKEN", "").Trim();
        }
    }
}