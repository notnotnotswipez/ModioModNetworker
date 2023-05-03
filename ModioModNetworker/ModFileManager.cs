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
using System.Web.WebPages;
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

        public static DownloadAction activeDownloadAction = null;

        private static WebClient _client;

        public static void Initialize()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            _client = new WebClient();
            _client.DownloadProgressChanged += OnDownloadProgressChanged;
            _client.DownloadFileCompleted += OnDownloadFileCompleted;
            _client.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);
        }

        private static void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs args)
        {
            activeDownloadAction = new DownloadAction(10);
        }

        public static string FindFile(string path, string fileName)
        {
            // Make a recursive function to find the file
            foreach (var file in Directory.GetFiles(path))
            {
                string[] splitPath = file.Split('\\');
                string currentFileName = splitPath[splitPath.Length - 1];
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

            MelonLogger.Msg("Added: " + modInfo.modId + " to install queue");
            queue.Add(modInfo);
        }

        public static void DownloadFile(string url, string path)
        {
            downloadPath = path;
            // Check if file exists
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            try
            {
                _client.DownloadFileAsync(new Uri(url), path);

            }
            catch (WebException e)
            {
                isDownloading = false;
                ModlistMenu.activeDownloadModInfo = null;
                MelonLogger.Error("Failed to download file: " + e.Message);
                throw;
            }
        }

        public static void QueueSubscriptions(int shown)
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
                    client.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);
                    MainClass.subscriptionThreadString =
                        client.DownloadString("https://api.mod.io/v1/me/subscribed?_offset=" + shown + "&limit=400");
                    fetchingSubscriptions = false;
                }
                catch (WebException e)
                {
                    fetchingSubscriptions = false;
                    MelonLogger.Error("Failed to get subscriptions: " + e.Message);
                }
            });
            thread.Start();
        }



        public static bool Subscribe(string modId)
        {
            string url = "https://api.mod.io/v1/games/3809/mods/@" + modId + "/subscribe";

            // Make https request to subscribe
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);
            request.ContentType = "application/x-www-form-urlencoded";
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.Created)
                {
                    MelonLogger.Msg("Subscribed to mod: " + modId);
                    return true;
                }
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

            string url = "https://api.mod.io/v1/games/3809/mods/@" + modId + "/subscribe";

            // Make https request to unsubscribe
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "DELETE";
            request.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);
            request.ContentType = "application/x-www-form-urlencoded";
            // Send request
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    MelonLogger.Msg("Unsubscribed from mod: " + modId);
                }
            }
            catch (WebException e)
            {
                MelonLogger.Error("Failed to unsubscribe from mod: " + modId);
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
                client.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);
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

    public class DownloadAction
    {
        public int delayFrames;

        public DownloadAction(int delayFrames)
        {
            this.delayFrames = delayFrames;
        }

        public bool Check()
        {
            if (delayFrames > 0)
            {
                delayFrames--;
                return false;
            }

            return true;
        }

        public void Handle()
        {
            try
            {
                string exportDirectory = ModFileManager.MOD_FOLDER_PATH.Replace("/", "\\") + "\\" + "tempfolder";
                if (Directory.Exists(exportDirectory))
                {
                    Directory.Delete(exportDirectory, true);
                }

                Directory.CreateDirectory(exportDirectory);

                // Unzip using memory stream
                using (ZipArchive archive = ZipFile.OpenRead(ModFileManager.downloadPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string path = Path.Combine(exportDirectory, entry.FullName);
                        if (entry.FullName.EndsWith("/"))
                        {
                            // Strip last slash
                            path = path.Substring(0, path.Length - 1);
                            Directory.CreateDirectory(path);
                            continue;
                        }
                        else
                        {
                            // Make sure directory exists
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                        }

                        entry.ExtractToFile(path, true);
                    }
                }

                // Pull the first folder out of the zip
                string palletJsonFile = ModFileManager.FindFile(exportDirectory, "pallet.json");

                while (!palletJsonFile.IsEmpty())
                {
                    string modFolder = palletJsonFile.Replace("\\pallet.json", "");
                    // Make folder in mods folder named after the first directory
                    string[] split = modFolder.Split('\\');
                    string modFolderName = split[split.Length - 1];
                    string modFolderDestination = ModFileManager.MOD_FOLDER_PATH + "/" + modFolderName;
                    // Make directory in mods folder
                    JavaScriptSerializer parser = new JavaScriptSerializer();

                    bool existing = false;

                    if (Directory.Exists(modFolderDestination))
                    {
                        existing = true;
                        string existingPalletJson = ModFileManager.FindFile(modFolderDestination, "pallet.json");
                        string fileContents = File.ReadAllText(existingPalletJson);
                        var jsonData = parser.Deserialize<dynamic>(fileContents);
                        string palletId = jsonData["objects"]["o:1"]["barcode"];
                        MainClass.warehousePalletReloadTargets.Add(palletId);
                        Directory.Delete(modFolderDestination, true);
                    }

                    if (!existing)
                    {
                        MainClass.warehouseReloadFolders.Add(modFolderDestination);
                    }

                    Directory.Move(modFolder, modFolderDestination);
                    // Create modinfo.json
                    string modInfoPath = modFolderDestination + "/modinfo.json";
                    string modInfoJson = parser.Serialize(ModlistMenu.activeDownloadModInfo);
                    File.WriteAllText(modInfoPath, modInfoJson);

                    palletJsonFile = ModFileManager.FindFile(exportDirectory, "pallet.json");
                    MelonLogger.Msg(modFolder+" Downloaded and extracted!");
                }

                // Delete the zip
                File.Delete(ModFileManager.downloadPath);
                // Delete loose folder
                Directory.Delete(exportDirectory);
                MainClass.warehouseReloadRequested = true;
                MainClass.refreshInstalledModsRequested = true;
                MainClass.subsChanged = true;
                
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error while downloading mod {ModlistMenu.activeDownloadModInfo.modId}: " + e);
                ModFileManager.isDownloading = false;
                ModlistMenu.activeDownloadModInfo = null;

            }
        }
    }
}

