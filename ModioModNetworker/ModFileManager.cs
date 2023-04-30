using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using LabFusion.Utilities;
using MelonLoader;
using ModioModNetworker.Data;
using SLZ.Marrow.Warehouse;
using UnityEngine;

namespace ModioModNetworker
{
    public class ModFileManager
    {
        public static string OAUTH_KEY = "";
        public static string API_PATH = "https://api.mod.io/v1/games/3809/mods/";

        public static string MOD_FOLDER_PATH = Application.persistentDataPath + "/Mods";
        
        public static string downloadingModId = "";
        public static string downloadPath = "";
        
        public static bool isDownloading = false;

        public static bool queueAvailable = false;
        private static List<ModInfo> queue = new List<ModInfo>();
        
        public static bool fetchingSubscriptions = false;

        private static WebClient _client;

        public static void Initialize()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            _client = new WebClient();
            _client.DownloadProgressChanged += OnDownloadProgressChanged;
            _client.DownloadFileCompleted += OnDownloadFileCompleted;
            _client.Headers.Add("Authorization", "Bearer "+OAUTH_KEY);
        }

        private static void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs args)
        {
            // Unzip contents
            string exportDirectory = MOD_FOLDER_PATH +"/"+downloadingModId;
            if (Directory.Exists(exportDirectory))
            {
                Directory.Delete(exportDirectory, true);
            }

            ZipFile.ExtractToDirectory(downloadPath, exportDirectory);
            // Pull the first folder out of the zip
            string palletJsonFile = FindFile(exportDirectory, "pallet.json");
            string modFolder = palletJsonFile.Replace("\\pallet.json", "");
            // Make folder in mods folder named after the first directory
            string[] split = modFolder.Split('\\');
            string modFolderName = split[split.Length-1];
            string modFolderDestination = MOD_FOLDER_PATH + "/" + modFolderName;
            // Make directory in mods folder
            JavaScriptSerializer parser = new JavaScriptSerializer();
            
            if (Directory.Exists(modFolderDestination))
            {

                foreach (var file in Directory.GetFiles(modFolderDestination))
                {
                    // Get the file name
                    string[] splitPath = file.Split('\\');
                    string fileName = splitPath[splitPath.Length-1];
                    if (fileName == "pallet.json")
                    {
                        // Read file contents
                        string fileContents = File.ReadAllText(file);
                        var jsonData = parser.Deserialize<dynamic>(fileContents);
                        string palletId = jsonData["objects"]["o:1"]["barcode"];
                        MainClass.warehousePalletReloadTarget = palletId;
                    }

                    File.Delete(file);
                }

                // SDK mods dont go 2 levels deep so theres no reason to make some recursive function.
                foreach (var internalDirectory in Directory.GetDirectories(modFolderDestination))
                {
                    foreach (var file in Directory.GetFiles(internalDirectory))
                    {
                        File.Delete(file);
                    }

                    Directory.Delete(internalDirectory);
                }
                Directory.Delete(modFolderDestination);
            }
            Directory.Move(modFolder, modFolderDestination);
            // Create modinfo.json
            string modInfoPath = modFolderDestination + "/modinfo.json";
            string modInfoJson = parser.Serialize(ModlistMenu.activeDownloadModInfo);
            File.WriteAllText(modInfoPath, modInfoJson);
            // Delete the zip
            File.Delete(downloadPath);
            // Delete loose folder
            Directory.Delete(exportDirectory);
            MainClass.warehouseReloadRequested = true;
            MainClass.warehouseTargetFolder = modFolderDestination;
            MainClass.refreshInstalledModsRequested = true;
            MainClass.subsChanged = true;
        }
        
        private static string FindFile(string path, string fileName)
        {
            // Make a recursive function to find the file
            foreach (var file in Directory.GetFiles(path))
            {
                string[] splitPath = file.Split('\\');
                string currentFileName = splitPath[splitPath.Length-1];
                if (currentFileName == fileName)
                {
                    return file;
                }
            }
            // Check subdirectories
            foreach (var directory in Directory.GetDirectories(path))
            {
                string result = FindFile(directory, fileName);
                if (result != "")
                {
                    return result;
                }
            }

            return "";
        }

        private static void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (ModlistMenu.activeDownloadModInfo != null)
            {
                ModlistMenu.activeDownloadModInfo.modDownloadPercentage = e.ProgressPercentage;
            }
        }
        
        public static void CheckQueue()
        {
            if (!isDownloading)
            {
                if (queue.Count > 0)
                {
                    ModInfo modInfo = queue[0];
                    queue.RemoveAt(0);
                    modInfo.Download();
                    MainClass.menuRefreshRequested = true;
                }
            }
        }
        
        public static void AddToQueue(ModInfo modInfo)
        {
            // Check if mod is in installed mods
            if (!modInfo.isValidMod)
            {
                return;
            }

            bool isInstalled = false;
            bool outdated = false;
            foreach (var mod in MainClass.installedMods)
            {
                if (mod.modId == modInfo.modId)
                {
                    isInstalled = true;
                    if (mod.version != modInfo.version)
                    {
                        outdated = true;
                    }
                    break;
                }
            }

            if (isInstalled && !outdated)
            {
                return;
            }

            // Check if mod is already in queue
            foreach (var mod in queue)
            {
                if (mod.modId == modInfo.modId)
                {
                    return;
                }
            }
            MelonLogger.Msg("Added: "+modInfo.modId+" to install queue");
            queue.Add(modInfo);
        }

        public static void DownloadFile(string url, string path)
        {
            downloadPath = path;
            try
            {
                _client.DownloadFileAsync(new Uri(url), path);
                
            }
            catch (WebException e)
            {
                isDownloading = false;
                ModlistMenu.activeDownloadModInfo = null;
                MelonLogger.Error("Failed to download file: "+e.Message);
                throw;
            }
        }
        
        public static void QueueSubscriptions()
        {
            if (fetchingSubscriptions)
            {
                return;
            }
            fetchingSubscriptions = true;

            Thread thread = new Thread(() =>
            {
                try
                {
                    WebClient client = new WebClient();
                    // Prevent SecureChannelFailure error
                    
                    
                    client.Headers.Add("Authorization", "Bearer "+OAUTH_KEY);
                    MainClass.subscriptionThreadString = client.DownloadString("https://api.mod.io/v1/me/subscribed");
                }
                catch (WebException e)
                {
                    fetchingSubscriptions = false;
                    MelonLogger.Error("Failed to get subscriptions: "+e.Message);
                }
            });
            thread.Start();
        }
        
        

        public static bool Subscribe(string modId)
        {
            string url = "https://api.mod.io/v1/games/3809/mods/@"+modId+"/subscribe";
            
            // Make https request to unsubscribe
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.Headers.Add("Authorization", "Bearer "+OAUTH_KEY);
            request.ContentType = "application/x-www-form-urlencoded";
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.Created)
                {
                    MelonLogger.Msg("Subscribed to mod: " + modId);
                    return true;
                }
                MelonLogger.Error("Failed to subscribe to mod: " + modId);
            }
            catch (WebException e)
            {
                MelonLogger.Error("Failed to subscribe to mod: " + modId);
                MelonLogger.Error(e.Message);
            }

            return false;
        }

        public static void UninstallAndUnsubscribe(string modId)
        {
            InstalledModInfo installedModInfo = null;
            foreach (var modInfo in MainClass.InstalledModInfos)
            {
                if (modInfo.modId == modId)
                {
                    installedModInfo = modInfo;
                }
            }

            if (installedModInfo != null)
            {
                AssetWarehouse.Instance.UnloadPallet(installedModInfo.palletBarcode);
                // Delete mod folder
                string modJsonPath = installedModInfo.palletJsonPath;
                // Get the directory containing the modinfo.json
                string folder = modJsonPath.Replace("\\modinfo.json", "");
                Directory.Delete(folder, true);
            }
            string url = "https://api.mod.io/v1/games/3809/mods/@"+modId+"/subscribe";
            
            // Make https request to unsubscribe
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "DELETE";
            request.Headers.Add("Authorization", "Bearer "+OAUTH_KEY);
            request.ContentType = "application/x-www-form-urlencoded";
            // Send request
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    MelonLogger.Msg("Unsubscribed from mod: "+modId);
                }
            }
            catch (WebException e)
            {
                MelonLogger.Error("Failed to unsubscribe from mod: "+modId);
                MelonLogger.Error(e.Message);
            }

            MainClass.refreshInstalledModsRequested = true;
        }

        public static string GetJson(string mod)
        {
            string json = "";
            try
            {
                // Make new client
                WebClient client = new WebClient();
                // Add auth header
                client.Headers.Add("Authorization", "Bearer "+OAUTH_KEY);
                // Make an https request to the mod.io api
                string link = API_PATH + mod + "/files";
                json = client.DownloadString(link);
            }
            catch (Exception e)
            {
                // Ignore
            }
            
            
            return json;
        }
    }
}