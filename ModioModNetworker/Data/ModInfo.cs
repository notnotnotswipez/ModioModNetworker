using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using BoneLib;
using LabFusion.Utilities;
using MelonLoader;

namespace ModioModNetworker.Data
{
    public class ModInfoThreadRequest
    {
        public string modId;
        public string json;
        public string destination;
    }

    public class ModInfo
    {
        public bool isValidMod;
        public bool downloading;
        public bool mature;
        public string modId;
        public float fileSizeKB;
        public string fileName;
        public string directDownloadLink;
        public double modDownloadPercentage;
        public string version = "0.0.0";
        public int structureVersion = 0;
        public bool temp = false;

        private static Action onFinished;
        public static int globalStructureVersion = 1;
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
                    ModFileManager.DownloadFile(directDownloadLink, MelonUtils.GameDirectory + "\\temp.zip");
                    return true;
                }
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
                    Make(request.modId, request.json, request.destination);
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
            return MainClass.subscribedModIoIds.Contains(modId);
        }

        public bool IsInstalled()
        {
            bool isInstalled = false;
            foreach (var mod in MainClass.installedMods)
            {
                if (mod.modId == modId)
                {
                    isInstalled = true;
                    break;
                }
            }

            return isInstalled;
        }

        public static void SetFinishedAction(Action action)
        {
            onFinished = action;
        }

        public static void RequestModInfo(string modId, string destination)
        {
            Thread thread = new Thread(() =>
            {
                string json = ModFileManager.GetJson("@"+modId);
                modInfoThreadRequests.Enqueue(new ModInfoThreadRequest()
                    { modId = modId, json = json, destination = destination });
            });
            thread.Start();
        }

        public static ModInfo MakeFromDynamic(dynamic mod, string modId)
        {
            ModInfo modInfo = new ModInfo();
            modInfo.structureVersion = globalStructureVersion;
            modInfo.modId = modId;

            modInfo.isValidMod = true;
            modInfo.downloading = false;
            
            modInfo.fileSizeKB = mod["filesize"];
            modInfo.fileName = mod["filename"];
            modInfo.directDownloadLink = mod["download"]["binary_url"];
            modInfo.version = mod["version"];

            return modInfo;
        }

        public static void Make(string modId, string json, string destination)
        {
            ModInfo modInfo = new ModInfo();
            modInfo.structureVersion = globalStructureVersion;
            modInfo.modId = modId;
            Action<ModInfo> action;
            if (destination == "menuinfos")
            {
                action = new Action<ModInfo>((info =>
                {
                    ModlistMenu._modInfos.Add(info);
                }));
            }
            else
            {
                action = new Action<ModInfo>((info =>
                {
                    
                }));
            }

            try
            {
                JavaScriptSerializer parser = new JavaScriptSerializer();
                var jsonData = parser.Deserialize<dynamic>(json);
                // Get data array and loop through it
                var data = jsonData["data"];

                if (data == null)
                {
                    modInfo.isValidMod = false;
                    action.Invoke(modInfo);
                    return;
                }

                modInfo.isValidMod = true;
                modInfo.downloading = false;

                dynamic foundMod = null;

                string prevVersion = "0.0.0";

                foreach (var mod in data)
                {
                    dynamic platforms = mod["platforms"];
                    bool validMod = false;
                    foreach (var supported in platforms)
                    {
                        // This mod is currently tied to Fusion which is PC only atm, but its good to future proof
                        bool isAndroid = HelperMethods.IsAndroid();
                        string currentPlatform = isAndroid ? "android" : "windows";
                        string antiPlatform = isAndroid ? "windows" : "android";
                        
                        if (supported["platform"] == currentPlatform)
                        {
                            validMod = true;
                        }
                        else if (supported["platform"] == antiPlatform)
                        {
                            validMod = false;
                            break;
                        }
                    }

                    if (!validMod) continue;

                    string modVersion = "" + mod["version"];
                    // Compare versions x.x.x
                    if (modVersion.CompareTo(prevVersion) > 0 || foundMod == null)
                    {
                        foundMod = mod;
                        prevVersion = modVersion;
                    }
                }

                if (foundMod != null)
                {
                    modInfo.fileSizeKB = foundMod["filesize"];
                    modInfo.directDownloadLink = foundMod["download"]["binary_url"];
                    modInfo.fileName = foundMod["filename"];
                    modInfo.version = ""+foundMod["version"];
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
                modInfo.isValidMod = false;
                action.Invoke(modInfo);
                return;
            }
            
            action.Invoke(modInfo);
        }
    }
}