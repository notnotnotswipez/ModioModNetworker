using LabFusion.Data;
using LabFusion.Extensions;
using LabFusion.Network;
using LabFusion.Network.Serialization;
using LabFusion.Utilities;
using MelonLoader;
using static UnityEngine.ResourceManagement.Util.BinaryStorageBuffer;

namespace ModioModNetworker.Data
{
    public class SerializedModInfo : INetSerializable
    {
        public bool valid = false;
        public bool mature = false;
        public ModInfo modInfo;
        private float fileSize;
        private string windowsDownloadLink;
        private string androidDownloadLink;
        private string thumbnailUrl;
        private string summary;
        private string modName;
        private string numericalId;
        private string versionNumber;
        private string modId;
        private List<string> tags;
        
        public static SerializedModInfo Create(ModInfo info)
        {
            return new SerializedModInfo()
            {
                valid = info.isValidMod,
                mature = info.mature,
                modId = info.modId,
                fileSize = info.fileSizeKB,
                windowsDownloadLink = info.windowsDownloadLink,
                androidDownloadLink = info.androidDownloadLink,
                numericalId = info.numericalId,
                versionNumber = info.version,
                tags = info.tags,
                modName = info.modName,
                thumbnailUrl = info.thumbnailLink,
                summary = info.modSummary
            };
        }

        public void Serialize(INetSerializer serializer)
        {
            if (serializer.IsReader)
            {
                serializer.SerializeValue(ref valid);
                serializer.SerializeValue(ref mature);
                serializer.SerializeValue(ref fileSize);
                serializer.SerializeValue(ref numericalId);
                string received = "";
                serializer.SerializeValue(ref received);
                string[] split = received.Split(StringExtensions.UniqueSeparator);
                versionNumber = split[0];
                windowsDownloadLink = split[1];
                androidDownloadLink = split[2];
                modId = split[3];
                thumbnailUrl = split[4];
                summary = split[5];
                modName = split[6];
                int tagCount = int.Parse(split[7]);
                List<string> tags = new List<string>();

                int starting = 8;

                for (int i = 0; i < tagCount; i++)
                {
                    tags.Add(split[i + starting]);
                }

                modInfo = new ModInfo
                {
                    structureVersion = ModInfo.globalStructureVersion,
                    isValidMod = valid,
                    numericalId = numericalId,
                    modId = modId,
                    mature = mature,
                    version = versionNumber,
                    windowsDownloadLink = windowsDownloadLink,
                    androidDownloadLink = androidDownloadLink,
                    fileSizeKB = fileSize,
                    fileName = modId + ".zip",
                    modSummary = summary,
                    thumbnailLink = thumbnailUrl,
                    modName = modName,
                    tags = tags
                };
            }
            else {
                serializer.SerializeValue(ref valid);
                serializer.SerializeValue(ref mature);
                serializer.SerializeValue(ref fileSize);
                serializer.SerializeValue(ref numericalId);
                string built = "";
                built += versionNumber + StringExtensions.UniqueSeparator;
                built += windowsDownloadLink + StringExtensions.UniqueSeparator;
                built += androidDownloadLink + StringExtensions.UniqueSeparator;
                built += modId + StringExtensions.UniqueSeparator;
                built += thumbnailUrl + StringExtensions.UniqueSeparator;
                built += summary + StringExtensions.UniqueSeparator;
                built += modName + StringExtensions.UniqueSeparator;
                built += tags.Count;

                foreach (var tag in tags)
                {
                    built += StringExtensions.UniqueSeparator + tag;
                }

                serializer.SerializeValue(ref built);
            }
        }

        public string ToDebugString() {
            string built = "";
            built += versionNumber + StringExtensions.UniqueSeparator;
            built += windowsDownloadLink + StringExtensions.UniqueSeparator;
            built += androidDownloadLink + StringExtensions.UniqueSeparator;
            built += modId + StringExtensions.UniqueSeparator;
            built += thumbnailUrl + StringExtensions.UniqueSeparator;
            built += summary + StringExtensions.UniqueSeparator;
            built += modName + StringExtensions.UniqueSeparator;
            built += tags.Count;

            foreach (var tag in tags)
            {
                built += StringExtensions.UniqueSeparator + tag;
            }

            return $"Serialized mod: {built} is valid {valid}, mature {mature}, numerical {numericalId}";
        }
    }
}