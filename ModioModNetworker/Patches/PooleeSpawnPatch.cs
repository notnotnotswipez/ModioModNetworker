using HarmonyLib;
using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Representation;
using LabFusion.Senders;
using MelonLoader;
using ModioModNetworker.Data;
using ModioModNetworker.Utilities;
using Il2CppSLZ;
using Il2CppSLZ.Marrow.Pool;
using LabFusion.Player;

namespace ModioModNetworker.Patches
{
    public class PooleeSpawnPatch
    {
        [HarmonyPatch(typeof(Poolee), "OnSpawn")]
        private static class SpawnPatchClass {
            
            public static void Prefix(Poolee __instance)
            {
                if (MainClass.confirmedHostHasIt && NetworkInfo.HasServer)
                {
                    ModInfo installedModInfo = ModInfoUtilities.GetModInfoForPoolee(__instance);
                    if (installedModInfo != null)
                    {
                        using (var writer = FusionWriter.Create()) {
                            using (var data = ModlistData.Create(PlayerIdManager.LocalId, installedModInfo, ModlistData.ModType.SPAWNABLE)) {
                                writer.Write(data);
                                using (var message = FusionMessage.ModuleCreate<ModlistMessage>(writer))
                                {
                                    MessageSender.BroadcastMessageExceptSelf(NetworkChannel.Reliable, message);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Also patch the catchup spawn
        [HarmonyPatch(typeof(SpawnSender), "SendCatchupSpawn", typeof(byte), typeof(string), typeof(ushort), typeof(SerializedTransform), typeof(ulong))]
        private static class CatchupSpawnPatch
        {
            public static void Prefix(byte owner, string barcode, ushort syncId, SerializedTransform serializedTransform, ulong userId)
            {
                if (NetworkInfo.IsServer)
                {
                    ModInfo installedModInfo = ModInfoUtilities.GetModInfoForSpawnableBarcode(barcode);
                    if (installedModInfo != null)
                    {
                        using (var writer = FusionWriter.Create()) {
                            using (var data = ModlistData.Create(PlayerIdManager.LocalId, installedModInfo, ModlistData.ModType.SPAWNABLE)) {
                                writer.Write(data);
                                using (var message = FusionMessage.ModuleCreate<ModlistMessage>(writer))
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
}