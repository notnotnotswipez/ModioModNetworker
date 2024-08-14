using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using BoneLib;
using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Representation;
using LabFusion.SDK.Modules;
using LabFusion.Utilities;
using MelonLoader;
using MelonLoader.ICSharpCode.SharpZipLib.Zip;
using MelonLoader.TinyJSON;
using ModioModNetworker.Data;
using ModioModNetworker.Patches;
using ModioModNetworker.Queue;
using ModioModNetworker.UI;
using ModioModNetworker.Utilities;
using ModIoModNetworker.Ui;
using Newtonsoft.Json;
using Il2CppSLZ.Marrow.SceneStreaming;
using Il2CppSLZ.Marrow.Warehouse;
using Il2CppSLZ.Rig;
using UnityEngine;
using File = System.IO.File;
using ZipFile = System.IO.Compression.ZipFile;
using LabFusion.Entities;
using static UnityEngine.ProBuilder.AutoUnwrapSettings;
using static UnityEngine.GraphicsBuffer;
using LabFusion.Player;
using LabFusion.Scene;
using Il2CppSLZ.Marrow;
using BoneLib.BoneMenu;
using LabFusion.Marrow;
using Il2CppSLZ.Marrow.Forklift;
using LabFusion.Downloading.ModIO;

namespace ModioModNetworker
{
    public struct ModioModNetworkerUpdaterVersion
    {
        public const string versionString = "2.3.0";
    }
    
    public class MainClass : MelonMod
    {

        private static string MODIO_MODNETWORKER_DIRECTORY = MelonUtils.GameDirectory + "/ModIoModNetworker";
        private static string MODIO_AUTH_TXT_DIRECTORY = MODIO_MODNETWORKER_DIRECTORY+"/auth.txt";
        private static string MODIO_BLACKLIST_TXT_DIRECTORY = MODIO_MODNETWORKER_DIRECTORY+"/blacklist.txt";

        public static List<string> subscribedModIoNumericalIds = new List<string>();
        public static List<string> blacklistedModIoIds = new List<string>();
        private static List<string> toRemoveSubscribedModIoIds = new List<string>();
        public static List<ModInfo> subscribedMods = new List<ModInfo>();
        public static List<ModInfo> installedMods = new List<ModInfo>();
        public static List<InstalledModInfo> InstalledModInfos = new List<InstalledModInfo>();

        private static List<InstalledModInfo> outOfDateModInfos = new List<InstalledModInfo>();

        public static bool warehouseReloadRequested = false;
        public static List<string> warehousePalletReloadTargets = new List<string>();
        public static List<string> warehouseReloadFolders = new List<string>();

        public static bool subsChanged = false;
        public static bool refreshInstalledModsRequested = false;
        public static bool refreshSubscribedModsRequested = false;
        public static bool menuRefreshRequested = false;

        public static string subscriptionThreadString = "";
        public static string trendingThreadString = "";
        public static bool subsRefreshing = false;
        private static int desiredSubs = 0;

        private bool addedCallback = false;
        
        public static MelonPreferences_Category melonPreferencesCategory;
        private static MelonPreferences_Entry<string> modsDirectory;
        public static MelonPreferences_Entry<bool> autoDownloadAvatarsConfig;
        public static MelonPreferences_Entry<bool> autoDownloadSpawnablesConfig;
        public static MelonPreferences_Entry<bool> autoDownloadLevelsConfig;
        public static MelonPreferences_Entry<bool> downloadMatureContentConfig;
        public static MelonPreferences_Entry<bool> tempLobbyModsConfig;
        public static MelonPreferences_Entry<bool> overrideFusionDLConfig;
        public static MelonPreferences_Entry<float> maxAutoDownloadMbConfig;
        public static MelonPreferences_Entry<float> maxLevelAutoDownloadGbConfig;

        public static float maxAutoDownloadMb = 500f;
        public static bool autoDownloadAvatars = true;
        public static bool autoDownloadSpawnables = true;
        public static bool autoDownloadLevels = false;
        public static float levelMaxGb = 1f;
        public static bool downloadMatureContent = false;
        public static bool tempLobbyMods = false;
        public static bool useRepo = false;
        public static bool overrideFusionDL = false;

        public static List<string> modNumericalsDownloadedDuringLobbySession = new List<string>();
        
        private static int subsShown = 0;
        private static int subTotal = 0;
        
        public bool palletLock = false;

        public static bool confirmedHostHasIt = false;
        private static bool loadedInstalled = false;

        public static bool handlingInstalled = false;
        public static bool handlingSubscribed = false;

