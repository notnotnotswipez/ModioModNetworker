using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BoneLib;
using Il2CppSLZ.Marrow.Forklift.Model;
using Il2CppSystem.IO;
using LabFusion.Player;
using LabFusion.Representation;
using LabFusion.Utilities;
using MelonLoader;
using ModIoModNetworker.Ui;
using Newtonsoft.Json;
using UnityEngine;

namespace ModioModNetworker.Data
{
    public class ModInfoThreadRequest
    {
        public string modId;
        public string json;
        public string destination;
        public Action<ModInfo> onInfo;
        public bool mature = false;
        public dynamic originalModInfo;
    }

    public class ModInfo
    {
        public bool isValidMod;
        public bool downloading;
        public bool mature;
        public bool isTracked = true;
        public string modId;
        public float fileSizeKB;
        public string thumbnailLink;
        public string modName;
        public string modSummary;
        public string fileName;
        public string windowsDownloadLink = "nothing";
        public string androidDownloadLink = "nothing";
        public string directDownloadLink = "nothing";
        public double modDownloadPercentage;
        public string numericalId;
        public string version = "0.0.0";
        public List<string> tags = new List<string>();
        public string author = "Networker";
        public int structureVersion = 0;
        public bool temp = false;

        private static Action onFinished;
        public static int globalStructureVersion = 4;
        public static float requestSize = 0;
        public static ConcurrentQueue<ModInfoThreadRequest> modInfoThreadRequests = new ConcurrentQueue<ModInfoThreadRequest>();

        public bool Download()
        {
            if (isValidMod)
            {
                if (!downloading && !ModFileManager.isDownloading)
                {
                    ModFileManager.isDownloading = true;
                    ModlistMenu.activeDownloadModInfo = this;
                    ModFileManager.downloadingModId = modId;
                    if (!HelperMethods.IsAndroid())
                    {
                        ModFileManager.DownloadFile(windowsDownloadLink, MelonUtils.GameDirectory + "\\temp.zip");
                    }
                    else
                    {
                        ModFileManager.DownloadFile(androidDownloadLink, Path.Combine(Application.persistentDataPath, "temp.zip"));
                    }

                    return true;
                }
            }

            return false;
        }

        public bool IsTracked()
        {
            return isTracked;
        }

        public bool IsBlacklisted() {
            if (MainClass.blacklistedModIoIds.Contains(modId) || MainClass.blacklistedModIoIds.Contains(numericalId))
            {
                return true;
            }
            return false;
        }

        public static void HandleQueue()
        {
            // Handle mod info requests
            if (modInfoThreadRequests.Count > 0)
            {
                ModInfoThreadRequest request;
                if (modInfoThreadRequests.TryDequeue(out request))
                {
                    requestSize--;
                    Make(request.modId, request.json, request.destination, request.originalModInfo, request.mature);
                    if (requestSize == 0)
                    {
                        onFinished?.Invoke();
                        onFinished = null;
                    }
                }
            }
        }
        
        public bool IsSubscribed()
        {
            return MainClass.subscribedModIoNumericalIds.Contains(numericalId);
        }

        public bool IsInstalled()
        {
            bool isInstalled = false;
            foreach (var mod in MainClass.installedMods)
            {
                if (mod.numericalId == numericalId)
                {
                    isInstalled = true;
                    break;
                }
            }
            /*foreach (var mod in MainClass.untrackedInstalledModInfos)
            {
                if (mod.modNumericalId == numericalId)
                {
                    isInstalled = true;
                    break;
                }
            }*/

            return isInstalled;
        }

