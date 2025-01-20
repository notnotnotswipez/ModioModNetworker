using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BoneLib;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.SceneStreaming;
using Il2CppSLZ.Marrow.Warehouse;
using LabFusion.Player;
using LabFusion.Representation;
using LabFusion.Utilities;
using MelonLoader;
using ModioModNetworker;
using ModioModNetworker.Data;

using ModioModNetworker.UI;
using ModIoModNetworker.Ui;
using Newtonsoft.Json;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.Networking;
using static Il2CppSLZ.Marrow.UnityExtensions.TransformExtensions;
using AsyncOperation = UnityEngine.AsyncOperation;

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
        public static UnityWebRequest activeDownloadWebRequest;

        public static string[] targetVersionStrings = { "1.1" , "1.2"};

        //private static WebClient _client;

        public static void Initialize()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            
            /*_client = new WebClient();
            _client.DownloadProgressChanged += OnDownloadProgressChanged;
            _client.DownloadFileCompleted += OnDownloadFileCompleted;
            _client.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);*/
        }

        private static void OnDownloadFileCompleted()
        {
            activeDownloadAction = new DownloadAction(10);
        }
       

        public static string FindFile(string path, string fileName)
        {
            try {
                foreach (var file in Directory.GetFiles(path))
                {
                    string[] splitPath = file.Split('\\');
                    string currentFileName = splitPath[splitPath.Length - 1];
                    if (currentFileName.EndsWith(fileName))
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
            }

            catch (Exception ex) {
                return "";
            }
            return "";
        }

        public static void OnDownloadProgressChanged(double progress)
        {
            if (ModlistMenu.activeDownloadModInfo != null)
            {
                ModlistMenu.activeDownloadModInfo.modDownloadPercentage = progress;
            }
        }
        
        public static void StopDownload()
        {
            if (isDownloading)
            {
                //_client.CancelAsync();
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
                activeDownloadWebRequest = null;
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

        public static bool AddToQueue(DownloadQueueElement queueElement, bool ignoreTag = false)
        {
            ModInfo modInfo = queueElement.info;

            // Check if mod is in installed mods
            if (!modInfo.isValidMod)
            {
                return false;
            }

            if (MainClass.blacklistedModIoIds.Contains(modInfo.modId) || MainClass.blacklistedModIoIds.Contains(modInfo.numericalId))
            {
                return false;
            }

            // Dont overwrite subscribed mods
            if (modInfo.IsSubscribed())
            {
                return false;
            }

            if (!ignoreTag) {
                bool contains = false;
                foreach (var tag in modInfo.tags) {
                    if (targetVersionStrings.Contains(tag)) {
                        contains = true;
                    }
                }
                if (!contains) {
                    return false;
                }
            }
            

            if (activeDownloadQueueElement != null)
            {
                if (activeDownloadQueueElement.info.modId == modInfo.modId || activeDownloadQueueElement.info.numericalId == modInfo.numericalId)
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

            queue.Add(queueElement);
            return true;
        }

        /*public static void DownloadFileUnityWeb(string url, string path)
        {
            UnityWebRequest request = UnityWebRequest.Get(url);
            request.method = "GET";
            request.downloadHandler = new DownloadHandlerFile(path);
            
            activeDownloadWebRequest = request;

            request.SetRequestHeader("Authorization", "Bearer " + OAUTH_KEY);
            var sponse = request.SendWebRequest();
            sponse.m_completeCallback += new Action<AsyncOperation>((op) =>
            {
                OnDownloadFileCompleted();
            });
        }*/

        public static async void DownloadFileHttpClient(string url, string path)
        {
            var handler = new HttpClientHandler();
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ServerCertificateCustomValidationCallback =
                (httpRequestMessage, cert, cetChain, policyErrors) =>
                {
                    return true;
                };

            using (HttpClient client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + OAUTH_KEY);

                using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
                {
                    long totalBytes = response.Content.Headers.ContentLength.Value;
                    long bytesRead = 0;
                    byte[] buffer = new byte[4096];
                    int bytesReceived;

                    using (FileStream fs = new FileStream(path, FileMode.CreateNew))
                    {
                        while ((bytesReceived = await streamToReadFrom.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fs.WriteAsync(buffer, 0, bytesReceived);
                            bytesRead += bytesReceived;

                            double percentage = (double) bytesRead / totalBytes * 100;
                            OnDownloadProgressChanged(percentage);
                        }
                    }
                }

                OnDownloadFileCompleted();
            }
        }

        public static async Task DownloadFileAsync(string url, string path)
        {
            /*HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
            request.Method = "GET";
            request.ContentType = "application/octet-stream";
            request.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);

            HttpWebResponse response = await request.GetResponseAsync() as HttpWebResponse;

            if (response.StatusCode == HttpStatusCode.OK)
            {
                using (Stream stream = response.GetResponseStream())
                {
                    long totalBytes = response.ContentLength;
                    long bytesRead = 0;
                    byte[] buffer = new byte[4096];
                    int bytesReceived;

                    using (FileStream fs = new FileStream(path, FileMode.CreateNew))
                    {
                        while ((bytesReceived = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fs.WriteAsync(buffer, 0, bytesReceived);
                            bytesRead += bytesReceived;

                            double percentage = (double) bytesRead / totalBytes * 100;
                            OnDownloadProgressChanged(percentage);
                        }

                    }
                }

                OnDownloadFileCompleted();
            }
            else
            {
                MelonLogger.Error("Failed to download file");
            }*/
            DownloadFileHttpClient(url, path);
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
                DownloadFileAsync(url, path);
            }
            catch (WebException e)
            {
                isDownloading = false;
                ModlistMenu.activeDownloadModInfo = null;
                activeDownloadQueueElement = null;
                activeDownloadWebRequest = null;
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
            
            UnityWebRequest httpWebRequest = UnityWebRequest.Get("https://api.mod.io/v1/me/subscribed?_offset=" + shown + "&limit=400");
            httpWebRequest.SetRequestHeader("Authorization", "Bearer " + OAUTH_KEY);
            var requestSent = httpWebRequest.SendWebRequest();
            requestSent.m_completeCallback += new Action<AsyncOperation>((asyncOperation) =>
            {
                MainClass.subscriptionThreadString = httpWebRequest.downloadHandler.text;
                fetchingSubscriptions = false;
            });

            /*Thread thread = new Thread(() =>
            {
                try
                {


                    if (httpWebRequest.result == UnityWebRequest.Result.ConnectionError || httpWebRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        return;
                    }
                    
                    HttpWebRequest httpWebRequest = (HttpWebRequest) WebRequest.Create("https://api.mod.io/v1/me/subscribed?_offset=" + shown + "&limit=400");
                    httpWebRequest.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);
                    HttpWebResponse httpWebresponse = (HttpWebResponse) httpWebRequest.GetResponse();
                    StreamReader streamReader = new StreamReader(httpWebresponse.GetResponseStream());
                    string result = streamReader.ReadToEnd();
                    MainClass.subscriptionThreadString = result;
                    
                    //MainClass.subscriptionThreadString = httpWebRequest.downloadHandler.text;

                    //httpWebresponse.Close();
                    //streamReader.Close();

                    fetchingSubscriptions = false;

                }
                catch (WebException e)
                {
                    fetchingSubscriptions = false;
                    MelonLogger.Error("Failed to get subscriptions: " + e.Message);
                }
            });
            thread.Start();*/
        }

        public static void QueueTrending(int offset, string searchQuery = "") {

            if (fetchingTrending)
            {
                return;
            }

            fetchingTrending = true;
            
            string extension = "&_q="+searchQuery;
            if (searchQuery == "") {
                extension = "";
            }

            SpotlightOverride.LoadFromRegularURL();
            
            UnityWebRequest httpWebRequest = UnityWebRequest.Get($"https://mod.io/v1/games/@bonelab/mods?_limit=100&_offset={offset}&_sort=-popular"+ extension);
            httpWebRequest.SetRequestHeader("Authorization", "Bearer " + OAUTH_KEY);
            var requestSent = httpWebRequest.SendWebRequest();
            requestSent.m_completeCallback += new Action<AsyncOperation>((asyncOperation) =>
            {
                MainClass.trendingThreadString = httpWebRequest.downloadHandler.text;
                fetchingTrending = false;
            });

            /*Thread thread = new Thread(() =>
            {
                try
                {
                    string extension = "&_q="+searchQuery;
                    if (searchQuery == "") {
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
            thread.Start();*/
        }



        public static bool Subscribe(string numericalid)
        {
            string url = "https://api.mod.io/v1/games/3809/mods/" + numericalid + "/subscribe";
            
            UnityWebRequest httpWebRequest = UnityWebRequest.Get(url);
            httpWebRequest.method = "POST";
            httpWebRequest.SetRequestHeader("Authorization", "Bearer " + OAUTH_KEY);
            httpWebRequest.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            var requestSent = httpWebRequest.SendWebRequest();
            requestSent.m_completeCallback += new Action<AsyncOperation>((asyncOperation) =>
            {
                if (httpWebRequest.responseCode == 201)
                {
                    MainThreadManager.QueueAction(() =>
                    {
                        if (NetworkerMenuController.instance)
                        {
                            NetworkerMenuController.instance.UpdateModPopupButtons();
                        }
                    });
                }
            });

            /*// Make https request to subscribe
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
            thread.Start();*/

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
            
            UnityWebRequest httpWebRequest = UnityWebRequest.Get(url);
            httpWebRequest.method = "DELETE";
            httpWebRequest.SetRequestHeader("Authorization", "Bearer " + OAUTH_KEY);
            httpWebRequest.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            var requestSent = httpWebRequest.SendWebRequest();
            requestSent.m_completeCallback += new Action<AsyncOperation>((asyncOperation) =>
            {
                MainThreadManager.QueueAction(() =>
                {
                    if (NetworkerMenuController.instance)
                    {
                        MainClass.subscribedModIoNumericalIds.Remove(numericalId);
                        NetworkerMenuController.instance.UpdateModPopupButtons();
                    }
                });
            });
            
            // Make https request to unsubscribe

            /*Thread thread = new Thread(() =>
            {
                HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
                request.Method = "DELETE";
                request.Headers.Add("Authorization", "Bearer " + OAUTH_KEY);
                request.ContentType = "application/x-www-form-urlencoded";
                // Send request
                try
                {
                    HttpWebResponse response = (HttpWebResponse) request.GetResponse();

                    MainThreadManager.QueueAction(() =>
                    {
                        if (NetworkerMenuController.instance)
                        {
                            MainClass.subscribedModIoNumericalIds.Remove(numericalId);
                            NetworkerMenuController.instance.UpdateModPopupButtons();
                        }
                    });

                    if (response.StatusCode == HttpStatusCode.NoContent)
                    {
                        response.Close();
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

            thread.Start();*/
        }

        public static void UnInstallMainThread(string numericalId)
        {
            InstalledModInfo installedModInfo = null;

            foreach (var modInfo in MainClass.InstalledModInfos)
            {
                if (modInfo.ModInfo.numericalId == numericalId)
                {
                    installedModInfo = modInfo;
                }
            }

            try
            {
                if (installedModInfo != null)
                {
                    string barcode = installedModInfo.palletBarcode;
                    UnloadPallet(barcode);

                    File.Delete(installedModInfo.manifestPath);
                    // Delete mod folder
                    string parentDirectory = Directory.GetParent(installedModInfo.catalogPath).FullName;

                    
                    Directory.Delete(parentDirectory, true);
                    
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("Exception when uninstalling mod: " + ex);
            }


            MainClass.RequestInstallCheck();
        }

        private static void UnloadPallet(string palletBarcode)
        {

            DeleteExistingModObjects(palletBarcode);

            try
            {
                AssetWarehouse.Instance.UnloadPallet(new Barcode(palletBarcode));
            }
            catch (Exception e)
            {
                // AW might not have been loaded? Or some other issue
            }
        }

        public static void DeleteExistingModObjects(string palletBarcode)
        {
            try {
                // Android thing
                foreach (var pallet in AssetWarehouse.Instance.GetPallets())
                {
                    if (pallet._barcode._id != palletBarcode)
                    {
                        continue;
                    }

                    foreach (Crate crate in pallet._crates)
                    {
                        foreach (var assetPool in AssetSpawner._instance._poolList)
                        {
                            if (assetPool._crate._barcode != crate._barcode)
                            {
                                continue;
                            }

                            foreach (var assetPoolee in assetPool._spawned)
                            {
                                GameObject.Destroy(assetPoolee.gameObject);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
            
            }
        }

        public static void UnInstall(string numericalId)
        {
            InstalledModInfo installedModInfo = null;
            
            foreach (var modInfo in MainClass.InstalledModInfos)
            {
                if (modInfo.ModInfo.numericalId == numericalId)
                {
                    installedModInfo = modInfo;
                }
            }


            Thread thread = new Thread(() =>
            {
                try {
                    if (installedModInfo != null)
                    {
                        string barcode = installedModInfo.palletBarcode;
                        MainThreadManager.QueueAction(() =>
                        {
                            UnloadPallet(barcode);
                        });

                        // Delete mod folder
                        File.Delete(installedModInfo.manifestPath);
                        // Delete mod folder
                        string parentDirectory = Directory.GetParent(installedModInfo.catalogPath).FullName;
                        Directory.Delete(parentDirectory, true);

                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error("Exception when uninstalling mod: " + ex);
                }


                MainClass.RequestInstallCheck();
            });

            thread.Start();
        }
        
        public static void GetJson(string mod, Action<string> onCompleted)
        {
            string link = API_PATH + mod + "/files";
            UnityWebRequest httpWebRequest = UnityWebRequest.Get(link);
            httpWebRequest.SetRequestHeader("Authorization", "Bearer " + OAUTH_KEY);
            var requestSent = httpWebRequest.SendWebRequest();
            
            
            requestSent.m_completeCallback += new Action<AsyncOperation>((op) =>
            {
                if (httpWebRequest.result == UnityWebRequest.Result.ConnectionError || httpWebRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError(httpWebRequest.error);
                    onCompleted?.Invoke(null);
                    return;
                }

                string result = httpWebRequest.downloadHandler.text;

                onCompleted?.Invoke(result);
            });
        }

        /*public static string GetJson(string mod, Action<string> onCompleted)
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
        }*/
        
        public static void GetRawModInfoJson(string mod, Action<dynamic> onCompleted)
        {
            string json = "";
            try
            {
                string link = API_PATH + mod;
                UnityWebRequest httpWebRequest = UnityWebRequest.Get(link);
                httpWebRequest.SetRequestHeader("Authorization", "Bearer " + OAUTH_KEY);
                var requestSent = httpWebRequest.SendWebRequest();

                requestSent.m_completeCallback += new Action<AsyncOperation>((op) =>
                {
                    json = httpWebRequest.downloadHandler.text;
                    dynamic dynamicJson = JsonConvert.DeserializeObject<dynamic>(json);
                    if (httpWebRequest.result == UnityWebRequest.Result.ConnectionError || httpWebRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.LogError(httpWebRequest.error);
                        onCompleted?.Invoke(dynamicJson);
                        return;
                    }
                    onCompleted?.Invoke(dynamicJson);
                });
                
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error when fetching raw mod info for {mod}: ");
                MelonLogger.Error(e);
            }
        }

        /*public static dynamic GetRawModInfoJson(string mod, Action<dynamic> onCompleted)
        {
            string json = "";
            try
            {
                string link = API_PATH + mod;
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
                MelonLogger.Error($"Error when fetching raw mod info for {mod}: ");
                MelonLogger.Error(e);
            }
            
            dynamic dynamicJson = JsonConvert.DeserializeObject<dynamic>(json);


            return dynamicJson;
        }*/
    }
}

public class DownloadQueueElement
{
    public PlayerId associatedPlayer;
    public ModInfo info;
    public bool notify = true;
    public bool lobby = false;
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
        Thread otherThread = new Thread(() =>
        {
            try
            {
                
                //string exportDirectory = ModFileManager.MOD_FOLDER_PATH.Replace("/", "\\") + "\\" + "tempfolder";
                string exportDirectory = Path.Combine(ModFileManager.MOD_FOLDER_PATH, "tempfolder");
                if (Directory.Exists(exportDirectory))
                {
                    Directory.Delete(exportDirectory, true);
                }

                Directory.CreateDirectory(exportDirectory);
                MelonLogger.Msg("Extracting " + ModFileManager.downloadPath + " to " + exportDirectory);
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
                        
                        // Make sure directory exists
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
 
                        string fileName = Path.GetFileName(path);
                        string tempPath = Path.Combine(Path.GetDirectoryName(path), "tempExtractedFile.temp");
                        
                        entry.ExtractToFile(tempPath, true);
       
                        File.Move(tempPath, Path.Combine(Path.GetDirectoryName(tempPath), fileName));
                    }
                    
                    archive.Dispose();
                }

                MelonLogger.Msg("Extracted " + ModFileManager.downloadPath + " to " + exportDirectory);

                // Pull the first folder out of the zip
                string palletJsonFile = ModFileManager.FindFile(exportDirectory, "pallet.json");

                
                while (palletJsonFile != "")
                {
                    string modFolder = Directory.GetParent(palletJsonFile).FullName;
                    MelonLogger.Msg("Mod folder is: " + modFolder);
                    // Make folder in mods folder named after the first directory
                    string[] split;
                    
                    if (!HelperMethods.IsAndroid())
                    {
                        split = modFolder.Split('\\');
                    }
                    else
                    {
                        split = modFolder.Split('/');
                    }

                    string modFolderName = split[split.Length - 1];
                    string modFolderDestination = ModFileManager.MOD_FOLDER_PATH + "/" + modFolderName;
                    // Make directory in mods folder
                    bool existing = false;

                    MelonLogger.Msg("Checking directory if it exists: "+modFolderDestination);
                    if (Directory.Exists(modFolderDestination))
                    {
                        MelonLogger.Msg("Directory exists: " + modFolderDestination);
                        existing = true;
                        string existingPalletJson = ModFileManager.FindFile(modFolderDestination, "pallet.json");
                        if (existingPalletJson != "")
                        {
                            string fileContents = File.ReadAllText(existingPalletJson);
                            var jsonData = (dynamic) JsonConvert.DeserializeObject<dynamic>(fileContents);
                            string palletId = (string) jsonData["objects"]["1"]["barcode"];
                            MainClass.warehousePalletReloadTargets.Add(palletId);
                        }
                        else {
                            existing = false;
                        }
                        Directory.Delete(modFolderDestination, true);
                    }
                    

                    Directory.Move(modFolder, modFolderDestination);
                    // Create modinfo.json
                    string modInfoPath = modFolderDestination + "/modinfo.json";
                    
                    string modInfoJson = (string) JsonConvert.SerializeObject(ModlistMenu.activeDownloadModInfo);
                    File.WriteAllText(modInfoPath, modInfoJson);

                    if (!existing)
                    {

                        MainClass.warehouseReloadFolders.Add(ModFileManager.FindFile(modFolderDestination, "pallet.json"));
                    }

                    // Add to installed mods
                    palletJsonFile = ModFileManager.FindFile(exportDirectory, "pallet.json");
                    MelonLogger.Msg(modFolder + " Downloaded and extracted!");
                }

                // Delete the zip
                File.Delete(ModFileManager.downloadPath);
                // Delete loose folder
                Directory.Delete(exportDirectory, true);
               
                MainClass.warehouseReloadRequested = true;
                MainClass.RequestInstallCheck();
                MainClass.subsChanged = true;
                //_client.Dispose();
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error while downloading mod {ModlistMenu.activeDownloadModInfo.modId}: " + e);
                ModFileManager.isDownloading = false;
                ModFileManager.activeDownloadQueueElement = null;
                ModFileManager.activeDownloadWebRequest = null;
                ModlistMenu.activeDownloadModInfo = null;
                //_client.Dispose();
            }
        });
        otherThread.Start();
    }
}

