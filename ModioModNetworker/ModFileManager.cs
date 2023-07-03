using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Web.WebPages;
using LabFusion.Representation;
using LabFusion.Utilities;
using MelonLoader;
using ModioModNetworker.Data;
using ModioModNetworker.Repo;
using ModioModNetworker.UI;
using ModIoModNetworker.Ui;
using SLZ.Marrow.SceneStreaming;
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
        private static List<DownloadQueueElement> queue = new List<DownloadQueueElement>();

        public static bool fetchingSubscriptions = false;
        public static bool fetchingTrending = false;

        public static DownloadAction activeDownloadAction = null;
        public static DownloadQueueElement activeDownloadQueueElement = null;

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

            try
            {
                MelonLogger.Msg("Download complete");
                MelonLogger.Msg("Creating export directory");
                string exportDirectory = ModFileManager.MOD_FOLDER_PATH.Replace("/", "\\") + "\\" + "tempfolder";
                if (Directory.Exists(exportDirectory))
                {
                    Directory.Delete(exportDirectory, true);
                }

                Directory.CreateDirectory(exportDirectory);
                MelonLogger.Msg("Export directory created!");
                MelonLogger.Msg("Unzipping...");
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
                MelonLogger.Msg("Finished unzipping");

                MelonLogger.Msg("Finding pallet.json files");

                // Pull the first folder out of the zip
                string palletJsonFile = ModFileManager.FindFile(exportDirectory, "pallet.json");

                while (!palletJsonFile.IsEmpty())
                {
                    MelonLogger.Msg("Found pallet.json file");
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

                    MelonLogger.Msg("Moving mod folder...");
                    Directory.Move(modFolder, modFolderDestination);
                    MelonLogger.Msg("Finished moving mod folder...");
                    // Create modinfo.json
                    string modInfoPath = modFolderDestination + "/modinfo.json";
                    string modInfoJson = parser.Serialize(ModlistMenu.activeDownloadModInfo);
                    MelonLogger.Msg("Writing modinfo.json...");
                    File.WriteAllText(modInfoPath, modInfoJson);
                    MelonLogger.Msg("Wrote modinfo.json!");
                    // Add to installed mods
                    palletJsonFile = ModFileManager.FindFile(exportDirectory, "pallet.json");
                    MelonLogger.Msg(modFolder + " Downloaded and extracted!");
                }

                MelonLogger.Msg("Deleting temp.zip");
                // Delete the zip
                File.Delete(ModFileManager.downloadPath);
                MelonLogger.Msg("temp.zip deleted!");
                // Delete loose folder
                MelonLogger.Msg("Deleting loose folder!");
                Directory.Delete(exportDirectory, true);
                MelonLogger.Msg("Loose folder deleted!");
               
                MainClass.warehouseReloadRequested = true;
                MainClass.refreshInstalledModsRequested = true;
                MainClass.subsChanged = true;
                _client.Dispose();
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error while downloading mod {ModlistMenu.activeDownloadModInfo.modId}: " + e);
                ModFileManager.isDownloading = false;
                ModFileManager.activeDownloadQueueElement = null;
                ModlistMenu.activeDownloadModInfo = null;
                _client.Dispose();
            }
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
        
        public static void StopDownload()
        {
            if (isDownloading)
            {
                _client.CancelAsync();
                if (activeDownloadQueueElement != null)
                {
                    if (activeDownloadQueueElement.associatedPlayer != null)
                    {
                        if (AvatarDownloadBar.bars.TryGetValue(activeDownloadQueueElement.associatedPlayer, out var bar))
                        {
                            bar.Finish();
                        }
                    }
                }
                isDownloading = false;
                activeDownloadQueueElement = null;
                ModlistMenu.activeDownloadModInfo = null;
            }
        }

        public static void CheckQueue()
        {
            if (!isDownloading)
            {
                if (AssetWarehouse.Instance == null)
                {
                    return;
                }

                if (SceneStreamer._session == null)
                {
                    return;
                }

                if (SceneStreamer._session.Status == StreamStatus.LOADING)
                {
                    return;
                }

                if (queue.Count > 0)
                {
                    DownloadQueueElement queueElement = queue[0];
                    if (queueElement.info.Download())
                    {
                        queue.RemoveAt(0);
                        activeDownloadQueueElement = queueElement;
                        MelonLogger.Msg("Downloading mod " + queueElement.info.modId);
                        if (activeDownloadQueueElement.associatedPlayer != null)
                        {
                            if (AvatarDownloadBar.bars.TryGetValue(activeDownloadQueueElement.associatedPlayer, out var bar))
                            {
                                bar.Show();
                            }
                        }
                    }
                    MainClass.menuRefreshRequested = true;
                }
            }
        }

        public static bool AddToQueue(DownloadQueueElement queueElement)
        {
            ModInfo modInfo = queueElement.info;
            // Check if mod is in installed mods
            if (!modInfo.isValidMod)
            {
                return false;
            }

            if (MainClass.blacklistedModIoIds.Contains(modInfo.modId))
            {
                return false;
            }

            if (activeDownloadQueueElement != null)
            {
                if (activeDownloadQueueElement.info.modId == modInfo.modId)
                {
                    return false;
                }
            }

            if (modInfo.mature && !MainClass.downloadMatureContent) {
                return false;
            }
            
            if (modInfo.version == null)
            {
                modInfo.version = "0.0.0";
            }

            bool isInstalled = false;
            bool outdated = false;
            foreach (var mod in MainClass.installedMods)
            {
                if (mod.numericalId == modInfo.numericalId || mod.modId == modInfo.modId)
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
                return false;
            }

            // Check if mod is already in queue
            foreach (var mod in queue)
            {
                if (mod.info.modId == modInfo.modId || mod.info.numericalId == modInfo.numericalId)
                {
                    return false;
                }
            }

            MelonLogger.Msg("Added: " + modInfo.modId + " to install queue");
            queue.Add(queueElement);
            return true;
        }

        public static async Task DownloadFileAsync(string url, string path)
        {
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
            request.Method = "GET";
            request.ContentType = "application/octet-stream";
            request.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);

            HttpWebResponse response = await request.GetResponseAsync() as HttpWebResponse;

            if (response.StatusCode == HttpStatusCode.OK)
            {
                using (Stream stream = response.GetResponseStream())
                {
                    using (FileStream fs = new FileStream(path, FileMode.CreateNew))
                    {
                        await stream.CopyToAsync(fs);

                    }
                }
            }
            else
            {
                MelonLogger.Error("Failed to download file");
            }
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
                activeDownloadQueueElement = null;
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
                    HttpWebRequest httpWebRequest = (HttpWebRequest) WebRequest.Create("https://api.mod.io/v1/me/subscribed?_offset=" + shown + "&limit=400");
                    httpWebRequest.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);
                    HttpWebResponse httpWebresponse = (HttpWebResponse) httpWebRequest.GetResponse();
                    StreamReader streamReader = new StreamReader(httpWebresponse.GetResponseStream());
                    string result = streamReader.ReadToEnd();

                    MainClass.subscriptionThreadString = result;

                    httpWebresponse.Close();
                    streamReader.Close();

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

        public static void QueueTrending(int offset, string searchQuery = "") {

            if (fetchingTrending)
            {
                return;
            }

            fetchingTrending = true;

            Thread thread = new Thread(() =>
            {
                try
                {
                    string extension = "&_q="+searchQuery;
                    if (searchQuery.IsEmpty()) {
                        extension = "";
                    }
                    HttpWebRequest httpWebRequest = (HttpWebRequest) WebRequest.Create($"https://mod.io/v1/games/@bonelab/mods?_limit=100&_offset={offset}&_sort=-popular"+ extension);
                    httpWebRequest.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);
                    HttpWebResponse httpWebresponse = (HttpWebResponse) httpWebRequest.GetResponse();
                    StreamReader streamReader = new StreamReader(httpWebresponse.GetResponseStream());
                    string result = streamReader.ReadToEnd();

                    MainClass.trendingThreadString = result;

                    httpWebresponse.Close();
                    streamReader.Close();

                    fetchingTrending = false;

                }
                catch (WebException e)
                {
                    fetchingTrending = false;
                    MelonLogger.Error("Failed to get trending mods: " + e.Message);
                }
            });
            thread.Start();
        }



        public static bool Subscribe(string numericalid)
        {
            string url = "https://api.mod.io/v1/games/3809/mods/" + numericalid + "/subscribe";

            // Make https request to subscribe
            Thread thread = new Thread(() =>
            {
                HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
                request.Method = "POST";
                request.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);
                request.ContentType = "application/x-www-form-urlencoded";

                try
                {
                    HttpWebResponse response = (HttpWebResponse) request.GetResponse();
                    if (response.StatusCode == HttpStatusCode.Created)
                    {
                        response.Close();
                        MainThreadManager.QueueAction(() =>
                        {
                            if (NetworkerMenuController.instance)
                            {
                                NetworkerMenuController.instance.UpdateModPopupButtons();
                            }
                        });
                    }
                    else
                    {
                        response.Close();
                    }
                }
                catch (WebException e)
                {
                    MelonLogger.Error("Failed to subscribe to mod: " + numericalid);
                    MelonLogger.Error(e.Message);
                }
            });
            thread.Start();

            return false;
        }

        public static void UninstallAndUnsubscribe(string modId)
        {
            UnInstall(modId);
            UnSubscribe(modId);
        }

        public static void UnSubscribe(string numericalId)
        {
            string url = "https://api.mod.io/v1/games/3809/mods/" + numericalId + "/subscribe";
            MainClass.subscribedModIoNumericalIds.Remove(numericalId);
            // Make https request to unsubscribe

            Thread thread = new Thread(() =>
            {
                HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
                request.Method = "DELETE";
                request.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);
                request.ContentType = "application/x-www-form-urlencoded";
                // Send request
                try
                {
                    HttpWebResponse response = (HttpWebResponse) request.GetResponse();

                    if (response.StatusCode == HttpStatusCode.NoContent)
                    {
                        response.Close();
                        MainThreadManager.QueueAction(() =>
                        {
                            if (NetworkerMenuController.instance)
                            {
                                NetworkerMenuController.instance.UpdateModPopupButtons();
                            }
                        });
                    }
                    else
                    {
                        response.Close();
                    }
                }
                catch (WebException e)
                {
                    MelonLogger.Error("Failed to unsubscribe from mod: " + numericalId);
                    MelonLogger.Error(e.Message);
                }
            });

            thread.Start();
        }

        public static void UnInstall(string numericalId)
        {
            InstalledModInfo installedModInfo = null;
            RepoModInfo repoModInfo = null;

            foreach (var modInfo in MainClass.InstalledModInfos)
            {
                if (modInfo.ModInfo.numericalId == numericalId)
                {
                    installedModInfo = modInfo;
                }
            }

            foreach (var repoInfo in MainClass.untrackedInstalledModInfos)
            {
                if (repoInfo.modNumericalId == numericalId)
                {
                    repoModInfo = repoInfo;
                }
            }

            Thread thread = new Thread(() =>
            {
                if (installedModInfo != null)
                {
                    string barcode = installedModInfo.palletBarcode;
                    MainThreadManager.QueueAction(() =>
                    {
                        
                        AssetWarehouse.Instance.UnloadPallet(barcode);
                    });
                    
                    // Delete mod folder
                    string modJsonPath = installedModInfo.modinfoJsonPath;
                    // Get the directory containing the modinfo.json
                    string folder = modJsonPath.Replace("\\modinfo.json", "");
                    Directory.Delete(folder, true);
                }

                if (repoModInfo != null)
                {
                    string barcode = repoModInfo.palletBarcode;
                    MainThreadManager.QueueAction(() =>
                    {
                        AssetWarehouse.Instance.UnloadPallet(barcode);
                    });
                    
                    // Delete mod folder
                    string palletJsonPath = repoModInfo.palletJsonPath;
                    // Get the directory containing the modinfo.json
                    string folder = palletJsonPath.Replace("\\pallet.json", "");
                    Directory.Delete(folder, true);
                }

                MainClass.refreshInstalledModsRequested = true;
            });

            thread.Start();
        }

        public static string GetJson(string mod)
        {
            string json = "";
            try
            {
                // Make an https request to the mod.io api
                string link = API_PATH + mod + "/files";
                HttpWebRequest httpWebRequest = (HttpWebRequest) WebRequest.Create(link);
                httpWebRequest.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);
                HttpWebResponse httpWebresponse = (HttpWebResponse) httpWebRequest.GetResponse();
                StreamReader streamReader = new StreamReader(httpWebresponse.GetResponseStream());
                string result = streamReader.ReadToEnd();
                httpWebresponse.Close();
                streamReader.Close();

                json = result;
            }
            catch (Exception e)
            {
                // Ignore
            }


            return json;
        }

        public static dynamic GetRawModInfoJson(string mod)
        {
            string json = "";
            try
            {
                // Make new client
                WebClient client = new WebClient();
                // Add auth header
                client.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);
                // Make an https request to the mod.io api
                string link = API_PATH + mod;
                json = client.DownloadString(link);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error when fetching raw mod info for {mod}: ");
                MelonLogger.Error(e);
            }

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            dynamic dynamicJson = serializer.Deserialize<dynamic>(json);


            return dynamicJson;
        }
    }
}

public class DownloadQueueElement
{
    public PlayerId associatedPlayer;
    public ModInfo info;
    public bool notify = true;
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
        /*Thread otherThread = new Thread(() =>
        {
            try
            {

        });
        otherThread.Start();*/
    }
}

