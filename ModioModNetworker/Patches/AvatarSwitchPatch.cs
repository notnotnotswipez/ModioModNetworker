using HarmonyLib;
using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Representation;
using LabFusion.Senders;
using MelonLoader;
using ModioModNetworker.Data;

namespace ModioModNetworker.Patches
{
    public class AvatarSwitchPatch
    {

        [HarmonyPatch(typeof(PlayerSender), "SendPlayerAvatar")]
        public static class PlayerSenderPatch
        {
            public static void Postfix()
            {
                if (NetworkInfo.HasServer && MainClass.confirmedHostHasIt)
                {
                    ModInfo avatarModInfo = null;
                    foreach (var installedModInfo in MainClass.InstalledModInfos)
                    {
                        if (installedModInfo.palletBarcode == RigData.RigReferences.RigManager._avatarCrate.Crate._pallet._barcode)
                        {
                            avatarModInfo = installedModInfo.ModInfo;
                            break;
                        }
                    }

                    if (!ModlistMessage.avatarMods.ContainsKey(PlayerIdManager.LocalId))
                    {
                        ModlistMessage.avatarMods.Add(PlayerIdManager.LocalId, avatarModInfo);
                    }
                    else
                    {
                        ModlistMessage.avatarMods[PlayerIdManager.LocalId] = avatarModInfo;
                    }


                    if (avatarModInfo != null)
                    {
                        using (var writer = FusionWriter.Create()) {
                            using (var data = ModlistData.Create(PlayerIdManager.LocalId, avatarModInfo, ModlistData.ModType.AVATAR)) {
                                writer.Write(data);
                                using (var message = FusionMessage.ModuleCreate<ModlistMessage>(writer))
                                {
                                    MelonLogger.Msg("Sending avatar mod info to server");
                                    MessageSender.BroadcastMessageExceptSelf(NetworkChannel.Reliable, message);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}