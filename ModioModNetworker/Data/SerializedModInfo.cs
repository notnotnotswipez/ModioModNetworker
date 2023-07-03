using LabFusion.Data;
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
        private string downloadLink;
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
                downloadLink = info.directDownloadLink,
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
            string[] split = received.Split(GameObjectUtilities.PathSeparator);
            numericalId = split[0];
            versionNumber = split[1];
            downloadLink = split[2];
            modId = split[3];

            RepoModInfo repoModInfo = RepoManager.GetRepoModInfoFromModId(numericalId);
            modInfo = new ModInfo
            {
                structureVersion = ModInfo.globalStructureVersion,
                isValidMod = valid,
                numericalId = numericalId,
                modId = modId,
                mature = mature,
                version = versionNumber,
                directDownloadLink = downloadLink,
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
            built += numericalId + GameObjectUtilities.PathSeparator;
            built += versionNumber + GameObjectUtilities.PathSeparator;
            built += downloadLink + GameObjectUtilities.PathSeparator;
            built += modId;
            writer.Write(built);
        }
    }
}