        public override void OnInitializeMelon()
        {
            

            melonPreferencesCategory = MelonPreferences.CreateCategory("ModioModNetworker");
            melonPreferencesCategory.SetFilePath(MelonUtils.UserDataDirectory+"/ModioModNetworker.cfg");
            modsDirectory =
                melonPreferencesCategory.CreateEntry<string>("ModDirectoryPath",
                    Application.persistentDataPath + "/Mods");
            autoDownloadAvatarsConfig = melonPreferencesCategory.CreateEntry<bool>("AutoDownloadAvatars", true);
            autoDownloadSpawnablesConfig = melonPreferencesCategory.CreateEntry<bool>("AutoDownloadSpawnables", true);
            autoDownloadLevelsConfig = melonPreferencesCategory.CreateEntry<bool>("AutoDownloadLevels", true);
            maxLevelAutoDownloadGbConfig = melonPreferencesCategory.CreateEntry<float>("MaxLevelAutoDownloadGb", 1f);
            tempLobbyModsConfig = melonPreferencesCategory.CreateEntry<bool>("TemporaryLobbyMods", false, null, "If set to true, lobby mods like (avatars/spawnables/levels) that got auto downloaded will be deleted when you leave the lobby.");
            maxAutoDownloadMbConfig = melonPreferencesCategory.CreateEntry<float>("MaxAutoDownloadMb", 500f);
            downloadMatureContentConfig = melonPreferencesCategory.CreateEntry<bool>("DownloadMatureContent", false);
            overrideFusionDLConfig = melonPreferencesCategory.CreateEntry<bool>("OverrideFusionDL", true);
            
            maxAutoDownloadMb = maxAutoDownloadMbConfig.Value;
            autoDownloadAvatars = autoDownloadAvatarsConfig.Value;
            downloadMatureContent = downloadMatureContentConfig.Value;
            autoDownloadSpawnables = autoDownloadSpawnablesConfig.Value;
            autoDownloadLevels = autoDownloadLevelsConfig.Value;
            tempLobbyMods = tempLobbyModsConfig.Value;
            levelMaxGb = maxLevelAutoDownloadGbConfig.Value;
            useRepo = false;
            overrideFusionDL = overrideFusionDLConfig.Value;
            
            ModFileManager.MOD_FOLDER_PATH = modsDirectory.Value;

            SpotlightOverride.LoadFromRegularURL();
            

            AssetBundle uiAssets;
            if (!HelperMethods.IsAndroid())
            {
                uiAssets = EmbeddedAssetBundle.LoadFromAssembly(Assembly.GetExecutingAssembly(), "ModioModNetworker.Resources.networkermenu.networker");
            }
            else
            {
                uiAssets = EmbeddedAssetBundle.LoadFromAssembly(Assembly.GetExecutingAssembly(), "ModioModNetworker.Resources.networkermenu.android.networker");
            }
            
            NetworkerAssets.LoadAssetsUI(uiAssets);

            PrepareModFiles();
            string auth = ReadAuthKey();
            blacklistedModIoIds = ReadBlacklist();
            MelonLogger.Msg("Loaded blacklist with "+blacklistedModIoIds.Count+" entries.");
           


            ModIOSettings.LoadToken(OnLoadToken);

                
            void OnLoadToken(string loadedToken) {

                ModFileManager.OAUTH_KEY = loadedToken;

                MelonLogger.Msg("Populating currently installed mods via this mod.");
                installedMods.Clear();
                InstalledModInfos.Clear();
                PopulateInstalledMods(ModFileManager.MOD_FOLDER_PATH);
                loadedInstalled = true;
                MelonLogger.Msg("Checking mod.io account subscriptions");
                PopulateSubscriptions();
                ModFileManager.QueueTrending(0);
                MelonLogger.Msg("Registered on mod.io with auth key!");
            }
            
            

            MelonLogger.Msg("Loading internal module...");
            ModuleHandler.LoadModule(Assembly.GetExecutingAssembly());
            ModFileManager.Initialize();
            ModlistMenu.Initialize();
            MultiplayerHooking.OnPlayerJoin += OnPlayerJoin;
            MultiplayerHooking.OnDisconnect += OnDisconnect;
            MultiplayerHooking.OnStartServer += OnStartServer;
            NetworkPlayer.OnNetworkRigCreated += OnPlayerRepCreated;
            MultiplayerHooking.OnLobbyCategoryCreated += OnLobbyCategoryMade;

            ModFileManager.QueueTrending(0);
            ModFileManager.QueueTrending(0);
            
            
 

        private void OnLobbyCategoryMade(Page category, INetworkLobby lobby)

        private void OnLobbyCategoryMade(Page category, INetworkLobby lobby)
        {
            if (lobby.TryGetMetadata("modionetworker", out var value))
            {
                category.CreateFunction("ModioModNetworker Active On Server", Color.cyan, () => { });
            }
            
            /*if (lobby.TryGetMetadata("LevelBarcode", out var barcode))
            {
                if (!FusionSceneManager.HasLevel(barcode)) {
                    lobby.TryGetMetadata("networkermap", out var numerical);
                    if (numerical != "null")
                    {
                        category.CreateFunctionElement("Download Level", Color.cyan, () => {
                            FusionNotifier.Send(new FusionNotification()
                            {
                                title = new NotificationText("Installing lobby level...", Color.cyan, true),
                                showTitleOnPopup = true,
                                popupLength = 1f,
                                message = "Please wait",
                                isMenuItem = false,
                                isPopup = true,
                            });
                            
                            // Native install because it was of our own volition
                            ModInfo.RequestModInfoNumerical(numerical, "install_native");
                        });
                    }
                }
            }*/


                
            // TODO: REPO DOWN
            if (lobby.TryGetMetadata("LevelBarcode", out var barcode))
            {
                if (!CrateFilterer.HasCrate<LevelCrate>(new Barcode(barcode))) {
                    category.CreateFunction("Download Level", Color.cyan, () =>
                    {


                        if (lobby.TryGetMetadata("networkermap", out var mapNumber))
                        {

                            if (mapNumber != "null")
                            {
                                FusionNotifier.Send(new FusionNotification()
                                {
                                    title = new NotificationText("Installing lobby level...", Color.cyan, true),
                                    showTitleOnPopup = true,
                                    popupLength = 1f,
                                    message = "Please wait",
                                    isMenuItem = false,
                                    isPopup = true,
                                });

                                // Native install because it was of our own volition
                                ModInfo.RequestModInfoNumerical(mapNumber, "install_native");
                            }
                            else {
                                FusionNotifier.Send(new FusionNotification()
                                {
                                    title = new NotificationText("Cannot install this map!", Color.cyan, true),
                                    showTitleOnPopup = true,
                                    popupLength = 1f,
                                    message = "Probably not a networked map!",
                                    isMenuItem = false,
                                    isPopup = true,
                                });
                            }
                        }
                        
                    });
                }
            }
        }

        private void DeleteAllTempMods()
        {
            foreach (var modInfo in installedMods)
            {
                if (modInfo.temp)
                {
                    ModFileManager.UnInstallMainThread(modInfo.numericalId);
                }
            }
        }

        public override void OnUpdate()
        {
            foreach (var bar in AvatarDownloadBar.bars.Values)
            {
                bar.Update();
            }

            ThumbnailThreader.HandleQueue();
            MainThreadManager.HandleQueue();
            
            LevelHoldQueue.Update();

            if (ModFileManager.activeDownloadQueueElement != null)
            {
                if (ModFileManager.activeDownloadQueueElement.associatedPlayer != null)
                {
                    if (AvatarDownloadBar.bars.TryGetValue(ModFileManager.activeDownloadQueueElement.associatedPlayer, out var bar))
                    {
                        ModInfo downloading = ModlistMenu.activeDownloadModInfo;
                        bar.SetModName(downloading.modId);
                        bar.SetPercentage((float)downloading.modDownloadPercentage);
                    }
                }
            }

            bool loadingThisFrame = false;
            if (SceneStreamer._session != null)
            {
                if (SceneStreamer._session.Status == StreamStatus.LOADING)
                {
                    loadingThisFrame = true;
                }
            }
            if (ModFileManager.activeDownloadAction != null)
            {
                if (ModFileManager.activeDownloadAction.Check())
                {
                    ModFileManager.activeDownloadAction.Handle();
                    ModFileManager.activeDownloadAction = null;
                }
            }

            if (!addedCallback)
            { 
                if (AssetWarehouse.Instance != null)
                {
                    AssetWarehouse.Instance.OnCrateAdded += new Action<Barcode>(s =>
                    {
                        palletLock = false;
                        
                        foreach (var playerRep in NetworkPlayerUtilities.GetAllNetworkPlayers())
                        {
                            // Use reflection to get the _isAvatarDirty bool from the playerRep
                            // This is so we can force the playerRep to update the avatar, just incase they had an avatar that we didnt previously have.
                            var isAvatarDirty = playerRep.AvatarSetter.GetType().GetField("_isAvatarDirty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            isAvatarDirty.SetValue(playerRep.AvatarSetter, true);
                        }
                        SpawnableHoldQueue.CheckValid(s._id);
                        LevelHoldQueue.CheckValid(s._id);

                    });
                    addedCallback = true;
                    DeleteAllTempMods();
                }
            }

            if (subsRefreshing)
            {
                if (subscribedModIoNumericalIds.Count >= desiredSubs)
                {
                    // Remove all invalid modids from the list
                    foreach (var modioId in toRemoveSubscribedModIoIds)
                    {
                        subscribedModIoNumericalIds.Remove(modioId);
                    }
                    toRemoveSubscribedModIoIds.Clear();
                    ModlistMenu.Refresh(true);
                    subsRefreshing = false;
                    handlingSubscribed = false;
                    FusionNotifier.Send(new FusionNotification()
                    {
                        title = new NotificationText("Mod.io Subscriptions Refreshed!", Color.cyan, true),
                        showTitleOnPopup = true,
                        popupLength = 1f,
                        isMenuItem = false,
                        isPopup = true,
                    });
                    MelonLogger.Msg("Finished refreshing mod.io subscriptions!");
                    
                    outOfDateModInfos.Clear();
                }
            }
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Subs refreshing check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            
            //stopwatch = Stopwatch.StartNew();
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Active download web request check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            
            //stopwatch = Stopwatch.StartNew();

            if (warehouseReloadRequested && assetWarehouseLoaded && AssetWarehouse.Instance._initialLoaded && !palletLock)
            {
                bool update = false;
                if (warehousePalletReloadTargets.Count > 0)
                {
                    ModFileManager.DeleteExistingModObjects(warehousePalletReloadTargets[0]);
                    //AssetWarehouse.Instance.ReloadPallet(warehousePalletReloadTargets[0]);

                    PalletManifest manifest = null;
                    foreach (var keypair in AssetWarehouse.Instance.palletManifests) {
                        if (keypair.key._id == warehousePalletReloadTargets[0]) {
                            manifest = keypair.value;
                            break;
                        }
                    }

                    AssetWarehouse.Instance.LoadAndUpdatePalletManifest(manifest.Pallet, ModlistMenu.activeDownloadModInfo.ToModListing(), manifest.PalletPath, manifest.CatalogPath);
                    warehousePalletReloadTargets.RemoveAt(0);
                    update = true; 
                }

                if (warehouseReloadFolders.Count > 0)
                {
                    AssetWarehouse.Instance.LoadPalletFromFolderAsync(warehouseReloadFolders[0], true, null, ModlistMenu.activeDownloadModInfo.ToModListing());
                    // Remove the first element from the list
                    warehouseReloadFolders.RemoveAt(0);
                    palletLock = true;
                }

                if (warehouseReloadFolders.Count == 0 && warehousePalletReloadTargets.Count == 0)
                {
                    string reference = "Downloaded!";
                    string subtitle = "This mod has been loaded into the game.";
                    if (update)
                    {
                        reference = "Updated!";
                        subtitle = "This mod has been updated and reloaded.";
                    }

                    if (ModFileManager.activeDownloadQueueElement.notify)
                    {
                        FusionNotifier.Send(new FusionNotification()
                        {
                            title = new NotificationText($"{ModlistMenu.activeDownloadModInfo.modId} {reference}", Color.cyan, true),
                            showTitleOnPopup = true,
                            message = new NotificationText(subtitle),
                            popupLength = 3f,
                            isMenuItem = false,
                            isPopup = true,
                        });
                    }

                    if (ModFileManager.activeDownloadQueueElement != null)
                    {
                        if (ModFileManager.activeDownloadQueueElement.associatedPlayer != null)
                        {
                            if (AvatarDownloadBar.bars.TryGetValue(ModFileManager.activeDownloadQueueElement.associatedPlayer, out var bar))
                            {
                                bar.Finish();
                            }
                        }
                    }
                    palletLock = false;
                    warehouseReloadRequested = false;
                    ModFileManager.isDownloading = false;
                    ModFileManager.activeDownloadWebRequest = null;
                    ModlistMenu.activeDownloadModInfo = null;
                    ModFileManager.activeDownloadQueueElement = null;
                }
            }
            
            ModInfo.HandleQueue();
            ModFileManager.CheckQueue();

            if (!loadingThisFrame)
            {
                if (ModlistMessage.waitAndQueue.Count > 0)
                {
                    foreach (var modInfo in ModlistMessage.waitAndQueue)
                    {
                        float mb = modInfo.fileSizeKB / 1000000;
                        if (mb < maxAutoDownloadMb && autoDownloadAvatars)
                        {
                            if (!downloadMatureContent && modInfo.mature)
                            {
                                return;
                            }

                            ModFileManager.AddToQueue(new DownloadQueueElement()
                            {
                                associatedPlayer = null,
                                info = modInfo,
                                notify = false
                            });
                        }
                    }
                    ModlistMessage.waitAndQueue.Clear();
                }
            }

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
                if (loadingThisFrame)
                {
                    return;
                }

                ModlistMenu.Refresh(true);
                menuRefreshRequested = false;
            }

            if (refreshSubscribedModsRequested && !handlingInstalled && !handlingSubscribed) {
                refreshSubscribedModsRequested = false;
                handlingSubscribed = true;

                subscribedMods.Clear();
                subscribedModIoNumericalIds.Clear();
                subTotal = 0;
                subsShown = 0;
                desiredSubs = 0;
                ModFileManager.QueueSubscriptions(subsShown);
            }

            if (refreshInstalledModsRequested && !handlingSubscribed && !handlingInstalled)
            {
                installedMods.Clear();
                InstalledModInfos.Clear();
                
                NetworkerMenuController.totalInstalled.Clear();
                ModlistMenu.installPage = 0;
                Thread thread = new Thread(() =>
                {
                    handlingInstalled = true;
                    PopulateInstalledMods(ModFileManager.MOD_FOLDER_PATH);
                    MainThreadManager.QueueAction(() =>
                    {
                        if (NetworkerMenuController.instance) {
                            NetworkerMenuController.instance.Refresh();
                        }
                    });
                    handlingInstalled = false;
                });
                thread.Start();
                loadedInstalled = true;
                ModlistMenu.Refresh(true);
                refreshInstalledModsRequested = false;
                if (NetworkerMenuController.instance)
                {
                    NetworkerMenuController.instance.UpdateModPopupButtons();
                }
            }

            if (subscriptionThreadString != "")
            {
                InternalPopulateSubscriptions();
            }
            if (trendingThreadString != "")
            {
                InternalPopulateTrending();
                if (NetworkerMenuController.instance) {
                    NetworkerMenuController.instance.OnNewTrendingRecieved();
                }
            }
        }

        private static void UpdateModInfo(ModInfo subscribed, InstalledModInfo installed)
        {
            try
            {
                ModInfo original = installed.ModInfo;
                original.mature = subscribed.mature;
                original.modSummary = subscribed.modSummary;
                original.modName = subscribed.modName;
                original.thumbnailLink = subscribed.thumbnailLink;
                original.numericalId = subscribed.numericalId;
                original.structureVersion = ModInfo.globalStructureVersion;
                original.windowsDownloadLink = subscribed.windowsDownloadLink;
                original.androidDownloadLink = subscribed.androidDownloadLink;

                if (original.version == null)
                {
                    original.version = "0.0.0";
                }
                string modInfoPath = Path.Combine(Directory.GetParent(installed.palletPath).Name, "modinfo.json");
                // Delete the old modinfo.json
                File.Delete(modInfoPath);
                // Write the new modinfo.json
                string modInfoJson = (string) JsonConvert.SerializeObject(original);
                File.WriteAllText(modInfoPath, modInfoJson);
                MelonLogger.Msg($"Updated modinfo.json for {original.modId} to version {original.structureVersion}");
            }
            catch (Exception e)
            {
                MelonLogger.Error("Skipped updating modinfo.json for "+installed.ModInfo.modId+" because of an error: "+e);
            }
        }

        public static void ReceiveSubModInfo(ModInfo modInfo, bool ignoreTag = false)
        {
            InstalledModInfo outOfDate = null;
            if (modInfo.version == null)
            {
                modInfo.version = "0.0.0";
            }

            /*foreach (var outOfDateInfo in outOfDateModInfos)
            {
                if (outOfDateInfo.modId == modInfo.modId)
                {
                    outOfDate = outOfDateInfo;
                    UpdateModInfo(modInfo, outOfDateInfo);
                }
            }*/

            if (outOfDate != null)
            {
                outOfDateModInfos.Remove(outOfDate);
            }

            if (!modInfo.isValidMod)
            {
                toRemoveSubscribedModIoIds.Add(modInfo.numericalId);
            }

            ModFileManager.AddToQueue(new DownloadQueueElement()
            {
                associatedPlayer = null,
                info = modInfo
            });

            subscribedModIoNumericalIds.Add(modInfo.numericalId);
            subscribedMods.Add(modInfo);
        }

        public static void PopulateSubscriptions()
        {
            refreshSubscribedModsRequested = true;
        }

        private static void InternalPopulateTrending() {
            string json = trendingThreadString;
            trendingThreadString = "";
            var trending = JsonConvert.DeserializeObject<dynamic>(json);
            
            foreach (var modEntry in trending["data"])
            {
                
                string modUrl = (string) modEntry["profile_url"];
                string numericalId = "" + modEntry["id"];
                string modTitle = (string) modEntry["name"];
                string summary = (string) modEntry["summary"];
                string thumbnailLink = (string) modEntry["logo"]["thumb_640x360"];
                string[] split = modUrl.Split('/');
                string name = split[split.Length - 1];
                bool valid = true;
                int windowsId = 0;
                int androidId = 0;
                try
                {
                    
                    foreach (var platform in modEntry["platforms"])
                    {
                        if (((string)platform["platform"]) == "windows")
                        {
                            int desired = (int)platform["modfile_live"];
                            windowsId = desired;
                            break;
                        }
                    }

                    foreach (var platform in modEntry["platforms"])
                    {
                        if (((string)platform["platform"]) == "android")
                        {
                            int desired = (int)platform["modfile_live"];
                            androidId = desired;
                            break;
                        }
                    }

                    // Same file for both platforms
                    if (windowsId != 0)
                    {
                        if (androidId != 0)
                        {
                            if (windowsId == androidId)
                            {
                                valid = false;
                            }
                        }
                    }

                    if (((int)modEntry["status"]) == 3)
                    {
                        valid = false;
                    }

                    ModInfo modInfo = ModInfo.MakeFromDynamic((dynamic)modEntry["modfile"], name);
                    modInfo.isValidMod = false;
                    modInfo.mature = ((int)modEntry["maturity_option"]) > 0;
                    modInfo.modName = modTitle;
                    modInfo.thumbnailLink = thumbnailLink;
                    modInfo.modSummary = summary;
                    modInfo.numericalId = numericalId;

                    foreach (var tag in modEntry["tags"])
                    {
                        modInfo.tags.Add((string) tag["name"]);
                    }


                    if (valid)
                    {
                        modInfo.androidDownloadLink =
                            $"https://api.mod.io/v1/games/3809/mods/{(string)modEntry["id"]}/files/{androidId}/download";

                        modInfo.windowsDownloadLink =
                            $"https://api.mod.io/v1/games/3809/mods/{(string)modEntry["id"]}/files/{windowsId}/download";

                        modInfo.isValidMod = true;
                    }

                    if (modInfo.version == null)
                    {
                        modInfo.version = "0.0.0";
                    }

                    if (modInfo.mature && !downloadMatureContent)
                    {
                        return;
                    }

                    NetworkerMenuController.modIoRetrieved.Add(modInfo);
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"Failed to parse trending mod {modTitle}: "+e);
                }
            }
        }

