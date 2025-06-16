using System;
using System.Collections.Generic;
using Il2CppSLZ.Marrow.SceneStreaming;
using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Network.Serialization;
using LabFusion.Player;
using LabFusion.Representation;
using LabFusion.SDK.Modules;
using LabFusion.Utilities;
using MelonLoader;
using ModioModNetworker.Data;
using ModIoModNetworker.Ui;

namespace ModioModNetworker
{
    public class ModlistData : INetSerializable
    {
        public enum ModType
        {
            LIST = 1,
            AVATAR = 2,
            SPAWNABLE = 3,
            LEVEL = 4,
        }

        public PlayerID playerId;
        public bool isFinal = false;
        public ModType modType = ModType.LIST;
        public SerializedModInfo serializedModInfo;

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public static ModlistData Create(bool final, Data.ModInfo info)
        {

            return new ModlistData()
            {
                isFinal = final,
                playerId = PlayerIDManager.LocalID,
                modType = ModType.LIST,
                serializedModInfo = SerializedModInfo.Create(info)
            };
        }

        public static ModlistData Create(PlayerID playerId, Data.ModInfo info, ModType infoType)
        {

            return new ModlistData()
            {
                isFinal = false,
                modType = infoType,
                playerId = playerId,
                serializedModInfo = SerializedModInfo.Create(info)
            };
        }

        public void Serialize(INetSerializer serializer)
        {
            serializer.SerializeValue(ref isFinal);
            serializer.SerializeValue(ref modType);
            serializer.SerializeValue(ref playerId);
            serializer.SerializeValue(ref serializedModInfo);
        }
    }

    public class ModlistMessage : ModuleMessageHandler
    {
        private static List<Data.ModInfo> modlist = new List<Data.ModInfo>();
        public static Dictionary<PlayerID, Data.ModInfo> avatarMods = new Dictionary<PlayerID, Data.ModInfo>();
        public static List<Data.ModInfo> waitAndQueue = new List<Data.ModInfo>();

        protected override void OnHandleMessage(ReceivedMessage message)
        {

            var data = message.ReadData<ModlistData>();

            if (NetworkInfo.IsHost && message.IsServerHandled)
            {
                if (data.modType != ModlistData.ModType.LIST)
                {
                    using (var messageSent = NetMessage.ModuleCreate<ModlistMessage>(message.Bytes, CommonMessageRoutes.ReliableToClients))
                    {
                        MessageSender.BroadcastMessageExcept(data.playerId, NetworkChannel.Reliable, messageSent);
                    }
                }
                else
                {
                    return;
                }
            }

            if (data != null)
            {
                Data.ModInfo modInfo = data.serializedModInfo.modInfo;
                if (data.modType != ModlistData.ModType.LIST)
                {
                    if (true)
                    {
                        switch (data.modType)
                        {
                            case ModlistData.ModType.AVATAR:
                                {

                                    if (!MainClass.overrideFusionDL)
                                    {
                                        return;
                                    }

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

                                        if (MainClass.modNumericalsDownloadedDuringLobbySession.Contains(modInfo.numericalId))
                                        {
                                            return;
                                        }

                                        if (modInfo.IsSubscribed())
                                        {
                                            return;
                                        }

                                        if (MainClass.tempLobbyMods)
                                        {
                                            modInfo.temp = true;
                                        }

                                        if (ModFileManager.AddToQueue(new DownloadQueueElement()
                                        {
                                            associatedPlayer = data.playerId,
                                            info = modInfo,
                                            notify = true
                                        }))
                                        {
                                            MainClass.modNumericalsDownloadedDuringLobbySession.Add(modInfo.numericalId);
                                        }
                                    }
                                    break;
                                }
                            case ModlistData.ModType.SPAWNABLE:
                                {

                                    if (!MainClass.overrideFusionDL)
                                    {
                                        return;
                                    }

                                    float mb = modInfo.fileSizeKB / 1000000;

                                    if (MainClass.autoDownloadSpawnables && mb < MainClass.maxAutoDownloadMb)
                                    {
                                        if (!MainClass.downloadMatureContent && modInfo.mature)
                                        {
                                            return;
                                        }

                                        if (modInfo.IsSubscribed())
                                        {
                                            return;
                                        }

                                        if (MainClass.modNumericalsDownloadedDuringLobbySession.Contains(modInfo.numericalId))
                                        {
                                            return;
                                        }

                                        if (MainClass.tempLobbyMods)
                                        {
                                            modInfo.temp = true;
                                        }

                                        if (ModFileManager.AddToQueue(new DownloadQueueElement()
                                        {
                                            associatedPlayer = null,
                                            info = modInfo,
                                            notify = true
                                        }))
                                        {
                                            MainClass.modNumericalsDownloadedDuringLobbySession.Add(modInfo.numericalId);
                                        }
                                    }

                                    break;
                                }
                            case ModlistData.ModType.LEVEL:
                                {
                                    if (!MainClass.overrideFusionDL)
                                    {
                                        return;
                                    }

                                    if (MainClass.useRepo)
                                    {
                                        return;
                                    }

                                    float mb = modInfo.fileSizeKB / 1000000;
                                    float gb = mb / 1000;

                                    if (MainClass.autoDownloadLevels && gb < MainClass.levelMaxGb)
                                    {
                                        if (!MainClass.downloadMatureContent && modInfo.mature)
                                        {
                                            return;
                                        }

                                        if (MainClass.modNumericalsDownloadedDuringLobbySession.Contains(modInfo.numericalId))
                                        {
                                            return;
                                        }

                                        if (MainClass.tempLobbyMods)
                                        {
                                            modInfo.temp = true;
                                        }

                                        if (ModFileManager.AddToQueue(new DownloadQueueElement()
                                        {
                                            associatedPlayer = null,
                                            info = modInfo,
                                            notify = true
                                        }))
                                        {
                                            MainClass.modNumericalsDownloadedDuringLobbySession.Add(modInfo.numericalId);
                                        }
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
                        NetworkerMenuController.SetHostSubscribedMods(modlist);
                        modlist.Clear();
                    }
                }
            }
        }
    }
}