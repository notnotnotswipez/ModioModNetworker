using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ModioModNetworker.Repo
{
    public class RepoManager
    {
        public static List<string> repos = new List<string>() { "https://blrepo.laund.moe/repository.json", "https://blrepo.laund.moe/nsfw_repository.json" };
        public static Dictionary<string, RepoModInfo> barcodeModIdPair = new Dictionary<string, RepoModInfo>();
        public static Dictionary<string, string> modIdToBarcode = new Dictionary<string, string>();

        public static void LoadBarcodePairsFromRepos()
        {
            JavaScriptSerializer javaSerializer = new JavaScriptSerializer();
            javaSerializer.MaxJsonLength = int.MaxValue;
            javaSerializer.RecursionLimit = int.MaxValue;
            
            foreach (string repo in repos)
            {
                using (System.Net.WebClient webClient = new System.Net.WebClient())
                {
                    string repoInformation = webClient.DownloadString(repo);

                    dynamic repoDynamic = javaSerializer.Deserialize<dynamic>(repoInformation);
                    foreach (dynamic item in repoDynamic["objects"]["o:1"]["mods"]) {
                        string refName = item["ref"];
                        dynamic modObject = repoDynamic["objects"][refName];

                        string barcode = modObject["barcode"];
                        if (barcode != null)
                        { 
                            string thumbLink = modObject["thumbnailUrl"];
                            string stripped = thumbLink.Replace("https://thumb.modcdn.io/mods", "");
                            string[] parts = stripped.Split('/');
                            string modIdDirect = parts[2];
                            string modName = modObject["title"];

                            // Who needs regex anyway.....
                            string[] split = modName.Split(new string[] { "/size>" }, StringSplitOptions.RemoveEmptyEntries);
                            string[] secondSplit = split[1].Split(new string[] { "<mspace" }, StringSplitOptions.RemoveEmptyEntries);

                            string safeName = secondSplit[0].Trim();
                            string version = modObject["version"];

                            RepoModInfo repoModInfo = new RepoModInfo()
                            {
                                modNumericalId = modIdDirect,
                                thumbnailLink = thumbLink,
                                summary = modObject["description"],
                                modName = safeName,
                                version = version,
                                palletBarcode = barcode
                            };

                            if (!barcodeModIdPair.ContainsKey(barcode)) {
                                barcodeModIdPair.Add(barcode, repoModInfo);
                            }
                            modIdToBarcode.Add(modIdDirect, barcode);
                        }
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
