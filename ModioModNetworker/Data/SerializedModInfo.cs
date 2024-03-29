using LabFusion.Data;
using LabFusion.Extensions;
using LabFusion.Network;
using LabFusion.Utilities;
using ModioModNetworker.Repo;

namespace ModioModNetworker.Data
{
    public class SerializedModInfo : IFusionSerializable
    {
        public bool valid = false;
        public bool mature = false;
        public ModInfo modInfo;
        private float fileSize;
        private string windowsDownloadLink;
        private string androidDownloadLink;
        private string numericalId;
        private string versionNumber;
        private string modId;
        
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
                versionNumber = info.version
            };
        }
        
        public void Deserialize(FusionReader reader)
        {
            valid = reader.ReadBoolean();
            mature = reader.ReadBoolean();
            fileSize = reader.ReadSingle();
            string received = reader.ReadString();
            string[] split = received.Split(StringExtensions.UniqueSeparator);
            numericalId = split[0];
            versionNumber = split[1];
            windowsDownloadLink = split[2];
            androidDownloadLink = split[3];
            modId = split[4];

            RepoModInfo repoModInfo = RepoManager.GetRepoModInfoFromModId(numericalId);
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
                fileName = modId+".zip"
            };
            if (repoModInfo != null) {
                modInfo.modSummary = repoModInfo.summary;
                modInfo.modName = repoModInfo.modName;
                modInfo.thumbnailLink = repoModInfo.thumbnailLink;
            }
        }

        public void Serialize(FusionWriter writer)
        {
            writer.Write(valid);
            writer.Write(mature);
            writer.Write(fileSize);
            string built = "";
            built += numericalId + StringExtensions.UniqueSeparator;
            built += versionNumber + StringExtensions.UniqueSeparator;
            built += windowsDownloadLink + StringExtensions.UniqueSeparator;
            built += androidDownloadLink + StringExtensions.UniqueSeparator;
            built += modId;
            writer.Write(built);
        }
    }
}