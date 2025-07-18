using HarmonyLib;
using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Representation;
using LabFusion.Senders;
using MelonLoader;
using ModioModNetworker.Data;
using Il2CppSLZ.Marrow.SceneStreaming;
using Il2CppSLZ.Marrow.Warehouse;
using LabFusion.Player;
using LabFusion.Network.Serialization;

namespace ModioModNetworker.Patches
{
    public class AvatarSwitchPatch
    {
        // TODO: REPO DOWN
        /*[HarmonyPatch(typeof(PlayerRep), "OnSwapAvatar")]
        public class OnRepSwapAvatarPatch {
            public static void Postfix(PlayerRep __instance, bool success) {
                if (!success && MainClass.useRepo && MainClass.autoDownloadAvatars)
                {
                    
                    string name;
                    __instance.PlayerId.TryGetDisplayName(out name);
                    string avatarBarcode = __instance.avatarId;
                    string palletBarcode = RepoManager.GetPalletBarcodeFromCrateBarcode(avatarBarcode);
                    RepoModInfo repoModInfo = RepoManager.GetRepoModInfoFromPalletBarcode(palletBarcode);

                    if (repoModInfo != null)
                    {
                        string existingNumericalId = repoModInfo.modNumericalId;
                        if (existingNumericalId != null)
                        {
                            ModInfo.RequestModInfoNumerical(existingNumericalId, "install_avatar;" + __instance.PlayerId.SmallId);
                        }
                    }
                    else {
                        
        .Error("We DO NOT have any repo information on: " + palletBarcode);
                    }
                }
            }
        }*/


        [HarmonyPatch(typeof(PlayerSender), "SendPlayerAvatar")]
        public static class PlayerSenderPatch
        {
            public static void Postfix()
            {
                if (NetworkInfo.HasServer && MainClass.confirmedHostHasIt)
                {
                    Data.ModInfo avatarModInfo = null;
                    foreach (var installedModInfo in MainClass.InstalledModInfos)
                    {
                        if (installedModInfo.palletBarcode == RigData.Refs.RigManager._avatarCrate.Crate._pallet._barcode._id)
                        {
                            avatarModInfo = installedModInfo.ModInfo;
                            break;
                        }
                    }

                    if (!ModlistMessage.avatarMods.ContainsKey(PlayerIDManager.LocalID))
                    {
                        ModlistMessage.avatarMods.Add(PlayerIDManager.LocalID, avatarModInfo);
                    }
                    else
                    {
                        ModlistMessage.avatarMods[PlayerIDManager.LocalID] = avatarModInfo;
                    }


                    if (avatarModInfo != null)
                    {
                        using (var writer = NetWriter.Create())
                        {
                            var data = ModlistData.Create(PlayerIDManager.LocalID, avatarModInfo, ModlistData.ModType.AVATAR);
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
}