        public ModListing ToModListing() {
            ModListing modListing = new ModListing();
            modListing.Author = "ModIoModNetworker";
            modListing.Title = modId;
            modListing.Description = modSummary;
            modListing.ThumbnailUrl = thumbnailLink;
            modListing.Version = version;
   
            modListing.Targets = new StringModTargetListingDictionary();

            ModIOModTarget modTarget = new ModIOModTarget();
            modTarget.ThumbnailOverride = null;
            
            modTarget.GameId = 3809;
            modTarget.ModId = long.Parse(numericalId);
            modTarget.ModfileId = long.Parse(windowsDownloadLink.Split("/files/")[1].Replace("/download", ""));

            modListing.Targets.Add("pc", modTarget);

            ModIOModTarget androidTarget = new ModIOModTarget();
            androidTarget.ThumbnailOverride = null;

            androidTarget.GameId = 3809;
            androidTarget.ModId = long.Parse(numericalId);
            androidTarget.ModfileId = long.Parse(androidDownloadLink.Split("/files/")[1].Replace("/download", ""));

            modListing.Targets.Add("android", androidTarget);

            string infoString = $"networker;{mature};{temp};{fileSizeKB};{fileName};{structureVersion};{modName};{tags.Count}";
            foreach (var tag in tags) {
                infoString += ";" + tag;
            }
            modListing.Targets.Add(infoString, modTarget);

            return modListing;

        }

        public void PopulateFromInfoString(string targetString)
        {
            string[] split = targetString.Split(";");
            mature = bool.Parse(split[1]);
            temp = bool.Parse(split[2]);
            fileSizeKB = float.Parse(split[3]);
            fileName = split[4];
            structureVersion = int.Parse(split[5]);
            modName = split[6];
            int tagCount = int.Parse(split[7]);
            int target = 8;
            for (int i = 0; i < tagCount; i++) {
                tags.Add(split[i + target]);
            }
            isValidMod = true;
        }

        public static void SetFinishedAction(Action action)
        {
            onFinished = action;
        }

        public static void RequestModInfo(string modId, string destination)
        {
            ModFileManager.GetJson("@" + modId, (json) =>
            {
                modInfoThreadRequests.Enqueue(new ModInfoThreadRequest()
                    { modId = modId, json = json, destination = destination });
            });
        }

        public static void RequestModInfoNumerical(string modIdNumerical, string destination)
        {

            if (modIdNumerical == null)
            {
                MelonLogger.Msg("Mod ID Numerical was null, skipping");
                return;
            }

            ModFileManager.GetRawModInfoJson(modIdNumerical, (totalModInfo) =>
            {
                if ((object)totalModInfo == null)
                {
                    return;
                }

                string modId = (string) totalModInfo["name_id"];
                bool mature = ((int) totalModInfo["maturity_option"]) > 0;
                ModFileManager.GetJson("@" + modId, (json) =>
                {
                    modInfoThreadRequests.Enqueue(new ModInfoThreadRequest()
                    {
                        modId = modId, json = json, destination = destination, mature = mature,
                        originalModInfo = totalModInfo
                    });
                });
            });
        }

        public static ModInfo MakeFromDynamic(dynamic mod, string modId)
        {
            ModInfo modInfo = new ModInfo();
            modInfo.structureVersion = globalStructureVersion;
            modInfo.modId = modId;

            modInfo.isValidMod = true;
            modInfo.downloading = false;
            
            modInfo.fileSizeKB = (float)mod["filesize"];
            modInfo.fileName = (string)mod["filename"];
            
            // They get properly set later
            modInfo.windowsDownloadLink = (string)mod["download"]["binary_url"];
            modInfo.version = (string)mod["version"];

            return modInfo;
        }

