using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Utilities;

namespace ModioModNetworker.Data
{
    public class SerializedModInfo : IFusionSerializable
    {
        public bool valid = false;
        public bool mature = false;
        public ModInfo modInfo;
        private float fileSize;
        private string downloadLink;
        private string modId;
        private string versionNumber;
        
        public static SerializedModInfo Create(ModInfo info)
        {
            return new SerializedModInfo()
            {
                valid = info.isValidMod,
                mature = info.mature,
                fileSize = info.fileSizeKB,
                downloadLink = info.directDownloadLink,
                modId = info.modId,
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
            modId = split[0];
            versionNumber = split[1];
            downloadLink = split[2];
            modInfo = new ModInfo
            {
                structureVersion = ModInfo.globalStructureVersion,
                isValidMod = valid,
                modId = modId,
                mature = mature,
                version = versionNumber,
                directDownloadLink = downloadLink,
                fileSizeKB = fileSize,
                fileName = modId+".zip"
            };
        }

        public void Serialize(FusionWriter writer)
        {
            writer.Write(valid);
            writer.Write(mature);
            writer.Write(fileSize);
            string built = "";
            built += modId + GameObjectUtilities.PathSeparator;
            built += versionNumber + GameObjectUtilities.PathSeparator;
            built += downloadLink;
            writer.Write(built);
        }
    }
}