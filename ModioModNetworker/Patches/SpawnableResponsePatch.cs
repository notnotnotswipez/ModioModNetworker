using HarmonyLib;
using LabFusion.Network;
using MelonLoader;
using ModioModNetworker.Queue;
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
                if (!isServerHandled)
                {
                    using (var reader = FusionReader.Create(bytes))
                    {
                        using (var data = reader.ReadFusionSerializable<SpawnResponseData>())
                        {
                            if (!IsCrate(data.barcode))
                            {
                                MelonLogger.Msg("SpawnableResponsePatch: Adding to queue");
                                SpawnableHoldQueue.AddToQueue(new SpawnableHoldQueueData()
                                {
                                    missingBarcode = data.barcode,
                                    _data = data
                                });
                                return false;
                            }
                            
                            if (LevelHoldQueue.LevelInQueue())
                            {
                                MelonLogger.Msg("SpawnableResponsePatch: Level in queue, stalling spawnable");
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