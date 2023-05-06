using System;
using System.Collections.Generic;
using System.Web.WebPages;
using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Representation;
using LabFusion.Utilities;
using MelonLoader;
using ModioModNetworker.Data;

namespace ModioModNetworker
{
    public class ModlistData : IFusionSerializable, IDisposable
    {
        public bool forPlayer = false;
        public PlayerId playerId;
        public bool isFinal = false;
        public bool valid = false;
        public bool mature = false;
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
            writer.Write(forPlayer);
            writer.Write(mature);
            writer.Write(playerId.SmallId);
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
            forPlayer = reader.ReadBoolean();
            mature = reader.ReadBoolean();
            playerId = PlayerIdManager.GetPlayerId(reader.ReadByte());
            valid = reader.ReadBoolean();
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

        public static ModlistData Create(bool final, ModInfo info)
        {

            return new ModlistData()
            {
                isFinal = final,
                forPlayer = false,
                modId = info.modId,
                mature = info.mature,
                playerId = PlayerIdManager.LocalId,
                versionNumber = info.version,
                downloadLink = info.directDownloadLink,
                fileSize = info.fileSizeKB,
                valid = info.isValidMod
            };
        }
        
        public static ModlistData Create(PlayerId playerId, ModInfo info)
        {

            return new ModlistData()
            {
                isFinal = false,
                forPlayer = true,
                playerId = playerId,
                modId = info.modId,
                mature = info.mature,
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
        public static Dictionary<PlayerId, ModInfo> avatarMods = new Dictionary<PlayerId, ModInfo>();

        public override void HandleMessage(byte[] bytes, bool isServerHandled = false)
        {
            using (var reader = FusionReader.Create(bytes))
            {
                using (var data = reader.ReadFusionSerializable<ModlistData>())
                {
                    if (NetworkInfo.IsServer && isServerHandled)
                    {
                        if (data.forPlayer)
                        {
                            using (var message = FusionMessage.ModuleCreate<ModlistMessage>(bytes))
                            {
                                MessageSender.BroadcastMessageExcept(data.playerId, NetworkChannel.Reliable, message);
                            }
                        }
                        else
                        {
                            return;
                        }
                    }

                    if (data != null)
                    {
                        ModInfo modInfo = data.modInfo;
                        if (data.forPlayer)
                        {
                            if (modInfo.isValidMod)
                            {
                                float mb = modInfo.fileSizeKB / 1000000;
                                if (mb < MainClass.maxAvatarMb && MainClass.autoDownloadAvatars)
                                {
                                    if (!MainClass.downloadMatureContent && modInfo.mature)
                                    {
                                        return;
                                    }

                                    if (ModFileManager.AddToQueue(modInfo))
                                    {
                                        // Its kinda annoying to see this message every time you download an avatar, theres a reason why VRChat doesn't do this :)
                                        MainClass.ignoreNextNotification = true;
                                    }
                                }

                                if (!avatarMods.ContainsKey(data.playerId))
                                {
                                    avatarMods.Add(data.playerId, modInfo);
                                }
                                else
                                {
                                    avatarMods[data.playerId] = modInfo;
                                }
                            }
                        }
                        else
                        {
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
}