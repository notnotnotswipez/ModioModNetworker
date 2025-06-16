using HarmonyLib;
using LabFusion.Network;
using LabFusion.Representation;
using LabFusion.Senders;
using LabFusion.Utilities;
using MelonLoader;
using ModioModNetworker.Data;
using ModioModNetworker.Queue;
using ModioModNetworker.Utilities;
using Il2CppSLZ.Marrow.Warehouse;
using LabFusion.Player;
using LabFusion.Marrow;
using LabFusion.Network.Serialization;

namespace ModioModNetworker.Patches
{
    public class LevelLoadPatch
    {
        [HarmonyPatch(typeof(LevelLoadMessage), "OnHandleMessage", typeof(ReceivedMessage))]
        public static class PatchClass
        {
            public static bool Prefix(ReceivedMessage received)
            {
                if (!NetworkInfo.IsHost && !received.IsServerHandled && MainClass.autoDownloadLevels)
                {

                    var data = received.ReadData<LevelLoadData>();

                    if (!MainClass.overrideFusionDL)
                    {
                        return true;
                    }

                    // Clear the level queue no matter what because no matter what outcome it is, we are going to be loading a new level.
                    LevelHoldQueue.ClearQueue();

                    if (MainClass.confirmedHostHasIt || MainClass.useRepo)
                    {
                        if (!CrateFilterer.HasCrate<LevelCrate>(new Barcode(data.LevelBarcode)))
                        {
                            LevelHoldQueue.SetQueue(new LevelHoldQueue.LevelHoldQueueData()
                            {
                                missingBarcode = data.LevelBarcode,
                                _data = data
                            });
                            return false;
                        }
                    }

                }
                return true;
            }
        }
        
        [HarmonyPatch(typeof(LoadSender), nameof(LoadSender.SendLevelLoad), typeof(string), typeof(string), typeof(ulong))]
        private static class SendLevelPatchClass {
            
            public static void Prefix(string barcode, string loadBarcode, ulong userId)
            {
                if (!NetworkInfo.IsHost)
                    return;
                
                ModInfo installedModInfo = ModInfoUtilities.GetModInfoForLevelBarcode(barcode);
                if (installedModInfo != null)
                {
                    LobbyCreatePatch.LobbyMetaDataHelperPatch.lobbyNumericalId = installedModInfo.numericalId;
                    using (var writer = NetWriter.Create())
                    {
                        var data = ModlistData.Create(PlayerIDManager.LocalID, installedModInfo, ModlistData.ModType.LEVEL);
                        data.Serialize(writer);
                        using (var message = NetMessage.ModuleCreate<ModlistMessage>(writer, CommonMessageRoutes.ReliableToClients))
                        {
                            MessageSender.SendFromServer(userId, NetworkChannel.Reliable, message);
                        }

                    }
                }
                else {
                    LobbyCreatePatch.LobbyMetaDataHelperPatch.lobbyNumericalId = "null";
                }
            }
        }
        
        [HarmonyPatch(typeof(LoadSender), nameof(LoadSender.SendLevelLoad), typeof(string), typeof(string))]
        private static class SendLevelPatchClassGeneric {

            public static void Prefix(string barcode, string loadBarcode)
            {
                if (!NetworkInfo.IsHost)
                    return;

                ModInfo installedModInfo = ModInfoUtilities.GetModInfoForLevelBarcode(barcode);
                if (installedModInfo != null)
                {
                    using (var writer = NetWriter.Create())
                    {
                        var data = ModlistData.Create(PlayerIDManager.LocalID, installedModInfo, ModlistData.ModType.LEVEL);

                        data.Serialize(writer);
                        using (var message = NetMessage.ModuleCreate<ModlistMessage>(writer, CommonMessageRoutes.ReliableToClients))
                        {
                            MessageSender.BroadcastMessageExceptSelf(NetworkChannel.Reliable, message);
                        }

                    }
                }
            }
        }
    }
}