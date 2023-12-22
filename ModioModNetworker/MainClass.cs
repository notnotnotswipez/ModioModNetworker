using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using BoneLib;
using BoneLib.BoneMenu.Elements;
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
using ModioModNetworker.Repo;
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

namespace ModioModNetworker
{
    public struct ModioModNetworkerUpdaterVersion
    {
        public const string versionString = "2.1.1";
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
        
        // TODO: REPO INFO
        public static List<RepoModInfo> untrackedInstalledModInfos = new List<RepoModInfo>();

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
        public static MelonPreferences_Entry<bool> useRepoConfig;
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

        public static List<string> modNumericalsDownloadedDuringLobbySession = new List<string>();
        
        private static int subsShown = 0;
        private static int subTotal = 0;
        
        public bool palletLock = false;

        public static bool confirmedHostHasIt = false;
        private static bool loadedInstalled = false;

        public static bool handlingInstalled = false;
        public static bool handlingSubscribed = false;
        
        
        // TRACKING VARIABLES
        
        public static bool sceneStreamerLoaded = false;
        public static bool assetWarehouseLoaded = false;
        
        public static StreamStatus lastStreamStatus = StreamStatus.DONE;

        public override void OnInitializeMelon()
        {
           

            melonPreferencesCategory = MelonPreferences.CreateCategory("ModioModNetworker");
            melonPreferencesCategory.SetFilePath(MelonUtils.UserDataDirectory+"/ModioModNetworker.cfg");
            modsDirectory =
                melonPreferencesCategory.CreateEntry<string>("ModDirectoryPath",
                    Application.persistentDataPath + "/Mods");
            autoDownloadAvatarsConfig = melonPreferencesCategory.CreateEntry<bool>("AutoDownloadAvatars", true);
            autoDownloadSpawnablesConfig = melonPreferencesCategory.CreateEntry<bool>("AutoDownloadSpawnables", true);
            autoDownloadLevelsConfig = melonPreferencesCategory.CreateEntry<bool>("AutoDownloadLevels", false);
            maxLevelAutoDownloadGbConfig = melonPreferencesCategory.CreateEntry<float>("MaxLevelAutoDownloadGb", 1f);
            tempLobbyModsConfig = melonPreferencesCategory.CreateEntry<bool>("TemporaryLobbyMods", false, null, "If set to true, lobby mods like (avatars/spawnables/levels) that got auto downloaded will be deleted when you leave the lobby.");
            useRepoConfig = melonPreferencesCategory.CreateEntry<bool>("UseRepoConfig", true, null, "If set to true, ModIoModNetworker will scan the repos available at https://blrepo.laund.moe/ for the mod.io IDs. This means it should find the proper mods automatically in any lobby, even if the host/other players do not have networker.");
            maxAutoDownloadMbConfig = melonPreferencesCategory.CreateEntry<float>("MaxAutoDownloadMb", 500f);
            downloadMatureContentConfig = melonPreferencesCategory.CreateEntry<bool>("DownloadMatureContent", false);
            
            maxAutoDownloadMb = maxAutoDownloadMbConfig.Value;
            autoDownloadAvatars = autoDownloadAvatarsConfig.Value;
            downloadMatureContent = downloadMatureContentConfig.Value;
            autoDownloadSpawnables = autoDownloadSpawnablesConfig.Value;
            autoDownloadLevels = autoDownloadLevelsConfig.Value;
            tempLobbyMods = tempLobbyModsConfig.Value;
            levelMaxGb = maxLevelAutoDownloadGbConfig.Value;
            useRepo = useRepoConfig.Value;
            
            ModFileManager.MOD_FOLDER_PATH = modsDirectory.Value;
            
            RepoManager.LoadBarcodePairsFromRepos();

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
            if (auth == "")
            {
                MelonLogger.Error("---------------- IMPORTANT ERROR ----------------");
                MelonLogger.Error("AUTH TOKEN NOT FOUND IN auth.txt.");
                MelonLogger.Error("MODIONETWORKER WILL NOT RUN! PLEASE FOLLOW THE INSTRUCTIONS LOCATED IN auth.txt!");
                MelonLogger.Error("You can find the auth.txt file in the ModIoModNetworker folder in your game directory.");
                MelonLogger.Error("-------------------------------------------------");
                return;
            }

            if (auth.Length < 50)
            {
                MelonLogger.Error("---------------- IMPORTANT ERROR ----------------");
                MelonLogger.Error("The AUTH TOKEN in auth.txt is invalid. AKA. It is not correct. It is probably the key.");
                MelonLogger.Error("Please follow the instructions in auth.txt to get your auth token.");
                MelonLogger.Error("-------------------------------------------------");
                return;
            }


            ModFileManager.OAUTH_KEY = auth;
            MelonLogger.Msg("Registered on mod.io with auth key!");

            MelonLogger.Msg("Loading internal module...");
            ModuleHandler.LoadModule(Assembly.GetExecutingAssembly());
            ModFileManager.Initialize();
            ModlistMenu.Initialize();
            MultiplayerHooking.OnPlayerJoin += OnPlayerJoin;
            MultiplayerHooking.OnDisconnect += OnDisconnect;
            MultiplayerHooking.OnStartServer += OnStartServer;
            MultiplayerHooking.OnPlayerRepCreated += OnPlayerRepCreated;
            MultiplayerHooking.OnLobbyCategoryCreated += OnLobbyCategoryMade;
            
            MelonLogger.Msg("Populating currently installed mods via this mod.");
            installedMods.Clear();
            InstalledModInfos.Clear();
            PopulateInstalledMods(ModFileManager.MOD_FOLDER_PATH);
            loadedInstalled = true;
            MelonLogger.Msg("Checking mod.io account subscriptions");
            PopulateSubscriptions();
            ModFileManager.QueueTrending(0);
            
            melonPreferencesCategory.SaveToFile();
        }

