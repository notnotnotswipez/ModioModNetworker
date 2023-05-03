using System;
using System.Collections.Generic;
using System.Web.WebPages;
using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Utilities;
using MelonLoader;
using ModioModNetworker.Data;

namespace ModioModNetworker
{
    public class ModlistData : IFusionSerializable, IDisposable
    {
        public bool isFinal = false;
        public bool valid = false;
        public ModInfo modInfo;
        private float fileSize;
        private string downloadLink;
        private string modId;
        private string versionNumber;

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public void Serialize(FusionWriter writer)
        {
            writer.Write(isFinal);
            writer.Write(valid);
            writer.Write(fileSize);
            string built = "";
            built += modId + GameObjectUtilities.PathSeparator;
            built += versionNumber + GameObjectUtilities.PathSeparator;
            built += downloadLink;
            writer.Write(built);
        }

        public void Deserialize(FusionReader reader)
        {
            isFinal = reader.ReadBoolean();
            valid = reader.ReadBoolean();
            fileSize = reader.ReadSingle();
            string received = reader.ReadString();
            string[] split = received.Split(GameObjectUtilities.PathSeparator);
            modId = split[0];
            versionNumber = split[1];
            downloadLink = split[2];
            modInfo = new ModInfo
            {
                isValidMod = valid,
                modId = modId,
                version = versionNumber,
                directDownloadLink = downloadLink,
                fileSizeKB = fileSize,
                fileName = modId+".zip"
            };
        }

        public static ModlistData Create(bool final, ModInfo info)
        {

            return new ModlistData()
            {
                isFinal = final,
                modId = info.modId,
                versionNumber = info.version,
                downloadLink = info.directDownloadLink,
                fileSize = info.fileSizeKB,
                valid = info.isValidMod
            };
        }
    }
    
    public class ModlistMessage : ModuleMessageHandler
    {
        private static List<ModInfo> modlist = new List<ModInfo>();

        public override void HandleMessage(byte[] bytes, bool isServerHandled = false)
        {
            using (var reader = FusionReader.Create(bytes))
            {
                using (var data = reader.ReadFusionSerializable<ModlistData>())
                {
                    if (NetworkInfo.IsServer && isServerHandled)
                    {
                        return;
                    }

                    if (data != null)
                    {
                        ModInfo modInfo = data.modInfo;
                        if (modInfo.isValidMod)
                        {
                            modlist.Add(data.modInfo);
                        }
                        if (data.isFinal)
                        {
                            MelonLogger.Msg("Got modlist data");
                            ModlistMenu.PopulateModInfos(modlist);
                            modlist.Clear();
                        }
                    }
                }
            }
        }
    }
}