using HarmonyLib;
using LabFusion.Network;
using LabFusion.Representation;
using LabFusion.Senders;
using LabFusion.Utilities;
using MelonLoader;
using ModioModNetworker.Data;
using ModioModNetworker.Queue;
using ModioModNetworker.Repo;
using ModioModNetworker.Utilities;
using SLZ.Marrow.Warehouse;

namespace ModioModNetworker.Patches
{
    public class LevelLoadPatch
    {
        [HarmonyPatch(typeof(SceneLoadMessage), "HandleMessage", typeof(byte[]), typeof(bool))]
        public static class PatchClass
        {
            public static bool Prefix(byte[] bytes, bool isServerHandled = false)
            {
                if (!NetworkInfo.IsServer && !isServerHandled && MainClass.autoDownloadLevels) {
                    using (var reader = FusionReader.Create(bytes)) {
                        using (var data = reader.ReadFusionSerializable<SceneLoadData>()) {
                            
                            // Clear the level queue no matter what because no matter what outcome it is, we are going to be loading a new level.
                            LevelHoldQueue.ClearQueue();

                            if (MainClass.confirmedHostHasIt || MainClass.useRepo) {
                                if (!IsCrate(data.levelBarcode))
                                {
                                    if (MainClass.useRepo) {
                                        string palletBarcode = RepoManager.GetPalletBarcodeFromCrateBarcode(data.levelBarcode);
                                        string existingNumericalId = RepoManager.GetRepoModInfoFromPalletBarcode(palletBarcode).modNumericalId;
                                        if (existingNumericalId != null) {
                                            ModInfo.RequestModInfoNumerical(existingNumericalId, "install_level");
                                        }
                                    }
                                    LevelHoldQueue.SetQueue(new LevelHoldQueue.LevelHoldQueueData()
                                    {
                                        missingBarcode = data.levelBarcode,
                                        _data = data
                                    });
                                    return false;
                                }
                            }
                        }
                    }
                }

                return true;
            }

            private static bool IsCrate(string barcode)
            {
                LevelCrate levelCrate =
                    AssetWarehouse.Instance.GetCrate<LevelCrate>(barcode);
                if (levelCrate == null)
                {
                    return false;
                }

                return true;
            }
        }
        
        [HarmonyPatch(typeof(LoadSender), "SendLevelLoad", typeof(string), typeof(ulong))]
        private static class SendLevelPatchClass {
            
            public static void Prefix(string barcode, ulong userId)
            {
                if (!NetworkInfo.IsServer)
                    return;
                
                ModInfo installedModInfo = ModInfoUtilities.GetModInfoForLevelBarcode(barcode);
                if (installedModInfo != null)
                {
                    using (var writer = FusionWriter.Create()) {
                        using (var data = ModlistData.Create(PlayerIdManager.LocalId, installedModInfo, ModlistData.ModType.LEVEL)) {
                            writer.Write(data);
                            using (var message = FusionMessage.ModuleCreate<ModlistMessage>(writer))
                            {
                                MelonLogger.Msg("Sending level mod info to player!");
                                MessageSender.SendFromServer(userId, NetworkChannel.Reliable, message);
                            }
                        }
                    }
                }
            }
        }
        
        [HarmonyPatch(typeof(LoadSender), "SendLevelLoad", typeof(string))]
        private static class SendLevelPatchClassGeneric {
            
            public static void Prefix(string barcode)
            {
                if (!NetworkInfo.IsServer)
                    return;
                
                ModInfo installedModInfo = ModInfoUtilities.GetModInfoForLevelBarcode(barcode);
                if (installedModInfo != null)
                {
                    using (var writer = FusionWriter.Create()) {
                        using (var data = ModlistData.Create(PlayerIdManager.LocalId, installedModInfo, ModlistData.ModType.LEVEL)) {
                            writer.Write(data);
                            using (var message = FusionMessage.ModuleCreate<ModlistMessage>(writer))
                            {
                                MelonLogger.Msg("Sending level mod info to server");
                                MessageSender.BroadcastMessageExceptSelf(NetworkChannel.Reliable, message);
                            }
                        }
                    }
                }
            }
        }
    }
}