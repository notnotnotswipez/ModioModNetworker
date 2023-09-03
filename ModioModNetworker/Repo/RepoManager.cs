using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using UnityEngine.Networking;

namespace ModioModNetworker.Repo
{
    public class RepoManager
    {
        public static List<string> repos = new List<string>() { "https://blrepo.laund.moe/repository.json", "https://blrepo.laund.moe/nsfw_repository.json" };
        public static Dictionary<string, RepoModInfo> barcodeModIdPair = new Dictionary<string, RepoModInfo>();
        public static Dictionary<string, string> modIdToBarcode = new Dictionary<string, string>();

        
        
        public static void LoadBarcodePairsFromRepos()
        {
            foreach (string repo in repos)
            {
                
                UnityWebRequest httpWebRequest = UnityWebRequest.Get(repo);
                httpWebRequest.SendWebRequest();

                while (!httpWebRequest.isDone)
                {
                    
                }

                if (httpWebRequest.result == UnityWebRequest.Result.ConnectionError || httpWebRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    continue;
                }

                string repoInformation = httpWebRequest.downloadHandler.text;

                /*HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(repo);
                HttpWebResponse httpWebresponse = (HttpWebResponse)httpWebRequest.GetResponse();
                StreamReader streamReader = new StreamReader(httpWebresponse.GetResponseStream());
                string repoInformation = streamReader.ReadToEnd();*/

                dynamic repoDynamic = JsonConvert.DeserializeObject<dynamic>(repoInformation, new JsonSerializerSettings
                {
                    MaxDepth = int.MaxValue
                });

                foreach (dynamic item in repoDynamic["objects"]["o:1"]["mods"])
                {
                    string refName = (string) item["ref"];
                    dynamic modObject = repoDynamic["objects"][refName];

                    string barcode = (string) modObject["barcode"];
                    if (barcode != null)
                    {
                        string thumbLink = (string) modObject["thumbnailUrl"];
                        string stripped = thumbLink.Replace("https://thumb.modcdn.io/mods", "");
                        string[] parts = stripped.Split('/');
                        string modIdDirect = parts[2];
                        string modName = (string) modObject["title"];

                        // Who needs regex anyway.....
                        string[] split = modName.Split(new string[] { "/size>" },
                            StringSplitOptions.RemoveEmptyEntries);
                        string[] secondSplit = split[1].Split(new string[] { "<mspace" },
                            StringSplitOptions.RemoveEmptyEntries);

                        string safeName = secondSplit[0].Trim();
                        string version = (string) modObject["version"];

                        RepoModInfo repoModInfo = new RepoModInfo()
                        {
                            modNumericalId = modIdDirect,
                            thumbnailLink = thumbLink,
                            summary = (string) modObject["description"],
                            modName = safeName,
                            version = version,
                            palletBarcode = barcode
                        };



                        if (!barcodeModIdPair.ContainsKey(barcode))
                        {
                            barcodeModIdPair.Add(barcode, repoModInfo);
                        }

                        modIdToBarcode.Add(modIdDirect, barcode);
                    }
                }
            }
        }

        public static RepoModInfo GetRepoModInfoFromModId(string modId)
        {
            if (modIdToBarcode.ContainsKey(modId))
            {
                string pallet = modIdToBarcode[modId];
                if (barcodeModIdPair.ContainsKey(pallet))
                {
                    return barcodeModIdPair[pallet];
                }
            }
            return null;
        }

        public static RepoModInfo GetRepoModInfoFromPalletBarcode(string palletBarcode) {
            if (barcodeModIdPair.ContainsKey(palletBarcode)) {
                return barcodeModIdPair[palletBarcode];
            }
            return null;
        }

        public static string GetPalletBarcodeFromCrateBarcode(string crateBarcode) {
            string[] split = crateBarcode.Split('.');
            string totalBuilt = split[0]+"."+split[1];
            return totalBuilt;
        }
    }

    public class RepoModInfo {
        public string modNumericalId;
        public string modName;
        public string summary;
        public string thumbnailLink;
        public string version;
        public string palletJsonPath;
        public string palletBarcode;
    }
}