        private static void InternalPopulateSubscriptions()
        {
            string json = subscriptionThreadString;
            subscriptionThreadString = "";
            var subs = JsonConvert.DeserializeObject<dynamic>(json);
            int count = 0;
            int resultTotal = (int) subs["result_total"];
            if (subTotal == 0)
            {
                MelonLogger.Msg("Total subscriptions: " + resultTotal);
                subTotal = resultTotal;
                desiredSubs = 0;
            }

            int resultCount = (int) subs["result_count"];
            if (resultCount == 0)
            {
                MelonLogger.Msg("No subscriptions found!");
                return;
            }
            
            foreach (var sub in subs["data"])
            {
                if ((int)sub["game_id"] == 3809)
                {
                    count++;
                }
            }
            
            desiredSubs += count;

            while (ModInfo.modInfoThreadRequests.TryDequeue(out var useless))
            {
            }

            ModInfo.requestSize = count;
            foreach (var sub in subs["data"])
            {
                // Make sure the sub is a mod from Bonelab
                if ((int)sub["game_id"] == 3809)
                {
                    string modUrl = (string)sub["profile_url"];
                    string numericalId = ""+(string)sub["id"];
                    string modTitle = (string)sub["name"];
                    string summary = (string)sub["summary"];
                    string author = (string) sub["submitted_by"]["username"];
                    string thumbnailLink = (string)sub["logo"]["thumb_640x360"];
                    string[] split = modUrl.Split('/');
                    string name = split[split.Length - 1];
                    bool valid = true;
                    int windowsId = 0;
                    int androidId = 0;
                    foreach (var platform in sub["platforms"])
                    {
                        if ((string)platform["platform"] == "windows")
                        {
                            int desired = (int)platform["modfile_live"];
                            windowsId = desired;
                            break;
                        }
                    }

                    foreach (var platform in sub["platforms"])
                    {
                        if ((string)platform["platform"] == "android")
                        {
                            int desired = (int)platform["modfile_live"];
                            androidId = desired;
                            break;
                        }
                    }

                    if (windowsId != 0)
                    {
                        if (androidId != 0)
                        {
                            if (windowsId == androidId)
                            {
                                valid = false;
                            }
                        }
                    }
                    
                    if ((int)sub["status"] == 3)
                    {
                        valid = false;
                    }

                    ModInfo modInfo = ModInfo.MakeFromDynamic((dynamic)sub["modfile"], name);
                    modInfo.isValidMod = false;
                    modInfo.mature = (int)sub["maturity_option"] > 0;
                    modInfo.modName = modTitle;
                    modInfo.thumbnailLink = thumbnailLink;
                    modInfo.modSummary = summary;
                    modInfo.numericalId = numericalId;
                    modInfo.author = author;

                    foreach (var tag in sub["tags"]) {
                        modInfo.tags.Add((string)tag["name"]);
                    }

                    if (valid)
                    {
                        modInfo.androidDownloadLink =
                            $"https://api.mod.io/v1/games/3809/mods/{sub["id"]}/files/{androidId}/download";

                        modInfo.windowsDownloadLink =
                            $"https://api.mod.io/v1/games/3809/mods/{sub["id"]}/files/{windowsId}/download";
                        
                        modInfo.isValidMod = true;
                    }
                    
                    ReceiveSubModInfo(modInfo);
                }
            }
            
            subsShown += resultCount;

            if (subTotal - subsShown > 0)
            {
                ModFileManager.QueueSubscriptions(subsShown);
            }

            if (subsShown >= subTotal)
            {
                subsRefreshing = true;
            }
        }