        private void OnLobbyCategoryMade(MenuCategory category, INetworkLobby lobby)
        {
            if (lobby.TryGetMetadata("modionetworker", out var value))
            {
                category.CreateFunctionElement("ModioModNetworker Active On Server", Color.cyan, () => { });
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
                if (!FusionSceneManager.HasLevel(barcode)) {
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
                        
                    
                        RepoModInfo repoModInfo = RepoManager.GetRepoModInfoFromPalletBarcode(RepoManager.GetPalletBarcodeFromCrateBarcode(barcode));
                        if (repoModInfo != null)
                        {
                            // Native install because it was of our own volition
                            ModInfo.RequestModInfoNumerical(repoModInfo.modNumericalId, "install_native");
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
            //Stopwatch stopwatch = Stopwatch.StartNew();
            foreach (var bar in AvatarDownloadBar.bars.Values)
            {
                bar.Update();
            }

            ThumbnailThreader.HandleQueue();
            MainThreadManager.HandleQueue();
            
            LevelHoldQueue.Update();
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Queue updates took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            
            //stopwatch = Stopwatch.StartNew();
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
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Active download queue check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            
            //stopwatch = Stopwatch.StartNew();

            bool loadingThisFrame = false;

            if (!sceneStreamerLoaded)
            {
                if (SceneStreamer._session != null)
                {
                    sceneStreamerLoaded = true;
                }
            }

            if (!assetWarehouseLoaded)
            {
                if (AssetWarehouse.Instance != null)
                {
                    assetWarehouseLoaded = true;
                }
            }

            if (sceneStreamerLoaded)
            {
                lastStreamStatus = SceneStreamer._session.Status;
                
                if (lastStreamStatus == StreamStatus.LOADING)
                {
                    loadingThisFrame = true;
                }
            }
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Setting scene streamer session status took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            //stopwatch = Stopwatch.StartNew();
            
            if (ModFileManager.activeDownloadAction != null)
            {
                if (ModFileManager.activeDownloadAction.Check())
                {
                    ModFileManager.activeDownloadAction.Handle();
                    ModFileManager.activeDownloadAction = null;
                }
            }
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Active download action check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            
            //stopwatch = Stopwatch.StartNew();
            if (!addedCallback)
            { 
                if (assetWarehouseLoaded)
                {
                    AssetWarehouse.Instance.OnCrateAdded += new Action<string>(s =>
                    {
                        palletLock = false;
                        foreach (var playerRep in PlayerRepManager.PlayerReps)
                        {
                            // Use reflection to get the _isAvatarDirty bool from the playerRep
                            // This is so we can force the playerRep to update the avatar, just incase they had an avatar that we didnt previously have.
                            var isAvatarDirty = playerRep.GetType().GetField("_isAvatarDirty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            isAvatarDirty.SetValue(playerRep, true);
                        }
                        SpawnableHoldQueue.CheckValid(s);
                        LevelHoldQueue.CheckValid(s);
                    });
                    addedCallback = true;
                    DeleteAllTempMods();
                }
            }
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Crate added callback check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            
            //stopwatch = Stopwatch.StartNew();

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
                    

                    // If there are outdated remaining ones, that means they come from an older version of networker where they were installed but
                    // Never subscribed to. We have to pull the new information from the repo instead.
                    
                    // TODO: REPO INFO
                    foreach (var outOfDateInfo in outOfDateModInfos)
                    {
                        RepoModInfo repoModInfo = RepoManager.GetRepoModInfoFromPalletBarcode(outOfDateInfo.palletBarcode);
                        if (repoModInfo != null)
                        {
                            // This is just for tracking purposes, we remake a "fake" one when its done
                            ModInfo modInfo = new ModInfo()
                            { 
                                modSummary = repoModInfo.summary,
                                modName = repoModInfo.modName,
                                thumbnailLink = repoModInfo.thumbnailLink,
                                numericalId = repoModInfo.modNumericalId,
                                mature = outOfDateInfo.ModInfo.mature,
                                windowsDownloadLink = outOfDateInfo.ModInfo.directDownloadLink
                            };
                            UpdateModInfo(modInfo, outOfDateInfo);
                        }
                    }
                    outOfDateModInfos.Clear();
                }
            }
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Subs refreshing check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            
            //stopwatch = Stopwatch.StartNew();

            if (ModFileManager.activeDownloadWebRequest != null)
            {
                ModFileManager.OnDownloadProgressChanged(ModFileManager.activeDownloadWebRequest.downloadProgress * 100);
            }
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Active download web request check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            
            //stopwatch = Stopwatch.StartNew();

            if (warehouseReloadRequested && assetWarehouseLoaded && AssetWarehouse.Instance._initialLoaded && !palletLock)
            {
                bool update = false;
                if (warehousePalletReloadTargets.Count > 0)
                {
                    AssetWarehouse.Instance.ReloadPallet(warehousePalletReloadTargets[0]);
                    warehousePalletReloadTargets.RemoveAt(0);
                    update = true;
                }

                if (warehouseReloadFolders.Count > 0)
                {
                    AssetWarehouse.Instance.LoadPalletFromFolderAsync(warehouseReloadFolders[0], true);
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
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Warehouse reload check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            
            //stopwatch = Stopwatch.StartNew();
            
            
            ModInfo.HandleQueue();
            ModFileManager.CheckQueue();
            
            //stopwatch.Stop();
            //MelonLogger.Msg("ModInfo queue check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            
            //stopwatch = Stopwatch.StartNew();

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
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Modlist message queue check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            
            //stopwatch = Stopwatch.StartNew();

            if (subsChanged)
            {
                subsChanged = false;
                if (NetworkInfo.HasServer && NetworkInfo.IsServer)
                {
                    SendAllMods();
                }
            }
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Subs changed check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            //stopwatch = Stopwatch.StartNew();

            if (menuRefreshRequested)
            {
                if (loadingThisFrame)
                {
                    return;
                }

                ModlistMenu.Refresh(true);
                menuRefreshRequested = false;
            }
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Menu refresh check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            //stopwatch = Stopwatch.StartNew();

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
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Refresh subscribed mods check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            //stopwatch = Stopwatch.StartNew();

            if (refreshInstalledModsRequested && !handlingSubscribed && !handlingInstalled)
            {
                installedMods.Clear();
                InstalledModInfos.Clear();
                
                // TODO: REPO INFO
                untrackedInstalledModInfos.Clear();
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
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Refresh installed mods check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            //stopwatch = Stopwatch.StartNew();

            if (subscriptionThreadString != "")
            {
                InternalPopulateSubscriptions();
            }
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Internal populate subscriptions check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
            
            //stopwatch = Stopwatch.StartNew();
            if (trendingThreadString != "")
            {
                InternalPopulateTrending();
                if (NetworkerMenuController.instance) {
                    NetworkerMenuController.instance.OnNewTrendingRecieved();
                }
            }
            
            //stopwatch.Stop();
            //MelonLogger.Msg("Internal populate trending check took "+stopwatch.Elapsed.TotalMilliseconds+"ms");
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
                string modInfoPath = installed.modinfoJsonPath;
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

        public static void ReceiveSubModInfo(ModInfo modInfo)
        {
            InstalledModInfo outOfDate = null;
            if (modInfo.version == null)
            {
                modInfo.version = "0.0.0";
            }

            foreach (var outOfDateInfo in outOfDateModInfos)
            {
                if (outOfDateInfo.modId == modInfo.modId)
                {
                    outOfDate = outOfDateInfo;
                    UpdateModInfo(modInfo, outOfDateInfo);
                }
            }

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
                foreach (var platform in modEntry["platforms"])
                {
                    if (((string)platform["platform"]) == "windows")
                    {
                        int desired = (int) platform["modfile_live"];
                        windowsId = desired;
                        break;
                    }
                }

                foreach (var platform in modEntry["platforms"])
                {
                    if (((string) platform["platform"]) == "android")
                    {
                        int desired = (int) platform["modfile_live"];
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

                if (((int) modEntry["status"]) == 3)
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

                if (modInfo.mature && !downloadMatureContent) {
                    return;
                }

                NetworkerMenuController.modIoRetrieved.Add(modInfo);
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
            try {

                // Sort by latest
                latestDirectories = new DirectoryInfo(directory).GetDirectories()
                                                  .OrderByDescending(f => f.LastWriteTime)
                                                  .ToList();
            }
            catch (Exception ex)
            {
                // Ignore, something just went wrong in a phase where we cannot do anything
            }
            

            foreach (var subDirectory in latestDirectories)
            {
                PopulateInstalledMods(subDirectory.FullName);
            }
            try {
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
                            palletId = (string) jsonData["objects"]["o:1"]["barcode"];
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

                            if (installedAlready) {
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
                    }

                    if (!foundModInfo) {
                        // TODO: REPO INFO
                        // This means this is NOT a networker tracked mod, but with the repo we can mangle it to be one.
                        foreach (var alreadyInstalled in InstalledModInfos)
                        {
                            if (alreadyInstalled.palletBarcode == palletId)
                            {
                                return;
                            }
                        }

                        RepoModInfo repoModInfo = RepoManager.GetRepoModInfoFromPalletBarcode(palletId);
                        if (repoModInfo != null) {
                            repoModInfo.palletJsonPath = palletJsonPath;
                            ModInfo falseReconstruction = new ModInfo()
                            {
                                modName = repoModInfo.modName,
                                isTracked = false,
                                thumbnailLink = repoModInfo.thumbnailLink,
                                modSummary = repoModInfo.summary,
                                numericalId = repoModInfo.modNumericalId
                            };
                      
                            NetworkerMenuController.totalInstalled.Add(falseReconstruction);
                            untrackedInstalledModInfos.Add(repoModInfo);
                        }
                    }
                }
            }
            catch {
                MelonLogger.Error("Got an error while parsing installed mod at: " + directory);
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
        
        public void OnPlayerRepCreated(RigManager manager)
        {
            if (PlayerRepManager.TryGetPlayerRep(manager, out var rep))
            {
                new AvatarDownloadBar(rep);
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