        public static void Make(string modId, string json, string destination, dynamic originalModInfo, bool mature = false)
        {
            ModInfo modInfo = new ModInfo();
            modInfo.structureVersion = globalStructureVersion;
            modInfo.modId = modId;
            modInfo.mature = mature;
            Action<ModInfo> action = new Action<ModInfo>((info =>
            {

            }));

            if (destination == "menuinfos")
            {
                action = new Action<ModInfo>((info =>
                {
                    ModlistMenu._modInfos.Add(info);
                }));
            }
            else if (destination == "spotlight") {

                action = new Action<ModInfo>((info =>
                {
                    NetworkerMenuController.spotlightOverride.downloadedInfo = info;
      

                    if (NetworkerMenuController.spotlightOverride.cachedThumbnail) {
                        GameObject.Destroy(NetworkerMenuController.spotlightOverride.cachedThumbnail);
                    }

                    NetworkerMenuController.spotlightOverride.cachedThumbnail = null;
                }));

                
            }
            else if (destination == "install_level") {
                action = new Action<ModInfo>((info =>
                {
                    if (MainClass.modNumericalsDownloadedDuringLobbySession.Contains(info.numericalId)) {
                        return;
                    }

                    if (info.IsSubscribed())
                    {
                        return;
                    }

                    if (MainClass.tempLobbyMods)
                    {
                        info.temp = true;
                    }

                    float mb = modInfo.fileSizeKB / 1000000;
                    float gb = mb / 1000;

                    if (gb < MainClass.levelMaxGb)
                    {

                        if (ModFileManager.AddToQueue(new DownloadQueueElement()
                        {
                            associatedPlayer = null,
                            info = info,
                            notify = true
                        }))
                        {
                            MainClass.modNumericalsDownloadedDuringLobbySession.Add(info.numericalId);
                        }
                    }
                }));
            }
            else if (destination == "install_spawnable")
            {
                action = new Action<ModInfo>((info =>
                {
                    if (info.IsSubscribed())
                    {
                        return;
                    }

                    if (MainClass.modNumericalsDownloadedDuringLobbySession.Contains(info.numericalId))
                    {
                        return;
                    }

                    if (MainClass.tempLobbyMods) {
                        info.temp = true;
                    }


                    float mb = modInfo.fileSizeKB / 1000000;

                    if (mb < MainClass.maxAutoDownloadMb) {
                        if (ModFileManager.AddToQueue(new DownloadQueueElement()
                        {
                            associatedPlayer = null,
                            info = info,
                            notify = true
                        }))
                        {
                            MainClass.modNumericalsDownloadedDuringLobbySession.Add(info.numericalId);
                        }
                    }
                }));
            }
            else if (destination == "install_native")
            {
                action = new Action<ModInfo>((info =>
                {
                    ModFileManager.AddToQueue(new DownloadQueueElement()
                    {
                        associatedPlayer = null,
                        info = info,
                        notify = true
                    });
                }));
            }
            else if (destination.StartsWith("install_avatar"))
            {
                string id = destination.Split(';')[1];
                byte idByte = byte.Parse(id);
                PlayerId playerId = PlayerIdManager.GetPlayerId(idByte);
                if (playerId != null) {
                    action = new Action<ModInfo>((info =>
                    {

                        if (info.IsSubscribed())
                        {
                            return;
                        }

                        if (MainClass.modNumericalsDownloadedDuringLobbySession.Contains(info.numericalId))
                        {
                            return;
                        }

                        if (MainClass.tempLobbyMods)
                        {
                            info.temp = true;
                        }

                        float mb = modInfo.fileSizeKB / 1000000;
                        if (mb < MainClass.maxAutoDownloadMb)
                        {
                            if (ModFileManager.AddToQueue(new DownloadQueueElement()
                            {
                                associatedPlayer = playerId,
                                info = info,
                                notify = false
                            }))
                            {
                                MainClass.modNumericalsDownloadedDuringLobbySession.Add(info.numericalId);
                            }
                        }
                    }));
                }
            }

            try
            {
                var jsonData = JsonConvert.DeserializeObject<dynamic>(json);


                // Get data array and loop through it
                dynamic data = jsonData["data"];

                if (data == null)
                {
                    modInfo.isValidMod = false;
                    action.Invoke(modInfo);
                    return;
                }

                modInfo.isValidMod = true;
                modInfo.downloading = false;

                dynamic foundMod = null;
                dynamic antiPlatformMod = null;

                string prevVersion = "0.0.0";
                string prevAntiVersion = "0.0.0";


                foreach (var mod in data)
                {
                    dynamic platforms = mod["platforms"];
                    bool validMainPlatform = false;
                    bool validAntiPlatformMod = false;
                    foreach (var supported in platforms)
                    {
                        // This mod is currently tied to Fusion which is PC only atm, but its good to future proof
                        //
                        // 7/18/23 - HAHAHA :)
                        bool isAndroid = HelperMethods.IsAndroid();
                        string currentPlatform = isAndroid ? "android" : "windows";
                        string antiPlatform = isAndroid ? "windows" : "android";
                        
                        if ((string)supported["platform"] == currentPlatform)
                        {
                            validMainPlatform = true;
                        }
                        else if ((string)supported["platform"] == antiPlatform)
                        {
                            validMainPlatform = false;
                            break;
                        }
                    }
                    
                    foreach (var supported in platforms)
                    {
                        // This mod is currently tied to Fusion which is PC only atm, but its good to future proof
                        //
                        // 7/18/23 - HAHAHA :)
                        bool isAndroid = HelperMethods.IsAndroid();
                        string currentPlatform = isAndroid ? "android" : "windows";
                        string antiPlatform = isAndroid ? "windows" : "android";
                        
                        if ((string)supported["platform"] == antiPlatform)
                        {
                            validAntiPlatformMod = true;
                        }
                        else if ((string)supported["platform"] == currentPlatform)
                        {
                            validAntiPlatformMod = false;
                            break;
                        }
                    }

                    if (!validMainPlatform && !validAntiPlatformMod)
                    {
                        continue;
                    }

                    if (validMainPlatform)
                    {
                        string modVersion = "" + (string)mod["version"];
                        // Compare versions x.x.x
                        if (modVersion.CompareTo(prevVersion) > 0 || foundMod == null)
                        {
                            foundMod = mod;
                            prevVersion = modVersion;
                        }
                    }

                    if (validAntiPlatformMod)
                    {
                        string modVersion = "" + (string)mod["version"];
                        if (modVersion.CompareTo(prevAntiVersion) > 0 || antiPlatformMod == null)
                        {
                            antiPlatformMod = mod;
                            prevAntiVersion = modVersion;
                        }
                    }
                }

                if (foundMod != null)
                {
                    if (originalModInfo != null)
                    {
                        // Apply the info we got from the get mod request. This has stuff like the numerical id and the thumbnail link.
                        string modUrl = (string)originalModInfo["profile_url"];
                        string numericalId = "" + (string)originalModInfo["id"];
                        string modTitle = (string)originalModInfo["name"];
                        string summary = (string)originalModInfo["summary"];
                        string thumbnailLink = (string)originalModInfo["logo"]["thumb_640x360"];
                     
                        modInfo.modName = modTitle;
                        modInfo.thumbnailLink = thumbnailLink;
                        modInfo.modSummary = summary;
                        modInfo.numericalId = numericalId;
                        modInfo.author = (string) originalModInfo["submitted_by"]["username"];

                        foreach (var tag in originalModInfo["tags"]) {
                            modInfo.tags.Add((string)tag["name"]);
                        }
                    }


                    modInfo.fileSizeKB = (float)foundMod["filesize"];

                    if (!HelperMethods.IsAndroid())
                    {
                        modInfo.windowsDownloadLink = (string)foundMod["download"]["binary_url"];
                        if (antiPlatformMod != null)
                        {
                            modInfo.androidDownloadLink = (string)antiPlatformMod["download"]["binary_url"];
                        }
                    }
                    else
                    {
                        modInfo.androidDownloadLink = (string)foundMod["download"]["binary_url"];
                        if (antiPlatformMod != null)
                        {
                            modInfo.windowsDownloadLink = (string)antiPlatformMod["download"]["binary_url"];
                        }
                    }


                    modInfo.fileName = (string)foundMod["filename"];

                    modInfo.version = "" + (string)foundMod["version"];

                    if (HelperMethods.IsAndroid())
                    {
                        if (antiPlatformMod != null)
                        {
                            // Just resorting to the windows version cause I don't really wanna deal with managing and comparing cross platform versions when people call their mods things like
                            // AWESOMENESS UPDATE - QUEST and AWESOMENESS UPDATE - PC
                            modInfo.version = "" + (string)antiPlatformMod["version"];
                        }
                    }

                    if (modInfo.version == null)
                    {
                        modInfo.version = "0.0.0";
                    }
                }
                else
                {
                    modInfo.isValidMod = false;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error(e);
                modInfo.isValidMod = false;
                action.Invoke(modInfo);
                return;
            }
            
            action.Invoke(modInfo);
        }
    }
}