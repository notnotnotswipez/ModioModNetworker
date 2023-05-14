using System;
using System.Collections.Generic;
using System.Web.WebPages;
using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Representation;
using LabFusion.Utilities;
using MelonLoader;
using ModioModNetworker.Data;
using SLZ.Marrow.SceneStreaming;

namespace ModioModNetworker
{
    public class ModlistData : IFusionSerializable, IDisposable
    {
        public enum ModType {
            LIST = 1,
            AVATAR = 2,
            SPAWNABLE = 3,
            LEVEL = 4,
        }
        
        public PlayerId playerId;
        public bool isFinal = false;
        public ModType modType = ModType.LIST;
        public SerializedModInfo serializedModInfo;
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public void Serialize(FusionWriter writer)
        {
            writer.Write(isFinal);
            writer.Write((byte)modType);
            writer.Write(playerId.SmallId);
            writer.Write(serializedModInfo);
        }

        public void Deserialize(FusionReader reader)
        {
            isFinal = reader.ReadBoolean();
            modType = (ModType)reader.ReadByte();
            playerId = PlayerIdManager.GetPlayerId(reader.ReadByte());
            serializedModInfo = reader.ReadFusionSerializable<SerializedModInfo>();
        }

        public static ModlistData Create(bool final, ModInfo info)
        {

            return new ModlistData()
            {
                isFinal = final,
                playerId = PlayerIdManager.LocalId,
                modType = ModType.LIST,
                serializedModInfo = SerializedModInfo.Create(info)
            };
        }
        
        public static ModlistData Create(PlayerId playerId, ModInfo info, ModType infoType)
        {

            return new ModlistData()
            {
                isFinal = false,
                modType = infoType,
                playerId = playerId,
                serializedModInfo = SerializedModInfo.Create(info)
            };
        }
    }
    
    public class ModlistMessage : ModuleMessageHandler
    {
        private static List<ModInfo> modlist = new List<ModInfo>();
        public static Dictionary<PlayerId, ModInfo> avatarMods = new Dictionary<PlayerId, ModInfo>();
        public static List<ModInfo> waitAndQueue = new List<ModInfo>();

        public override void HandleMessage(byte[] bytes, bool isServerHandled = false)
        {
            using (var reader = FusionReader.Create(bytes))
            {
                using (var data = reader.ReadFusionSerializable<ModlistData>())
                {
                    if (NetworkInfo.IsServer && isServerHandled)
                    {
                        if (data.modType != ModlistData.ModType.LIST)
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
                        ModInfo modInfo = data.serializedModInfo.modInfo;
                        if (data.modType != ModlistData.ModType.LIST)
                        {
                            if (modInfo.isValidMod)
                            {
                                switch (data.modType)
                                {
                                    case ModlistData.ModType.AVATAR:
                                    {
                                        if (!avatarMods.ContainsKey(data.playerId))
                                        {
                                            avatarMods.Add(data.playerId, modInfo);
                                        }
                                        else
                                        {
                                            avatarMods[data.playerId] = modInfo;
                                        }
                                
                                        if (SceneStreamer._session != null)
                                        {
                                            if (SceneStreamer._session.Status == StreamStatus.LOADING)
                                            {
                                                waitAndQueue.Add(modInfo);
                                                return;
                                            }
                                        }

                                        float mb = modInfo.fileSizeKB / 1000000;
                                        if (mb < MainClass.maxAutoDownloadMb && MainClass.autoDownloadAvatars)
                                        {
                                            if (!MainClass.downloadMatureContent && modInfo.mature)
                                            {
                                                return;
                                            }
                                            
                                            if (MainClass.tempLobbyMods)
                                            {
                                                modInfo.temp = true;
                                            }

                                            ModFileManager.AddToQueue(new DownloadQueueElement()
                                            {
                                                associatedPlayer = data.playerId,
                                                info = modInfo,
                                                notify = false
                                            });
                                        }
                                        break;
                                    }
                                    case ModlistData.ModType.SPAWNABLE:
                                    {
                                        float mb = modInfo.fileSizeKB / 1000000;
                        
                                        if (MainClass.autoDownloadSpawnables && mb < MainClass.maxAutoDownloadMb)
                                        {
                                            if (!MainClass.downloadMatureContent && modInfo.mature)
                                            {
                                                return;
                                            }
                                            
                                            if (MainClass.tempLobbyMods)
                                            {
                                                modInfo.temp = true;
                                            }
                            
                                            ModFileManager.AddToQueue(new DownloadQueueElement()
                                            {
                                                associatedPlayer = null,
                                                info = modInfo,
                                            });
                                        }

                                        break;
                                    }
                                    case ModlistData.ModType.LEVEL:
                                    {
                                        float mb = modInfo.fileSizeKB / 1000000;
                                        float gb = mb / 1000;
                        
                                        if (MainClass.autoDownloadLevels && gb < MainClass.levelMaxGb)
                                        {
                                            if (!MainClass.downloadMatureContent && modInfo.mature)
                                            {
                                                return;
                                            }

                                            if (MainClass.tempLobbyMods)
                                            {
                                                modInfo.temp = true;
                                            }

                                            ModFileManager.AddToQueue(new DownloadQueueElement()
                                            {
                                                associatedPlayer = null,
                                                info = modInfo,
                                            });
                                        }

                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (modInfo.isValidMod)
                            {
                                modlist.Add(modInfo);
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