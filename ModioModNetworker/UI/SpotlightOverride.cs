using MelonLoader;
using ModioModNetworker.Data;
using ModIoModNetworker.Ui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ModioModNetworker.UI
{
    public class SpotlightOverride
    {
        public string manualDisplayId;
        public string descriptionOverride;
        public string titleOverride;
        public string subTitle;
        public ModInfo downloadedInfo;
        public Texture cachedThumbnail;

        public static void LoadFromRegularURL()
        {
            UnityWebRequest httpWebRequest = UnityWebRequest.Get("https://raw.githubusercontent.com/notnotnotswipez/ModioModNetworker/networker-spotlight/modSpotlight.txt");
            var requestSent = httpWebRequest.SendWebRequest();

            requestSent.m_completeCallback += new System.Action<AsyncOperation>((asyncOperation) =>
            {
                string repoInformation = httpWebRequest.downloadHandler.text;

                string[] lines = repoInformation.Split('\n');
                foreach (string line in lines)
                {
                    if (line.StartsWith("manualDisplayMod: ")) {
                        MelonLogger.Msg("READ MANUAL DISPLAY ID");
                        NetworkerMenuController.spotlightOverride.manualDisplayId = GetSegmentOrNull(line, "manualDisplayMod: ");

                        if (NetworkerMenuController.spotlightOverride.manualDisplayId != null) {
                            ModInfo.RequestModInfoNumerical(NetworkerMenuController.spotlightOverride.manualDisplayId, "spotlight");
                        }
                    }

                    if (line.StartsWith("subTitle: "))
                    {
                        NetworkerMenuController.spotlightOverride.subTitle = GetSegmentOrNull(line, "subTitle: ");
                    }
                    if (line.StartsWith("titleOverride: "))
                    {
                        NetworkerMenuController.spotlightOverride.titleOverride = GetSegmentOrNull(line, "titleOverride: ");
                    }
                    if (line.StartsWith("descriptionOverride: "))
                    {
                        NetworkerMenuController.spotlightOverride.descriptionOverride = GetSegmentOrNull(line, "descriptionOverride: ");
                    }

                    MelonLogger.Msg("Read line from online: " + line);
                }
            });
        }

        private static string GetSegmentOrNull(string line, string startWith) {
            string result = line.Replace(startWith, "");
            if (result != "null") {
                return result;
            }

            return null;
        }
    }
}
