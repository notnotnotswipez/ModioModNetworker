using HarmonyLib;
using LabFusion.Network;
using MelonLoader;
using ModioModNetworker.Data;
using ModioModNetworker.Queue;
using ModioModNetworker.Repo;
using ModioModNetworker.Utilities;
using SLZ.Marrow.Warehouse;

namespace ModioModNetworker.Patches
{
    public class SpawnableResponsePatch 
    {
        [HarmonyPatch(typeof(SpawnResponseMessage), "HandleMessage", typeof(byte[]), typeof(bool))]
        public static class PatchClass
        {
            public static bool Prefix(byte[] bytes, bool isServerHandled = false)
            {
                if (!isServerHandled && MainClass.autoDownloadSpawnables)
                {
                    using (var reader = FusionReader.Create(bytes))
                    {
                        using (var data = reader.ReadFusionSerializable<SpawnResponseData>())
                        {
                            if (!MainClass.confirmedHostHasIt && !MainClass.useRepo) {
                                return true;
                            }

                            if (!IsCrate(data.barcode))
                            {
                                if (MainClass.useRepo)
                                {
                                    string palletBarcode = RepoManager.GetPalletBarcodeFromCrateBarcode(data.barcode);
                                    string existingNumericalId = RepoManager.GetRepoModInfoFromPalletBarcode(palletBarcode).modNumericalId;
                                    if (existingNumericalId != null)
                                    {
                                        ModInfo.RequestModInfoNumerical(existingNumericalId, "install_spawnable");
                                    }
                                }
                                SpawnableHoldQueue.AddToQueue(new SpawnableHoldQueueData()
                                {
                                    missingBarcode = data.barcode,
                                    _data = data
                                });
                                return false;
                            }
                            
                            if (LevelHoldQueue.LevelInQueue())
                            {
                                SpawnableHoldQueue.AddToQueue(data);
                                return false;
                            }
                        }
                    }
                }

                return true;
            }

            private static bool IsCrate(string barcode)
            {
                GameObjectCrate gameObjectCrate =
                    AssetWarehouse.Instance.GetCrate<GameObjectCrate>(barcode);
                if (gameObjectCrate == null)
                {
                    return false;
                }

                return true;
            }
        }
    }
}