        public void PopulateInstalledMods(string directory)
        {
            List<DirectoryInfo> latestDirectories = new List<DirectoryInfo>();
            try
            {

                // Sort by latest
                latestDirectories = new DirectoryInfo(directory).GetDirectories()
                                                  .OrderByDescending(f => f.LastWriteTime)
                                                  .ToList();
            }
            catch (Exception ex)
            {
                // Ignore, something just went wrong in a phase where we cannot do anything
            }

            bool useManifests = true;

            if (!useManifests)
            {
                /*foreach (var subDirectory in latestDirectories)
                {
                    PopulateInstalledMods(subDirectory.FullName);
                }

                string palletId = "";
                bool validMod = false;
                string palletJsonPath = "";
                foreach (var file in Directory.GetFiles(directory))
                {
                    if (file.EndsWith("pallet.json"))
                    {
                        palletJsonPath = file;
                        string fileContents = File.ReadAllText(file);
                        var jsonData = JsonConvert.DeserializeObject<dynamic>(fileContents);
                        try
                        {
                            palletId = (string) jsonData["objects"]["1"]["barcode"];
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
                    bool foundModInfo = false;
                    foreach (var file in Directory.GetFiles(directory))
                    {

                        if (file.EndsWith("modinfo.json"))
                        {
                            try
                            {
                                var modInfo = (ModInfo) JsonConvert.DeserializeObject<ModInfo>(File.ReadAllText(file));
                                foundModInfo = true;
                                bool installedAlready = false;

                                // We can use modid for this cause these are tracked
                                foreach (var alreadyInstalled in installedMods)
                                {
                                    if (alreadyInstalled.numericalId == modInfo.numericalId)
                                    {
                                        installedAlready = true;
                                    }
                                }

                                if (installedAlready)
                                {
                                    continue;
                                }

                                installedMods.Add(modInfo);
                                InstalledModInfo installedModInfo = new InstalledModInfo()
                                {
                                    modinfoJsonPath = file,
                                    modId = modInfo.modId,
                                    palletBarcode = palletId,
                                    ModInfo = modInfo
                                };

                                NetworkerMenuController.totalInstalled.Add(modInfo);
                                InstalledModInfos.Add(installedModInfo);
                                if (modInfo.structureVersion != ModInfo.globalStructureVersion)
                                {
                                    outOfDateModInfos.Add(installedModInfo);
                                }
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Error("Error while parsing mod at: " + file);
                            }
                        }
                    }

                    if (!foundModInfo)
                    {
                        // TODO: REPO INFO
                        // This means this is NOT a networker tracked mod, but with the repo we can mangle it to be one.
                        foreach (var alreadyInstalled in InstalledModInfos)
                        {
                            if (alreadyInstalled.palletBarcode == palletId)
                            {
                                return;
                            }
                        }


                    }
                }*/
            }
            else {
                foreach (var file in Directory.GetFiles(directory))
                {
                    if (file.EndsWith(".manifest"))
                    {
                        var manifestInfo = (dynamic) JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(file));

                        ModInfo reconstruction = new ModInfo();


                        try
                        {
                            string version = (string) manifestInfo["objects"]["2"]["version"];
                            string title = (string) manifestInfo["objects"]["2"]["title"];
                            string description = (string) manifestInfo["objects"]["2"]["description"];
                            string thumbUrl = (string) manifestInfo["objects"]["2"]["thumbnailUrl"];
                            int targetPC = -1;
                            int targetAndroid = -1;

                            string networkerString = "";

                            int modId = 0;

                            foreach (var target in manifestInfo["objects"]["2"]["targets"])
                            {
                                string targetManifest = target.ToString();
                                if (targetManifest.Contains("networker"))
                                {
                                    string[] split = targetManifest.Split("\": {");
                                    networkerString = split[0].Replace("\"", "");
                                }
                            }

                            string pcDownloadLink = "";
                            string androidDownloadLink = "";

                            try
                            {

                                targetAndroid = (int) manifestInfo["objects"]["2"]["targets"]["android"]["ref"];

                            }
                            catch (Exception ex)
                            {

                            }

                            try
                            {

                                targetPC = (int) manifestInfo["objects"]["2"]["targets"]["pc"]["ref"];

                            }
                            catch (Exception ex)
                            {

                            }

                            if (targetPC != -1)
                            {
                                int modFileId = (int) manifestInfo["objects"][targetPC.ToString()]["modfileId"];
                                modId = (int) manifestInfo["objects"][targetPC.ToString()]["modId"];
                                pcDownloadLink = $"https://api.mod.io/v1/games/3809/mods/{modId}/files/{modFileId}/download";
                            }
                            if (targetAndroid != -1)
                            {
                                int modFileId = manifestInfo["objects"][targetAndroid.ToString()]["modfileId"];
                                modId = (int) manifestInfo["objects"][targetAndroid.ToString()]["modId"];
                                androidDownloadLink = $"https://api.mod.io/v1/games/3809/mods/{modId}/files/{modFileId}/download";
                            }


                            reconstruction.version = version;
                            reconstruction.thumbnailLink = thumbUrl;
                            reconstruction.modSummary = description;
                            reconstruction.androidDownloadLink = androidDownloadLink;
                            reconstruction.windowsDownloadLink = pcDownloadLink;
                            reconstruction.modId = title;
                            reconstruction.numericalId = modId + "";
                            reconstruction.structureVersion = ModInfo.globalStructureVersion;

                            if (networkerString != "")
                            {
                                reconstruction.PopulateFromInfoString(networkerString);
                            }

                            /*if (reconstruction.IsInstalled())
                            {
                                return;
                            }*/

                            NetworkerMenuController.totalInstalled.Add(reconstruction);
                            installedMods.Add(reconstruction);

                            InstalledModInfo installedModInfo = new InstalledModInfo()
                            {
                                manifestPath = file,
                                palletBarcode = (string) manifestInfo["objects"]["1"]["palletBarcode"],
                                palletPath = (string) manifestInfo["objects"]["1"]["palletPath"],
                                catalogPath = (string) manifestInfo["objects"]["1"]["catalogPath"],
                                ModInfo = reconstruction
                            };

                            InstalledModInfos.Add(installedModInfo);

                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }
            }
        }

        public void OnStartServer()
        {
            ModlistMenu.Refresh(false);
            confirmedHostHasIt = true;
        }

        public void OnDisconnect()
        {
            ModlistMessage.avatarMods.Clear();
            ModlistMenu.Clear();
            confirmedHostHasIt = false;
            modNumericalsDownloadedDuringLobbySession.Clear();
            
            DeleteAllTempMods();
        }

        public override void OnApplicationQuit()
        {
            DeleteAllTempMods();
        }

        public void OnPlayerJoin(PlayerId playerId)
        {
            if (NetworkInfo.HasServer && NetworkInfo.IsServer)
            {
                SendAllMods();
                SendAllAvatars();
            }
        }
        
        public void OnPlayerRepCreated(NetworkPlayer networkPlayer, RigManager manager)
        {
            if (!networkPlayer.NetworkEntity.IsOwner) {
                new AvatarDownloadBar(networkPlayer);
            }
        }

        private void SendAllAvatars()
        {
            foreach (var keyPair in ModlistMessage.avatarMods)
            {
                ModlistData avatarModData = ModlistData.Create(keyPair.Key, keyPair.Value, ModlistData.ModType.AVATAR);
                using (var writer = FusionWriter.Create()) {
                    using (var data = avatarModData) {
                        writer.Write(data);
                        using (var message = FusionMessage.ModuleCreate<ModlistMessage>(writer))
                        {
                            MessageSender.BroadcastMessageExcept(keyPair.Key, NetworkChannel.Reliable, message);
                        }
                    }
                }
            }
        }

        private void SendAllMods()
        {
            int index = 0;
            foreach (ModInfo id in subscribedMods)
            {
                bool final = index == subscribedMods.Count - 1;
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
            
            if (!File.Exists(MODIO_BLACKLIST_TXT_DIRECTORY))
            {
                CreateDefaultBlacklistText(MODIO_BLACKLIST_TXT_DIRECTORY);
            }
        }

        private void CreateDefaultBlacklistText(string directory)
        {
            using (StreamWriter sw = File.CreateText(directory))    
            {    
                sw.WriteLine("#                       ----- WELCOME TO THE MOD.IO BLACKLIST TXT! -----");
                sw.WriteLine("#");
                sw.WriteLine("# This file is where you put mods that you DO NOT want to download under any circumstances.");
                sw.WriteLine("# If you want to blacklist a mod, simply put the mod ID in this file, and it will not be downloaded.");
                sw.WriteLine("# You can find the mod ID by going to the mod.io page for the mod, and looking at the URL.");
                sw.WriteLine("# The mod ID is the name at the end of the URL.");
                sw.WriteLine("# For example, if the URL is https://mod.io/g/bonelab/m/remove-bodylog-transform-vfx, the mod ID is remove-bodylog-transform-vfx");
                sw.WriteLine("# To blacklist mods, simply put each mod ID on a new line. DO NOT START YOUR LINES WITH #, as this will comment out the line.");
                sw.WriteLine("# Ex. ");
                sw.WriteLine("# remove-bodylog-transform-vfx");
                sw.WriteLine("# my-awesome-replacer");
                sw.WriteLine("# annoying-mod");
            }
        }

        private void CreateDefaultAuthText(string directory)
        {
            using (StreamWriter sw = File.CreateText(directory))    
            {    
                sw.WriteLine("#                       ----- WELCOME TO THE MOD.IO AUTH TXT! -----");
                sw.WriteLine("#");
                sw.WriteLine("# Put your mod.io OAuth token in this file, and it will be used to download mods from the mod.io network.");
                sw.WriteLine("# Your OAuth token can be found here: https://mod.io/me/access");
                sw.WriteLine("# At the bottom, you should see a section called 'OAuth Access'");
                sw.WriteLine("# Create a key, then create a token using the + Icon. call it whatever you'd like, this doesnt matter.");
                sw.WriteLine("# Then create a token, call it whatever you'd like, this doesnt matter.");
                sw.WriteLine("# The token is pretty long, so make sure you copy the entire thing. Make sure you're copying the token, not the key.");
                sw.WriteLine("# Once you've copied the token, paste it in this file, replacing the text labeled REPLACE_THIS_TEXT_WITH_YOUR_TOKEN.");
                sw.WriteLine("AuthToken=REPLACE_THIS_TEXT_WITH_YOUR_TOKEN");
            }   
        }
        
        private List<string> ReadBlacklist()
        {
            // Read the file and get the AuthToken= line
            string[] lines = File.ReadAllLines(MODIO_BLACKLIST_TXT_DIRECTORY);
            List<string> blacklist = new List<string>();
            foreach (string line in lines)
            {
                if (!line.StartsWith("#") && line != "")
                {
                    blacklist.Add(line.Trim());
                }
            }
            
            return blacklist;
        }

        public static void WriteLineToBlacklist(string line) {
            using (StreamWriter file = new StreamWriter(MODIO_BLACKLIST_TXT_DIRECTORY, true))
            {
                file.WriteLine(line);
            }
        }

        public static void RemoveLineFromBlacklist(string line)
        {
            var tempFilePath = Path.GetTempFileName();
            using (var sr = new StreamReader(MODIO_BLACKLIST_TXT_DIRECTORY))
            using (var sw = new StreamWriter(tempFilePath))
            {
                string currentLine;
                while ((currentLine = sr.ReadLine()) != null)
                {
                    if (currentLine != line)
                    {
                        sw.WriteLine(currentLine);
                    }
                }
            }
            File.Delete(MODIO_BLACKLIST_TXT_DIRECTORY);
            File.Move(tempFilePath, MODIO_BLACKLIST_TXT_DIRECTORY);
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